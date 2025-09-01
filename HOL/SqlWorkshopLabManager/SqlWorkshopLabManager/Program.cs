using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.EventHubs.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqlHolWorkshopLabManager
{
	public class Program
	{
		private const int MaxDop = 10;	// Too large a number can result in diminishing returns, Azure rate limiting, and/or throttling with 429 (too many requests) errors

		private static string _resourceGroupName;
		private static string _resourceRegionName;
		private static string _sqlDatabaseServerName;
		private static string _sqlDatabaseUsername;
		private static string _sqlDatabasePassword;
		private static string _eventHubNamespaceName;
		private static string _eventHubName;
		private static string _eventHubPolicyName;
		private static int _eventHubSasTokenExpirationDays;
		private static string _storageAccountName;
		private static string _storageContainerName;
		private static string _adventureWorksResourceGroupName;
		private static string _adventureWorksBacpacUri;

		private static AttendeeInfo[] _attendees;
		private static SubscriptionData _subscriptionData;
		private static ResourceGroupResource _resourceGroup;
		private static ResourceGroupResource _adventureWorksResourceGroup;

		static async Task Main()
		{
			DisplayHeading();

			if (!await InitializeApplication())
			{
				return;
			}

			Console.Clear();

			var action = default(string);
			do
			{
				DisplayHeading();
				Console.WriteLine();
				Console.ForegroundColor = ConsoleColor.White;
				Console.WriteLine("Choose an action");
				Console.ResetColor();
				Console.WriteLine($"  V = View configuration");
				Console.WriteLine($"  A = Show attendee list");
				Console.WriteLine($"  L = List all attendee resources");
				Console.WriteLine($"  C = Create attendee resources");
				Console.WriteLine($"  D = Delete attendee resources");
				Console.WriteLine($"  E = Email attendees");
				Console.WriteLine($"  Q = Quit");
				Console.WriteLine();
				Console.Write("Enter choice: ");

				var input = Console.ReadLine()?.Trim();
				var tokens = input?.Split([' '], 2, StringSplitOptions.RemoveEmptyEntries) ?? [];
				action = tokens.ElementAtOrDefault(0)?.ToUpperInvariant();
				var attendee = tokens.ElementAtOrDefault(1); // attendee name if provided, or null attendee name for all attendees

				switch (action)
				{
					case "V":
						ViewConfiguration();
						break;

					case "A":
						ShowAttendeeList();
						break;

					case "L":
						await ListResources();
						break;

					case "C":
						await CreateResources(string.IsNullOrWhiteSpace(attendee) ? null : attendee);
						break;

					case "D":
						await DeleteResources(string.IsNullOrWhiteSpace(attendee) ? null : attendee);
						break;

					case "E":
						await EmailAttendees(string.IsNullOrWhiteSpace(attendee) ? null : attendee);
						break;

					case "Q":
						Console.WriteLine("Exiting...");
						break;

					default:
						Console.WriteLine("Invalid input.");
						break;
				}
				Console.WriteLine();
				Console.Write("Press any key to continue... ");
				Console.ReadKey(intercept: true);
				Console.Clear();

			} while (action != "Q");
		}

		private static void DisplayHeading()
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"Azure SQL Database Workshop for Developers - Lab Resource Manager");
			Console.ResetColor();
		}

		private static async Task<bool> InitializeApplication()
		{
			Console.WriteLine();
			Console.WriteLine("Initializing...");

			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var attendeesFilePath = Path.Combine(currentDir, "Attendees.csv");

			if (!File.Exists(attendeesFilePath))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Attendee list file not found at '{attendeesFilePath}'");
				Console.ResetColor();

				return false;
			}

			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build();

			_resourceGroupName = config["ResourceGroupName"];
			_resourceRegionName = config["ResourceRegionName"];
			_sqlDatabaseServerName = config["SqlDatabase:ServerName"];
			_sqlDatabaseUsername = config["SqlDatabase:Username"];
			_sqlDatabasePassword = config["SqlDatabase:Password"];
			_eventHubNamespaceName = config["EventHub:NamespaceName"];
			_eventHubName = config["EventHub:EventHubName"];
			_eventHubPolicyName = config["EventHub:PolicyName"];
			_eventHubSasTokenExpirationDays = int.Parse(config["EventHub:SasTokenExpirationDays"]);
			_storageAccountName = config["Storage:AccountName"];
			_storageContainerName = config["Storage:ContainerName"];
			_adventureWorksResourceGroupName = config["AdventureWorks:ResourceGroupName"];
			_adventureWorksBacpacUri = config["AdventureWorks:BacpacUri"];

			_attendees = File.ReadAllLines(attendeesFilePath) 
				.Select(line => line.Trim())
				.Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
				.Select(line => 
				{
					var parts = line.Split(',', 2);
					var name = parts[0].Trim();
					var email = parts.Length > 1 ? parts[1].Trim() : string.Empty;
					return new AttendeeInfo(name, email);
				})
				.ToArray();

			var credential = new AzureCliCredential();      // requires that you first run `az login` in a terminal
			var armClient = new ArmClient(credential);

			try
			{
				var subscription = await armClient.GetDefaultSubscriptionAsync();
				_subscriptionData = subscription.Data;

				_resourceGroup = await subscription.GetResourceGroups().GetAsync(_resourceGroupName);
				_adventureWorksResourceGroup = await subscription.GetResourceGroups().GetAsync(_adventureWorksResourceGroupName);
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Could not retrieve Azure resource information.");
				Console.WriteLine(ex.Message);
				Console.ResetColor();

				return false;
			}

			ViewConfiguration();
			return true;
		}

		private static void ViewConfiguration()
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("Subscription Information");
			Console.ResetColor();
			Console.WriteLine($"  Subscription Name     {_subscriptionData.DisplayName}");
			Console.WriteLine($"  Subscription ID       {_subscriptionData.SubscriptionId}");
			Console.WriteLine($"  Tenant ID             {_subscriptionData.TenantId}");
			Console.WriteLine($"  Resource Group Name   {_resourceGroupName}");
			Console.WriteLine($"  Resource Region Name  {_resourceRegionName}");
			Console.ResetColor();
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"SQL Database");
			Console.ResetColor();
			Console.WriteLine($"  Server Name           {_sqlDatabaseServerName}");
			Console.WriteLine($"  Username              {_sqlDatabaseUsername}");
			Console.WriteLine($"  Password              {_sqlDatabasePassword}");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Event Hub");
			Console.ResetColor();
			Console.WriteLine($"  Namespace Name        {_eventHubNamespaceName}");
			Console.WriteLine($"  Event Hub Name        {_eventHubName}");
			Console.WriteLine($"  Policy Name           {_eventHubPolicyName}");
			Console.WriteLine($"  SAS Token Expiration  {_eventHubSasTokenExpirationDays} day(s)");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Storage");
			Console.ResetColor();
			Console.WriteLine($"  Account Name          {_storageAccountName}");
			Console.WriteLine($"  Container Name        {_storageContainerName}");
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"AdventureWorks");
			Console.ResetColor();
			Console.WriteLine($"  Resource Group Name   {_adventureWorksResourceGroupName}");
			Console.WriteLine($"  Bacpac URI            {_adventureWorksBacpacUri}");
			Console.WriteLine();
		}

		private static void ShowAttendeeList()
		{
			Console.WriteLine();
			Console.WriteLine("Attendee list:");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Green;
			foreach (var attendee in _attendees)
			{
				Console.WriteLine(attendee);
			}
			Console.ResetColor();

			Console.WriteLine();
			Console.WriteLine($"Total Attendees: {_attendees.Length}");
		}

		private static async Task ListResources()
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Attendee resources in resource group '{_resourceGroupName}':");
			Console.ResetColor();

			var counter = 0;

			Console.ForegroundColor = ConsoleColor.Green;

			await foreach (var sqlServer in _resourceGroup.GetSqlServers().GetAllAsync())
			{
				Console.WriteLine($"{++counter,3}. SQL database server: {sqlServer.Data.Name}");
			}

			await foreach (var eventHubsNamespace in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				Console.WriteLine($"{++counter,3}. Event hub namespace: {eventHubsNamespace.Data.Name}");
			}

			await foreach (var storage in _resourceGroup.GetStorageAccounts().GetAllAsync())
			{
				Console.WriteLine($"{++counter,3}. Storage account: {storage.Data.Name}");
			}

			Console.ResetColor();

			Console.WriteLine();
			Console.WriteLine($"Total attendee resources: {counter}");
		}

		private static async Task CreateResources(string attendeeName = null)
		{
			var attendees = attendeeName == null ? _attendees : [new AttendeeInfo(attendeeName)];

			if (!ConfirmYesNo($"Are you sure you want to create resources for {attendees.Length} attendee(s)?"))
			{
				return;
			}

			var created = 0;
			var outputLines = new List<string> { "AttendeeName,EmailAddress,SqlDatabaseServerName,EventHubNamespaceName,EventHubSasToken,StorageAccountConnectionString" };
			var outputLock = new object();
			var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDop };

			var started = DateTime.Now;
			var counter = 0;

			Console.ForegroundColor = ConsoleColor.Green;

			await Parallel.ForEachAsync(attendees, options, async (attendee, cancellationToken) =>
			{
				try
				{
					var sqlDatabaseTask = CreateSqlDatabaseResources(attendee, Interlocked.Increment(ref counter), cancellationToken);
					var eventHubTask = CreateEventHubResources(attendee, Interlocked.Increment(ref counter), cancellationToken);
					var storageAccountTask = CreateStorageAccountResources(attendee, Interlocked.Increment(ref counter), cancellationToken);

					await Task.WhenAll(sqlDatabaseTask, eventHubTask, storageAccountTask);

					lock (outputLock)
					{
						outputLines.Add($"{attendee.AttendeeName},{attendee.EmailAddress},{attendee.SqlDatabaseServerName},{attendee.EventHubNamespaceName},{attendee.EventHubSasToken},{attendee.StorageAccountConnectionString}");
					}

					Interlocked.Increment(ref created);
				}
				catch (Exception ex)
				{
					var current = Interlocked.Increment(ref counter);
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"{current,3}. Error creating resources for attendee '{attendee.AttendeeName}': {ex.Message}");
					Console.ResetColor();
				}
			});

			Console.ResetColor();

			var elapsed = DateTime.Now.Subtract(started);

			var sortedLines = outputLines
				.Skip(1)
				.OrderBy(line => line.Split(',')[0])
				.Prepend(outputLines[0])
				.ToList();

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			var lineCounter = 0;
			foreach (var line in sortedLines)
			{
				Console.WriteLine($"{++lineCounter,3}. {line}");
			}
			Console.ResetColor();
			Console.WriteLine();

			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var outputPath = Path.Combine(currentDir, "AttendeeResources.csv");
			File.WriteAllLines(outputPath, sortedLines);

			Console.WriteLine($"\nProcessed {attendees.Length} attendee(s); successfully created resources for {created} attendee(s) in {elapsed}");
			Console.WriteLine($"Generated {outputPath}");
		}

		private static async Task CreateSqlDatabaseResources(AttendeeInfo attendee, int counter, CancellationToken cancellationToken)
		{
			var serverName = $"{_sqlDatabaseServerName}-{attendee.AttendeeName}";
			attendee.SqlDatabaseServerName = serverName;

			var sqlServerCollection = _resourceGroup.GetSqlServers();

			if (await sqlServerCollection.ExistsAsync(serverName, expand: null, cancellationToken))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"{counter,3}. Skipping SQL database server: {serverName} (already exists)");
				Console.ForegroundColor = ConsoleColor.Green;

				return;
			}

			Console.WriteLine($"{counter,3}. Creating SQL database server: {serverName}");

			var serverData = new SqlServerData(new AzureLocation(_resourceRegionName))
			{
				AdministratorLogin = _sqlDatabaseUsername,
				AdministratorLoginPassword = _sqlDatabasePassword,
				MinTlsVersion = SqlMinimalTlsVersion.Tls1_2,
				PublicNetworkAccess = ServerNetworkAccessFlag.Enabled,
			};

			var serverOperation = await sqlServerCollection.CreateOrUpdateAsync(
				WaitUntil.Completed,
				serverName,
				serverData,
				cancellationToken);

			var serverResource = serverOperation.Value;

			var firewallRules = serverResource.GetSqlFirewallRules();

			await firewallRules.CreateOrUpdateAsync(
				WaitUntil.Completed,
				firewallRuleName: "WideOpen",
				new SqlFirewallRuleData
				{
					StartIPAddress = "0.0.0.0",
					EndIPAddress = "255.255.255.255"
				},
				cancellationToken);

			await ImportAdventureWorks2022(attendee, counter, serverResource, cancellationToken);
		}

		private static async Task ImportAdventureWorks2022(AttendeeInfo attendeeInfo, int counter, SqlServerResource serverResource, CancellationToken cancellationToken)
		{
			Console.WriteLine($"    {counter,3}. Creating SQL database AdventureWorks2022 on server {attendeeInfo.SqlDatabaseServerName}");

			var databases = serverResource.GetSqlDatabases();
			var databaseData = new SqlDatabaseData(new AzureLocation(_resourceRegionName)) { Sku = new SqlSku("GP_S_Gen5_2") };
			var createOperation = await databases.CreateOrUpdateAsync(WaitUntil.Completed, "AdventureWorks2022", databaseData, cancellationToken);
			var database = createOperation.Value;
			var storageAccount = (await _adventureWorksResourceGroup.GetStorageAccounts().GetAsync("lennidemo", expand: null, cancellationToken)).Value;
			var storageAccountKey = await GetStorageAccountKey(storageAccount, cancellationToken);
			var bacpacUri = new Uri(_adventureWorksBacpacUri);

			Console.WriteLine($"    {counter,3}. Importing AdventureWorks2022.bacpac to server {attendeeInfo.SqlDatabaseServerName} from {bacpacUri}");

			var importDefinition = new ImportExistingDatabaseDefinition(StorageKeyType.StorageAccessKey, storageAccountKey, bacpacUri, _sqlDatabaseUsername, _sqlDatabasePassword);

			await database.ImportAsync(WaitUntil.Started, importDefinition, cancellationToken);	// let the import operation execute asynchronously
		}

		private static async Task CreateEventHubResources(AttendeeInfo attendeeInfo, int counter, CancellationToken cancellationToken)
		{
			var attendeeName = attendeeInfo.AttendeeName;

			var eventHubNamespaceName = $"{_eventHubNamespaceName}-{attendeeName}";
			attendeeInfo.EventHubNamespaceName = eventHubNamespaceName;

			var eventHubNamespaceCollection = _resourceGroup.GetEventHubsNamespaces();

			if (await eventHubNamespaceCollection.ExistsAsync(eventHubNamespaceName, cancellationToken))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"{counter,3}. Skipping event hub namespace: {eventHubNamespaceName} (already exists)");
				Console.ForegroundColor = ConsoleColor.Green;

				attendeeInfo.EventHubSasToken = await GenerateEventHubSasTokenAsync(eventHubNamespaceName);
				return;
			}

			Console.WriteLine($"{counter,3}. Creating event hub namespace: {eventHubNamespaceName}");

			// Create event hubs namespace

			var eventHubNamespaceData = new EventHubsNamespaceData(_resourceRegionName)
			{
				Sku = new EventHubsSku(EventHubsSkuName.Basic)
			};

			var eventHubNamespaceResource = await eventHubNamespaceCollection.CreateOrUpdateAsync(WaitUntil.Completed, eventHubNamespaceName, eventHubNamespaceData, cancellationToken);

			// Create event hub

			var eventHubCollection = eventHubNamespaceResource.Value.GetEventHubs();

			var eventHubData = new EventHubData
			{
				RetentionDescription = new RetentionDescription
				{
					CleanupPolicy = CleanupPolicyRetentionDescription.Delete,
					RetentionTimeInHours = 1
				},
			};

			var eventHub = await eventHubCollection.CreateOrUpdateAsync(WaitUntil.Completed, _eventHubName, eventHubData, cancellationToken);

			// Create event hub authorization rule (policy)

			var eventHubRuleData = new EventHubsAuthorizationRuleData
			{
				Rights =
					{
						EventHubsAccessRight.Manage,
						EventHubsAccessRight.Listen,
						EventHubsAccessRight.Send,
					}
			};

			var eventHubAuthorizationRules = eventHub.Value.GetEventHubAuthorizationRules();

			await eventHubAuthorizationRules.CreateOrUpdateAsync(WaitUntil.Completed, _eventHubPolicyName, eventHubRuleData, cancellationToken);

			// Generate SAS token

			attendeeInfo.EventHubSasToken = await GenerateEventHubSasTokenAsync(eventHubNamespaceName);
		}

		private static async Task<string> GenerateEventHubSasTokenAsync(string namespaceName)
		{
			// Build the full URI to the Event Hub within the namespace
			var resourceUri = $"https://{namespaceName}.servicebus.windows.net/{_eventHubName}";
			var encodedUri = WebUtility.UrlEncode(resourceUri);

			// Calculate expiry time in seconds since epoch
			var expiry = (int)(DateTime.UtcNow.AddDays(_eventHubSasTokenExpirationDays) - new DateTime(1970, 1, 1)).TotalSeconds;
			var stringToSign = $"{encodedUri}\n{expiry}";

			// Get the EventHub resource
			var namespaceResource = await _resourceGroup.GetEventHubsNamespaces().GetAsync(namespaceName);
			var eventHub = await namespaceResource.Value.GetEventHubs().GetAsync(_eventHubName);
			var policy = await eventHub.Value.GetEventHubAuthorizationRules().GetAsync(_eventHubPolicyName);
			var key = (await policy.Value.GetKeysAsync()).Value.PrimaryKey;

			if (string.IsNullOrEmpty(key))
			{
				throw new InvalidOperationException($"Primary key for policy '{_eventHubPolicyName}' is null or empty.");
			}

			using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
			var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
			var encodedSig = WebUtility.UrlEncode(signature);

			return $"SharedAccessSignature sr={encodedUri}&sig={encodedSig}&se={expiry}&skn={_eventHubPolicyName}";
		}

		private static async Task CreateStorageAccountResources(AttendeeInfo attendeeInfo, int counter, CancellationToken cancellationToken)
		{
			var attendeeName = attendeeInfo.AttendeeName;
			var storageAccountName = BuildStorageAccountName(attendeeName);
			var storageAccounts = _resourceGroup.GetStorageAccounts();
			var storageAccount = default(StorageAccountResource);

			if (await storageAccounts.ExistsAsync(storageAccountName, expand: null, cancellationToken))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"{counter,3}. Skipping storage account: {storageAccountName} (already exists)");
				Console.ForegroundColor = ConsoleColor.Green;

				storageAccount = (await storageAccounts.GetAsync(storageAccountName, expand: null, cancellationToken)).Value;
			}
			else
			{
				Console.WriteLine($"{counter,3}. Creating storage account: {storageAccountName}");

				var storageData = new StorageAccountCreateOrUpdateContent(
					new StorageSku(StorageSkuName.StandardLrs),
					StorageKind.StorageV2,
					new AzureLocation(_resourceRegionName))
				{
					EnableHttpsTrafficOnly = true,
					MinimumTlsVersion = StorageMinimumTlsVersion.Tls1_2,
					AllowBlobPublicAccess = false,
					AccessTier = StorageAccountAccessTier.Hot,
				};

				var createOperation = await storageAccounts.CreateOrUpdateAsync(
					WaitUntil.Completed,
					storageAccountName,
					storageData,
					cancellationToken);

				storageAccount = createOperation.Value;

				var blobService = (await storageAccount.GetBlobService().GetAsync(cancellationToken)).Value;
				var containers = blobService.GetBlobContainers();

				await containers.CreateOrUpdateAsync(
					WaitUntil.Completed,
					_storageContainerName,
					new BlobContainerData { PublicAccess = StoragePublicAccessType.None },
					cancellationToken);
			}

			var accountKey = await GetStorageAccountKey(storageAccount, cancellationToken);

			attendeeInfo.StorageAccountConnectionString =
				$"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={accountKey};EndpointSuffix=core.windows.net";
		}

		private static string BuildStorageAccountName(string attendeeName)
		{
			var rawStorageAccountName = $"{_storageAccountName}-{attendeeName}";
			var safeStorageAccountName = new string(rawStorageAccountName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
			var storageAccountName = safeStorageAccountName.Length > 24 ? safeStorageAccountName.Substring(0, 24) : safeStorageAccountName;

			return storageAccountName;
		}

		private static async Task<string> GetStorageAccountKey(StorageAccountResource storageAccount, CancellationToken cancellationToken)
		{
			var storageAccountKey = default(string);
			await foreach (var key in storageAccount.GetKeysAsync(expand: null, cancellationToken))
			{
				if (string.Equals(key.KeyName, "key1", StringComparison.OrdinalIgnoreCase))
				{
					storageAccountKey = key.Value;
					break;
				}
			}

			return storageAccountKey;
		}

		private static async Task DeleteResources(string attendeeName = null)
		{
			var sqlDatabaseServersToDelete = new List<SqlServerResource>();
			await foreach (var sqlDatabaseServer in _resourceGroup.GetSqlServers().GetAllAsync())
			{
				if (attendeeName == null)
				{
					sqlDatabaseServersToDelete.Add(sqlDatabaseServer);
				}
				else
				{
					var targetName = $"{_sqlDatabaseServerName}-{attendeeName}";
					if (sqlDatabaseServer.Data.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
					{
						sqlDatabaseServersToDelete.Add(sqlDatabaseServer);
					}
				}
			}

			var eventHubNamespacesToDelete = new List<EventHubsNamespaceResource>();
			await foreach (var eventHubNamespace in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				if (attendeeName == null)
				{
					eventHubNamespacesToDelete.Add(eventHubNamespace);
				}
				else
				{
					var targetName = $"{_eventHubNamespaceName}-{attendeeName}";
					if (eventHubNamespace.Data.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
					{
						eventHubNamespacesToDelete.Add(eventHubNamespace);
					}
				}
			}

			var storageAccountsToDelete = new List<StorageAccountResource>();
			await foreach (var storageAccount in _resourceGroup.GetStorageAccounts().GetAllAsync())
			{
				if (attendeeName == null)
				{
					storageAccountsToDelete.Add(storageAccount);
				}
				else
				{
					var targetName = BuildStorageAccountName(attendeeName);
					if (storageAccount.Data.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
					{
						storageAccountsToDelete.Add(storageAccount);
					}
				}
			}

			var totalDeletes = sqlDatabaseServersToDelete.Count + eventHubNamespacesToDelete.Count + storageAccountsToDelete.Count;
			if (totalDeletes == 0)
			{
				Console.WriteLine("\nNothing to delete.");
				return;
			}

			if (!ConfirmYesNo(
				$"Are you sure you want to delete {sqlDatabaseServersToDelete.Count} SQL database server(s), {eventHubNamespacesToDelete.Count} event hub namespace(s), {storageAccountsToDelete.Count} storage account(s) - total of {totalDeletes} resource(s)"))
			{
				return;
			}

			var operationCounter = 0;
			var sqlDatabaseServersDeletedCount = 0;
			var eventHubNamespacesDeletedCount = 0;
			var storageAccountsDeletedCount = 0;

			var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDop };
			var started = DateTime.Now;

			Console.ForegroundColor = ConsoleColor.Red;

			var sqlDatabaseTask = Parallel.ForEachAsync(sqlDatabaseServersToDelete, options, async (sql, ct) =>
			{
				var current = Interlocked.Increment(ref operationCounter);
				var name = sql.Data.Name;

				try
				{
					Console.WriteLine($"{current,3}. Deleting SQL database server: {name}");
					await sql.DeleteAsync(WaitUntil.Completed, ct);
					Interlocked.Increment(ref sqlDatabaseServersDeletedCount);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"{current,3}. Error deleting SQL database server {name}: {ex.Message}");
				}
			});

			var eventHubTask = Parallel.ForEachAsync(eventHubNamespacesToDelete, options, async (ns, ct) =>
			{
				var current = Interlocked.Increment(ref operationCounter);
				var name = ns.Data.Name;

				try
				{
					Console.WriteLine($"{current,3}. Deleting event hub namespace: {name}");
					await ns.DeleteAsync(WaitUntil.Completed, ct);
					Interlocked.Increment(ref eventHubNamespacesDeletedCount);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"{current,3}. Error deleting event hub namespace {name}: {ex.Message}");
				}
			});

			var storageAccountTask = Parallel.ForEachAsync(storageAccountsToDelete, options, async (sa, ct) =>
			{
				var current = Interlocked.Increment(ref operationCounter);
				var name = sa.Data.Name;

				try
				{
					Console.WriteLine($"{current,3}. Deleting storage account: {name}");
					await sa.DeleteAsync(WaitUntil.Completed, ct);
					Interlocked.Increment(ref storageAccountsDeletedCount);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"{current,3}. Error deleting storage account {name}: {ex.Message}");
				}
			});

			await Task.WhenAll(sqlDatabaseTask, eventHubTask, storageAccountTask);

			Console.ResetColor();

			var elapsed = DateTime.Now.Subtract(started);
			Console.WriteLine($"\nDeleted " +
				$"{sqlDatabaseServersDeletedCount}/{sqlDatabaseServersToDelete.Count} SQL database server(s), " +
				$"{eventHubNamespacesDeletedCount}/{eventHubNamespacesToDelete.Count} event hub namespace(s), " +
				$"{storageAccountsDeletedCount}/{storageAccountsToDelete.Count} storage account(s) " +
				$"(elapsed: {elapsed})");
		}

		private static async Task EmailAttendees(string attendeeName)
		{
			var attendees = attendeeName == null ? _attendees : [new AttendeeInfo(attendeeName)];

			if (!ConfirmYesNo($"Are you sure you want to create resources for {attendees.Length} attendee(s)?"))
			{
				return;
			}

			// GPT... read the AttendeeResources.csv file into an array of AttendeeInfo objects.
			// Then, for each attendee in the attendees array, find the matching AttendeeInfo object
			// Then, if the EmailAddress is not empty, send an email to the attendee with their resource information.
			// I want to use SMTP with my Outlook / Microsoft 365 email address lenni.lobel@sleektech.com.
			// For now, hardcode SMTP configuration, I will refactor later
		}

		private static bool ConfirmYesNo(string message)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write($"{message} (Y/N): ");
			Console.ResetColor();
			return Console.ReadLine().Trim().ToUpper() == "Y";
		}

	}

}
