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

namespace CESLabManager
{
	public class Program
	{
		private static string _resourceGroupName;
		private static string _namespaceName;
		private static string _eventHubName;
		private static string _policyName;
		private static int _sasTokenExpirationDays;

		private static List<string> _attendees;
		private static SubscriptionData _subscriptionData;
		private static ResourceGroupResource _resourceGroup;

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
				Console.WriteLine($"  L = List all attendee namespaces");
				Console.WriteLine($"  C = Create attendee namespaces");
				Console.WriteLine($"  D = Delete attendee namespaces");
				Console.WriteLine($"  Q = Quit");
				Console.WriteLine();
				Console.Write("Enter choice: ");
				action = Console.ReadLine()?.Trim().ToUpperInvariant();

				try
				{
					switch (action)
					{
						case "V":
							ViewConfiguration();
							break;

						case "A":
							ShowAttendeeList();
							break;

						case "L":
							await ListNamespaces();
							break;

						case "C":
							await CreateNamespaces();
							break;

						case "D":
							await DeleteNamespaces();
							break;

						case "Q":
							Console.WriteLine("Exiting...");
							break;

						default:
							Console.WriteLine("Invalid input.");
							break;
					}
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Error: {ex.Message}");
					Console.ResetColor();
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
			Console.WriteLine($"Change Event Stream (CES) Lab Manager");
			Console.ResetColor();
		}

		private static async Task<bool> InitializeApplication()
		{
			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var attendeesFilePath = Path.Combine(currentDir, "Attendees.txt");

			if (!File.Exists(attendeesFilePath))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"ERROR: Attendee list file not found at '{attendeesFilePath}'");
				Console.ResetColor();
				return false;
			}

			var config = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.Build();

			_resourceGroupName = config["EventHub:ResourceGroupName"];
			_namespaceName = config["EventHub:NamespaceName"];
			_eventHubName = config["EventHub:EventHubName"];
			_policyName = config["EventHub:PolicyName"];
			_sasTokenExpirationDays = int.Parse(config["EventHub:SasTokenExpirationDays"]);

			_attendees = File.ReadAllLines(attendeesFilePath) 
				.Select(line => line.Trim())
				.Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
				.ToList();

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
			Console.WriteLine("Azure Subscription Information");
			Console.ResetColor();
			Console.WriteLine($"  Subscription Name     {_subscriptionData.DisplayName}");
			Console.WriteLine($"  Subscription ID       {_subscriptionData.SubscriptionId}");
			Console.WriteLine($"  Tenant ID             {_subscriptionData.TenantId}");
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine();
			Console.WriteLine($"Azure Event Hubs Information");
			Console.ResetColor();
			Console.WriteLine($"  Resource Group Name   {_resourceGroupName}");
			Console.WriteLine($"  Namespace Name        {_namespaceName}");
			Console.WriteLine($"  Event Hub Name        {_eventHubName}");
			Console.WriteLine($"  Policy Name           {_policyName}");
			Console.WriteLine($"  SAS Token Expiration  {_sasTokenExpirationDays} day(s)");
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
			Console.WriteLine($"Total Attendees: {_attendees.Count}");
		}

		private static async Task ListNamespaces()
		{
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Event Hub namespaces in resource group '{_resourceGroupName}':");
			Console.ResetColor();

			var counter = 0;

			await foreach (var eventHubsNamespace in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				Console.WriteLine($"{++counter,3}. {eventHubsNamespace.Data.Name}");
			}
		}

		private static async Task CreateNamespaces()
		{
			if (!ConfirmYesNo("Are you sure you want to create namespaces for all attendees?"))
			{
				return;
			}

			var counter = 0;
			var skipped = 0;
			var created = 0;
			var outputLines = new List<string> { "Attendee,NamespaceName,SasToken" };
			var outputLock = new object();

			var options = new ParallelOptions
			{
				MaxDegreeOfParallelism = 5 // Too large a number can overwhelm Azure
			};

			Console.ForegroundColor = ConsoleColor.Green;
			await Parallel.ForEachAsync(_attendees, options, async (attendee, cancellationToken) =>
			{
				var currentCounter = Interlocked.Increment(ref counter);

				var namespaceName = $"{_namespaceName}-{attendee}";
				var namespaceCollection = _resourceGroup.GetEventHubsNamespaces();

				if (await namespaceCollection.ExistsAsync(namespaceName, cancellationToken))
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"{currentCounter,3}. {attendee} - skipped (namespace already exists)");
					Console.ForegroundColor = ConsoleColor.Green;
					Interlocked.Increment(ref skipped);
					return;
				}

				Console.WriteLine($"{currentCounter,3}. {attendee}");

				// Create event hubs namespace

				var namespaceData = new EventHubsNamespaceData("Central US")
				{
					Sku = new EventHubsSku(EventHubsSkuName.Basic)
				};

				var namespaceResource = await namespaceCollection.CreateOrUpdateAsync(WaitUntil.Completed, namespaceName, namespaceData, cancellationToken);

				// Create event hub

				var eventHubCollection = namespaceResource.Value.GetEventHubs();

				var eventHubData = new EventHubData
				{
					RetentionDescription = new RetentionDescription
					{
						CleanupPolicy = CleanupPolicyRetentionDescription.Delete,
						RetentionTimeInHours = 1
					},
				};

				await eventHubCollection.CreateOrUpdateAsync(WaitUntil.Completed, _eventHubName, eventHubData, cancellationToken);

				// Create event hub

				var eventHub = await eventHubCollection.GetAsync(_eventHubName, cancellationToken);

				var eventHubRuleData = new EventHubsAuthorizationRuleData
				{
					Rights =
						{
							EventHubsAccessRight.Manage,
							EventHubsAccessRight.Listen,
							EventHubsAccessRight.Send,
						}
				};

				var authRules = eventHub.Value.GetEventHubAuthorizationRules();

				await authRules.CreateOrUpdateAsync(WaitUntil.Completed, _policyName, eventHubRuleData, cancellationToken);

				var token = await GenerateSasTokenAsync(namespaceName);

				lock (outputLock)
				{
					outputLines.Add($"{attendee},{namespaceName},{token}");
				}

				Interlocked.Increment(ref created);
			});
			Console.ResetColor();

			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var outputPath = Path.Combine(currentDir, "AttendeeNamespaces.csv");

			var sortedLines = outputLines
				.Skip(1)
				.OrderBy(line => line.Split(',')[0]) // Sort by attendee
				.Prepend(outputLines[0]) // Re-add header
				.ToList();

			File.WriteAllLines(outputPath, sortedLines);
			Console.WriteLine($"\nTotal namespaces created: {created}, skipped: {skipped}");
			Console.WriteLine($"Generated {outputPath}");
		}

		private static async Task DeleteNamespaces()
		{
			if (!ConfirmYesNo("Are you sure you want to delete all attendee namespaces?"))
			{
				return;
			}

			var counter = 0;

			Console.ForegroundColor = ConsoleColor.Red;
			await foreach (var eventHubsNamespace in _resourceGroup.GetEventHubsNamespaces().GetAllAsync())
			{
				var namespaceName = eventHubsNamespace.Data.Name;
				Console.WriteLine($"{++counter,3}. {namespaceName}");

				var namespaceCollection = _resourceGroup.GetEventHubsNamespaces();

				await namespaceCollection.Get(namespaceName).Value.DeleteAsync(WaitUntil.Completed);
			}

			Console.ResetColor();
		}

		private static async Task<string> GenerateSasTokenAsync(string namespaceName)
		{
			// Build the full URI to the Event Hub within the namespace
			var resourceUri = $"https://{namespaceName}.servicebus.windows.net/{_eventHubName}";
			var encodedUri = WebUtility.UrlEncode(resourceUri);

			// Calculate expiry time in seconds since epoch
			var expiry = (int)(DateTime.UtcNow.AddDays(_sasTokenExpirationDays) - new DateTime(1970, 1, 1)).TotalSeconds;
			var stringToSign = $"{encodedUri}\n{expiry}";

			// Get the EventHub resource
			var namespaceResource = await _resourceGroup.GetEventHubsNamespaces().GetAsync(namespaceName);
			var eventHub = await namespaceResource.Value.GetEventHubs().GetAsync(_eventHubName);
			var policy = await eventHub.Value.GetEventHubAuthorizationRules().GetAsync(_policyName);
			var key = (await policy.Value.GetKeysAsync()).Value.PrimaryKey;

			if (string.IsNullOrEmpty(key))
			{
				throw new InvalidOperationException($"Primary key for policy '{_policyName}' is null or empty.");
			}

			using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
			var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
			var encodedSig = WebUtility.UrlEncode(signature);

			return $"SharedAccessSignature sr={encodedUri}&sig={encodedSig}&se={expiry}&skn={_policyName}";
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
