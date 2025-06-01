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
using System.Threading.Tasks;

class Program
{
	private static SubscriptionData _subscriptionData;
	private static string _resourceGroupName;
	private static string _namespaceName;
	private static string _eventHubName;

	private static List<string> _students;
	private static EventHubsNamespaceResource _namespaceResource;
	private static EventHubResource _eventHubResource;

	static async Task Main()
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		_resourceGroupName = config["EventHub:ResourceGroupName"];
		_namespaceName = config["EventHub:NamespaceName"];
		_eventHubName = config["EventHub:EventHubName"];

		var currentDir = AppContext.BaseDirectory;
		var studentsFilePath = Path.Combine(currentDir, "Students.txt");

		if (!File.Exists(studentsFilePath))
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"ERROR: Student list file not found at '{studentsFilePath}'");
			return;
		}

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

			var resourceGroup = await subscription.GetResourceGroups().GetAsync(_resourceGroupName);
			_namespaceResource = await resourceGroup.Value.GetEventHubsNamespaces().GetAsync(_namespaceName);
			_eventHubResource = await _namespaceResource.GetEventHubs().GetAsync(_eventHubName);
		}
		catch
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("ERROR: Could not retrieve Azure resource information.");
			return;
		}

		ViewConfiguration();

		var action = default(string);
		do
		{
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"Change Event Stream (CES) Lab Manager");
			Console.WriteLine($"Current Tier: {_namespaceResource.Data.Sku.Name}");
			Console.ResetColor();
			Console.WriteLine();
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine("Choose an action");
			Console.ResetColor();
			Console.WriteLine("  V = View configuration");
			Console.WriteLine("  S = Show student list");
			Console.WriteLine("  L = List all consumer groups");
			Console.WriteLine("  C = Create student consumer groups");
			Console.WriteLine("  D = Delete student consumer groups");
			Console.WriteLine($"  T = Toggle pricing tier ({(_namespaceResource.Data.Sku.Name == "Standard" ? "Standard -> Basic" : "Basic -> Standard")})");
			Console.WriteLine("  Q = Quit");
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
						await ListConsumerGroups();
						break;

					case "C":
						await CreateConsumerGroups();
						break;

					case "D":
						await DeleteConsumerGroups();
						break;

					case "T":
						await ToggleSku();
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

	private static void ViewConfiguration()
	{
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine("Azure Subscription Information");
		Console.ResetColor();
		Console.WriteLine($"  Subscription Name    : {_subscriptionData.DisplayName}");
		Console.WriteLine($"  Subscription ID      : {_subscriptionData.SubscriptionId}");
		Console.WriteLine($"  Tenant ID            : {_subscriptionData.TenantId}");
		Console.ForegroundColor = ConsoleColor.White;
		Console.WriteLine();
		Console.WriteLine($"Azure Event Hubs Information");
		Console.ResetColor();
		Console.WriteLine($"  Resource Group Name  : {_resourceGroupName}");
		Console.WriteLine($"  Namespace Name       : {_namespaceName}");
		Console.WriteLine($"  Event Hub Name       : {_eventHubName}");
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

	private static async Task<List<string>> GetConsumerGroupNames()
	{
		var list = new List<string>();
		await foreach (var group in _eventHubResource.GetEventHubsConsumerGroups().GetAllAsync())
		{
			list.Add(group.Data.Name);
		}
		return list;
	}

	private static async Task ListConsumerGroups()
	{
		Console.WriteLine();
		Console.WriteLine($"Listing all consumer groups in '{_eventHubName}':");

		var groups = await GetConsumerGroupNames();

		Console.ForegroundColor = ConsoleColor.Green;
		foreach (var group in groups)
		{
			Console.WriteLine(group);
		}
		Console.ResetColor();

		Console.WriteLine();
		Console.WriteLine($"Total Consumer Groups: {groups.Count}");
	}

	private static async Task CreateConsumerGroups()
	{
		if (_namespaceResource.Data.Sku.Name == "Basic")
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Cannot create consumer groups for Basic SKU; first toggle the SKU to Standard");
			Console.ResetColor();
			return;
		}

		Console.WriteLine();
		Console.Write("Are you sure you want to create all student consumer groups? (Y/N): ");
		if (!Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var existing = await GetConsumerGroupNames();
		var count = 0;

		foreach (var student in _students)
		{
			var name = $"cg-{student}";
			if (existing.Contains(name))
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"Skipped (already exists): {name}");
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"Creating Consumer Group: {name}");
				try
				{
					await _eventHubResource.GetEventHubsConsumerGroups().CreateOrUpdateAsync(WaitUntil.Completed, name, new EventHubsConsumerGroupData());
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Error creating consumer group '{name}': {ex.Message}");
					continue;
				}
				count++;
			}
		}

		Console.ResetColor();
		Console.WriteLine();
		Console.WriteLine($"Total Consumer Groups Created: {count}");
	}

	private static async Task DeleteConsumerGroups()
	{
		Console.Write("Are you sure you want to delete all student consumer groups? (Y/N): ");
		if (!Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var existing = await GetConsumerGroupNames();
		var count = 0;

		foreach (var student in _students)
		{
			var name = $"cg-{student}";
			if (existing.Contains(name))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"Deleting Consumer Group: {name}");
				await _eventHubResource.GetEventHubsConsumerGroups().Get(name).Value.DeleteAsync(WaitUntil.Completed);
				count++;
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"Skipped (not found): {name}");
			}
		}

		Console.ResetColor();
		
		Console.WriteLine();
		Console.WriteLine($"Total Consumer Groups Deleted: {count}");
	}

	private static async Task ToggleSku()
	{
		var currentSku = _namespaceResource.Data.Sku.Name;

		Console.WriteLine();
		Console.Write($"Are you sure you want to toggle the SKU from {currentSku}? (Y/N): ");
		if (!Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		Console.WriteLine();
		Console.WriteLine($"Toggling pricing tier for Event Hub Namespace '{_namespaceName}' (Current Tier: {currentSku})");

		if (currentSku == "Standard")
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("Current tier is Standard. Preparing to downgrade to Basic...");

			await foreach (var group in _eventHubResource.GetEventHubsConsumerGroups().GetAllAsync())
			{
				if (group.Data.Name != "$Default")
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Deleting Consumer Group: {group.Data.Name}");
					await group.DeleteAsync(WaitUntil.Completed);
				}
			}

			Console.WriteLine("Updating tier to Basic...");

			var data = _namespaceResource.Data;
			data.Sku = new EventHubsSku("Basic");
			_namespaceResource = await _namespaceResource.UpdateAsync(data);
		}
		else if (currentSku == "Basic")
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Current tier is Basic. Upgrading to Standard...");

			var data = _namespaceResource.Data;
			data.Sku = new EventHubsSku("Standard");

			_namespaceResource = await _namespaceResource.UpdateAsync(data);
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Unsupported SKU type: {currentSku}");
		}

		_namespaceResource = await _namespaceResource.GetAsync(); // refresh
		Console.ResetColor();
		Console.WriteLine($"Updated pricing tier: {_namespaceResource.Data.Sku.Name}");
	}
}
