using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.EventHubs.Models;
using Azure.ResourceManager.Resources;
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
	public class ProgramV1
	{
		private const int MaxDop = 6;	// Too large a number can result in diminishing returns, Azure rate limiting, and/or throttling with 429 (too many requests) errors

		private static string _resourceGroupName;
		private static string _sqlDatabaseServerName;
		private static string _eventHubNamespaceName;
		private static string _eventHubName;
		private static string _eventHubPolicyName;
		private static int _eventHubSasTokenExpirationDays;
		private static string _storageAccountName;
		private static string _storageContainerName;

		private static string[] _attendeeNames;
		private static SubscriptionData _subscriptionData;
		private static ResourceGroupResource _resourceGroup;

		static async Task MainV2()
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
			Console.WriteLine($"SQL Server 2025 Developer Workshop Lab Manager");
			Console.ResetColor();
		}

		private static async Task<bool> InitializeApplication()
		{
			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var attendeeNamesFilePath = Path.Combine(currentDir, "AttendeeNames.txt");

			if (!File.Exists(attendeeNamesFilePath))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"ERROR: Attendee list file not found at '{attendeeNamesFilePath}'");
				Console.ResetColor();
				return false;
			}

			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build();

			// Resource group
			_resourceGroupName = config["ResourceGroupName"];

			// SQL Database
			_sqlDatabaseServerName = config["SqlDatabase:ServerName"];

			// Event hub
			_eventHubNamespaceName = config["EventHub:NamespaceName"];
			_eventHubName = config["EventHub:EventHubName"];
			_eventHubPolicyName = config["EventHub:PolicyName"];
			_eventHubSasTokenExpirationDays = int.Parse(config["EventHub:SasTokenExpirationDays"]);

			// Storage
			_storageAccountName = config["Storage:AccountName"];
			_storageContainerName = config["Storage:ContainerName"];

			// Attendees
			_attendeeNames = File.ReadAllLines(attendeeNamesFilePath) 
				.Select(line => line.Trim())
				.Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
				.ToArray();

			var credential = new AzureCliCredential();      // requires that you first run `az login` in a terminal
			var armClient = new ArmClient(credential);

			try
			{
				var subscription = await armClient.GetDefaultSubscriptionAsync();
				_subscriptionData = subscription.Data;

				_resourceGroup = await subscription.GetResourceGroups().GetAsync(_resourceGroupName);
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("ERROR: Could not retrieve Azure resource information.");
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
			Console.ResetColor();
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"SQL Database");
			Console.ResetColor();
			Console.WriteLine($"  Server Name           {_sqlDatabaseServerName}");
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
		}

		private static void ShowAttendeeList()
		{
			Console.WriteLine();
			Console.WriteLine("Attendee list:");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Green;
			foreach (var attendee in _attendeeNames)
			{
				Console.WriteLine(attendee);
			}
			Console.ResetColor();

			Console.WriteLine();
			Console.WriteLine($"Total Attendees: {_attendeeNames.Length}");
		}

		private static async Task ListResources()
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Attendee resource in resource group '{_resourceGroupName}':");
			Console.ResetColor();

			var counter = 0;

			await foreach (var eventHubsNamespace in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				Console.WriteLine($"{++counter,3}. Event hub namespace {eventHubsNamespace.Data.Name}");
			}

			Console.WriteLine($"\nTotal attendee event nub namespaces: {counter}");
		}

		private static async Task CreateResources(string attendeeName = null)
		{
			var attendeeNames = attendeeName == null ? _attendeeNames : [attendeeName];

			if (!ConfirmYesNo($"Are you sure you want to create resources for {attendeeNames.Length} attendee(s)?"))
			{
				return;
			}

			var counter = 0;
			var created = 0;
			var outputLines = new List<string> { "AttendeeName,SqlDatabaseServerName,EventHubNamespaceName,EventHubSasToken,StorageAccountConnectionString" };
			var outputLock = new object();
			var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDop };

			var started = DateTime.Now;
			Console.ForegroundColor = ConsoleColor.Green;
			await Parallel.ForEachAsync(attendeeNames, options, async (attendeeName, cancellationToken) =>
			{
				var currentCounter = Interlocked.Increment(ref counter);

				var attendeeInfo = new AttendeeInfo(attendeeName);

				await CreateSqlDatabaseServer(attendeeInfo, currentCounter, cancellationToken);
				await CreateEventHubResources(attendeeInfo, currentCounter, cancellationToken);

				lock (outputLock)
				{
					outputLines.Add($"{attendeeName},{attendeeInfo.SqlDatabaseServerName},{attendeeInfo.EventHubNamespaceName},{attendeeInfo.EventHubSasToken},{attendeeInfo.StorageAccountConnectionString}");
				}

				Interlocked.Increment(ref created);
			});
			Console.ResetColor();
			var elapsed = DateTime.Now.Subtract(started);

			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var outputPath = Path.Combine(currentDir, "AttendeeResources.csv");

			var sortedLines = outputLines
				.Skip(1)
				.OrderBy(line => line.Split(',')[0]) // Sort by attendee
				.Prepend(outputLines[0]) // Re-add header
				.ToList();

			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			counter = 0;
			foreach (var line in sortedLines)
			{
				Console.WriteLine($"{++counter,3}. {line}");
			}
			Console.ResetColor();
			Console.WriteLine();

			File.WriteAllLines(outputPath, sortedLines);
			Console.WriteLine($"\nCreated {created} attendee resource(s) in {elapsed}"); 
			Console.WriteLine($"Generated {outputPath}");
		}

		private static async Task CreateSqlDatabaseServer(AttendeeInfo attendeeInfo, int currentCounter, CancellationToken cancellationToken)
		{
			var attendeeName = attendeeInfo.AttendeeName;

			var sqlDatabaseServerName = $"{_sqlDatabaseServerName}-{attendeeName}";

			// GPT: Write the code to create a new Azure SQL Database server for the attendee in the _resourceGroup. Using a similar pattern to the existence check on the event hub resources in the method below, first check if the server name exists, and skip the creation if it does, otherwise, create the database server.

			attendeeInfo.SqlDatabaseServerName = sqlDatabaseServerName;
		}

		private static async Task CreateEventHubResources(AttendeeInfo attendeeInfo, int currentCounter, CancellationToken cancellationToken)
		{
			var attendeeName = attendeeInfo.AttendeeName;

			var eventHubNamespaceName = $"{_eventHubNamespaceName}-{attendeeName}";
			var eventHubNamespaceCollection = _resourceGroup.GetEventHubsNamespaces();

			if (await eventHubNamespaceCollection.ExistsAsync(eventHubNamespaceName, cancellationToken))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"{currentCounter,3}. Skipping event hub namespace: {eventHubNamespaceName} (namespace already exists)");
				Console.ForegroundColor = ConsoleColor.Green;

				return;
			}

			Console.WriteLine($"{currentCounter,3}. Creating event hub namespace: {eventHubNamespaceName}");

			// Create event hubs namespace

			var eventHubNamespaceData = new EventHubsNamespaceData("Central US")
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

			var eventHubSasToken = await GenerateEventHubSasTokenAsync(eventHubNamespaceName);

			// Capture attendee's event hub namespace name and SAS token
			attendeeInfo.EventHubNamespaceName = eventHubNamespaceName;
			attendeeInfo.EventHubSasToken = eventHubSasToken;
		}

		private static async Task DeleteResources(string attendee = null)
		{
			var namespaceResources = new List<EventHubsNamespaceResource>();
			await foreach (var namespaceResource in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				var attendeeSuffix = namespaceResource.Data.Name.Replace(_eventHubNamespaceName + "-", string.Empty);
				if (attendee == null || namespaceResource.Data.Name == $"{_eventHubNamespaceName}-{attendee}")
				{
					namespaceResources.Add(namespaceResource);
				}
			}

			if (!ConfirmYesNo($"Are you sure you want to delete {namespaceResources.Count} attendee resource(s)?"))
			{
				return;
			}

			var counter = 0;
			var options = new ParallelOptions { MaxDegreeOfParallelism = MaxDop };

			var started = DateTime.Now;
			Console.ForegroundColor = ConsoleColor.Red;
			await Parallel.ForEachAsync(namespaceResources, options, async (eventHubsNamespace, cancellationToken) =>
			{
				var namespaceName = eventHubsNamespace.Data.Name;
				var currentCounter = Interlocked.Increment(ref counter);

				Console.WriteLine($"{currentCounter,3}. Deleting event hub namespace: {namespaceName}");

				await eventHubsNamespace.DeleteAsync(WaitUntil.Completed, cancellationToken);
			});
			Console.ResetColor();
			var elapsed = DateTime.Now.Subtract(started);

			Console.WriteLine($"\nTotal namespaces deleted: {counter} (elapsed: {elapsed})");
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

		private static bool ConfirmYesNo(string message)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write($"{message} (Y/N): ");
			Console.ResetColor();
			return Console.ReadLine().Trim().ToUpper() == "Y";
		}

	}

}
