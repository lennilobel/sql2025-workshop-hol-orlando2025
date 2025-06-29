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

		private static List<string> _students;
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
				Console.WriteLine($"  S = Show student list");
				Console.WriteLine($"  L = List all student namespaces");
				Console.WriteLine($"  C = Create student namespaces");
				Console.WriteLine($"  D = Delete student namespaces");
				Console.WriteLine($"  G = Generate SAS access token");
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

						case "S":
							ShowStudentList();
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

						case "G":
							await GenerateAllSasTokens();
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
			var studentsFilePath = Path.Combine(currentDir, "Students.txt");

			if (!File.Exists(studentsFilePath))
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"ERROR: Student list file not found at '{studentsFilePath}'");
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

			_students = File.ReadAllLines(studentsFilePath) 
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
			Console.WriteLine($"  Subscription Name     : {_subscriptionData.DisplayName}");
			Console.WriteLine($"  Subscription ID       : {_subscriptionData.SubscriptionId}");
			Console.WriteLine($"  Tenant ID             : {_subscriptionData.TenantId}");
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine();
			Console.WriteLine($"Azure Event Hubs Information");
			Console.ResetColor();
			Console.WriteLine($"  Resource Group Name   : {_resourceGroupName}");
			Console.WriteLine($"  Namespace Name        : {_namespaceName}");
			Console.WriteLine($"  Event Hub Name        : {_eventHubName}");
			Console.WriteLine($"  Policy Name           : {_policyName}");
			Console.WriteLine($"  SAS Token Expiration  : {_sasTokenExpirationDays} day(s)");
			Console.WriteLine();
		}

		private static void ShowStudentList()
		{
			Console.WriteLine();
			Console.WriteLine("Student list (used to name consumer groups):");
			Console.WriteLine();

			Console.ForegroundColor = ConsoleColor.Green;
			foreach (var student in _students)
			{
				Console.WriteLine(student);
			}

			Console.ResetColor();
			Console.WriteLine();
			Console.WriteLine($"Total Students: {_students.Count}");
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
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write("Are you sure you want to create namespaces for all students? (Y/N): ");
			Console.ResetColor();
			if (Console.ReadLine().Trim().ToUpper() != "Y")
			{
				return;
			}

			var skipped = 0;
			var created = 0;
			var outputLines = new List<string> { "Student,NamespaceName,SasToken" };

			Console.ForegroundColor = ConsoleColor.Green;
			foreach (var student in _students)
			{
				Console.WriteLine();
				Console.WriteLine($"Student: {student}");

				var namespaceName = $"{_namespaceName}-{student}";
				var namespaceCollection = _resourceGroup.GetEventHubsNamespaces();

				if (await namespaceCollection.ExistsAsync(namespaceName))
				{
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine($"Skipped (already exists): {namespaceName}");
					skipped++;
					continue;
				}

				Console.WriteLine($"  Creating event hub namespace: {namespaceName}");

				var namespaceData = new EventHubsNamespaceData("Central US");
				//{
				//	Sku = new EventHubsSku(EventHubsSkuName.Basic)
				//};
				var namespaceResource = await namespaceCollection.CreateOrUpdateAsync(WaitUntil.Completed, namespaceName, namespaceData);

				Console.WriteLine($"  Creating event hub: {_eventHubName}");

				var eventHubCollection = namespaceResource.Value.GetEventHubs();
				await eventHubCollection.CreateOrUpdateAsync(
					WaitUntil.Completed,
					_eventHubName,
					new EventHubData
					{
						RetentionDescription = new RetentionDescription
						{
							CleanupPolicy = CleanupPolicyRetentionDescription.Delete,
							RetentionTimeInHours = 1
						},
					});
				 
				var eventHub = await namespaceResource.Value.GetEventHubs().GetAsync(_eventHubName);

				Console.WriteLine($"  Creating event policy: {_policyName}");

				var authRules = eventHub.Value.GetEventHubAuthorizationRules();
				await authRules.CreateOrUpdateAsync(
					WaitUntil.Completed,
					_policyName,
					new EventHubsAuthorizationRuleData
					{
						Rights =
						{
							EventHubsAccessRight.Manage,
 							EventHubsAccessRight.Listen,
							EventHubsAccessRight.Send,
						}
					});

				var token = await GenerateSasTokenAsync(namespaceName);
				outputLines.Add($"{student},{namespaceName},{token}");
				created++;
			}
			Console.ResetColor();

			var currentDir = AppContext.BaseDirectory + "\\..\\..\\..";
			var outputPath = Path.Combine(currentDir, "StudentNamespaces.csv");

			File.WriteAllLines(outputPath, outputLines);
			Console.WriteLine($"\nTotal Namespaces Created: {created}, Skipped: {skipped}");
			Console.WriteLine($"Generated {outputPath}");
		}

		private static async Task DeleteNamespaces()
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.Write("Are you sure you want to delete all student namespaces? (Y/N): ");
			Console.ResetColor();
			if (Console.ReadLine().Trim().ToUpper() != "Y")
			{
				return;
			}

			var count = 0;
			foreach (var student in _students)
			{
				var nsName = $"{_namespaceName}-{student}";
				var nsCollection = _resourceGroup.GetEventHubsNamespaces();
				if (await nsCollection.ExistsAsync(nsName))
				{
					Console.WriteLine($"Deleting: {nsName}");
					await nsCollection.Get(nsName).Value.DeleteAsync(WaitUntil.Completed);
					count++;
				}
				else
				{
					Console.WriteLine($"Skipped (not found): {nsName}");
				}
			}
			Console.WriteLine($"Total Namespaces Deleted: {count}");
		}

		private static async Task GenerateAllSasTokens()
		{
			foreach (var student in _students)
			{
				var nsName = $"{_namespaceName}-{student}";
				var token = await GenerateSasTokenAsync(nsName);
				Console.WriteLine($"{student}: {token}\n");
			}
		}

		private static async Task<string> GenerateSasTokenAsync(string nsName)
		{
			// Build the full URI to the Event Hub within the namespace
			var resourceUri = $"https://{nsName}.servicebus.windows.net/{_eventHubName}";
			var encodedUri = WebUtility.UrlEncode(resourceUri);

			// Calculate expiry time in seconds since epoch
			var expiry = (int)(DateTime.UtcNow.AddDays(_sasTokenExpirationDays) - new DateTime(1970, 1, 1)).TotalSeconds;
			var stringToSign = $"{encodedUri}\n{expiry}";

			// Get the EventHub resource
			var ns = await _resourceGroup.GetEventHubsNamespaces().GetAsync(nsName);
			var eventHub = await ns.Value.GetEventHubs().GetAsync(_eventHubName);
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

	}
}
