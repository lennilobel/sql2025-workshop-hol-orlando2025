using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.EventHubs;
using Azure.ResourceManager.EventHubs.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure;

class Program
{
	private const string ResourceGroup = "demos-rg";
	private const string NamespaceName = "sql2025-ces";
	private const string EventHubName = "ces-hub";
	private static List<string> Students = new();
	private static EventHubsNamespaceResource NamespaceResource;
	private static EventHubResource EventHubResource;

	static async Task Main()
	{
		var currentDir = AppContext.BaseDirectory;
		var studentsFilePath = Path.Combine(currentDir, "ManageEventHubConsumerGroups-students.txt");

		if (!File.Exists(studentsFilePath))
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"ERROR: Student list file not found at '{studentsFilePath}'");
			return;
		}

		Students = File.ReadAllLines(studentsFilePath)
			.Select(line => line.Trim())
			.Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
			.ToList();

		var armClient = new ArmClient(new DefaultAzureCredential());

		try
		{
			var subscription = await armClient.GetDefaultSubscriptionAsync();
			var resourceGroup = await subscription.GetResourceGroups().GetAsync(ResourceGroup);
			NamespaceResource = await resourceGroup.Value.GetEventHubsNamespaces().GetAsync(NamespaceName);
			EventHubResource = await NamespaceResource.GetEventHubs().GetAsync(EventHubName);
		}
		catch
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("ERROR: Could not retrieve Event Hub namespace. Check resource group and namespace name.");
			return;
		}

		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine($"Manage Event Hub Consumer Groups for HOL Students (Current Tier: {NamespaceResource.Data.Sku.Name})");

		string mode;
		do
		{
			Console.ResetColor();
			Console.WriteLine("\nChoose an action:");
			Console.WriteLine("  S = Show student list");
			Console.WriteLine("  L = List all consumer groups");
			Console.WriteLine("  C = Create student consumer groups");
			Console.WriteLine("  D = Delete student consumer groups");
			Console.WriteLine("  T = Toggle Event Hub pricing tier (Standard <-> Basic)");
			Console.WriteLine("  Q = Quit\n");

			Console.Write("Enter choice (S, L, C, D, T, Q): ");
			mode = Console.ReadLine()?.Trim().ToUpperInvariant();

			switch (mode)
			{
				case "S": ShowStudentList(); break;
				case "L": await ListConsumerGroups(); break;
				case "C": await CreateConsumerGroups(); break;
				case "D": await DeleteConsumerGroups(); break;
				case "T": await ToggleSku(); break;
				case "Q": Console.WriteLine("Exiting..."); break;
				default: Console.WriteLine("Invalid input."); break;
			}

		} while (mode != "Q");
	}

	static void ShowStudentList()
	{
		Console.WriteLine("\nStudent list (used to name consumer groups):\n");
		Students.ForEach(s => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine(s); });
		Console.ResetColor();
		Console.WriteLine($"\nTotal Students: {Students.Count}");
	}

	static async Task<List<string>> GetConsumerGroupNames()
	{
		var list = new List<string>();
		await foreach (var group in EventHubResource.GetEventHubConsumerGroups().GetAllAsync())
		{
			list.Add(group.Data.Name);
		}
		return list;
	}

	static async Task ListConsumerGroups()
	{
		Console.WriteLine($"\nListing all consumer groups in '{EventHubName}':\n");
		var groups = await GetConsumerGroupNames();
		groups.ForEach(g => { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine(g); });
		Console.ResetColor();
		Console.WriteLine($"\nTotal Consumer Groups: {groups.Count}");
	}

	static async Task CreateConsumerGroups()
	{
		Console.Write("\nAre you sure you want to create all student consumer groups? (Y/N): ");
		if (!Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase)) return;

		var existing = await GetConsumerGroupNames();
		int count = 0;

		foreach (var student in Students)
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
				await EventHubResource.GetEventHubConsumerGroups().CreateOrUpdateAsync(WaitUntil.Completed, name, new EventHubConsumerGroupData());
				count++;
			}
		}

		Console.ResetColor();
		Console.WriteLine($"\nTotal Consumer Groups Created: {count}");
	}

	static async Task DeleteConsumerGroups()
	{
		Console.Write("\nAre you sure you want to delete all student consumer groups? (Y/N): ");
		if (!Console.ReadLine().Trim().Equals("Y", StringComparison.OrdinalIgnoreCase)) return;

		var existing = await GetConsumerGroupNames();
		int count = 0;

		foreach (var student in Students)
		{
			var name = $"cg-{student}";
			if (existing.Contains(name))
			{
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine($"Deleting Consumer Group: {name}");
				await EventHubResource.GetEventHubConsumerGroups().Get(name).Value.DeleteAsync(WaitUntil.Completed);
				count++;
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"Skipped (not found): {name}");
			}
		}

		Console.ResetColor();
		Console.WriteLine($"\nTotal Consumer Groups Deleted: {count}");
	}

	static async Task ToggleSku()
	{
		var currentSku = NamespaceResource.Data.Sku.Name;
		Console.WriteLine($"\nToggling pricing tier for Event Hub Namespace '{NamespaceName}' (Current Tier: {currentSku})");

		if (currentSku.Equals("Standard", StringComparison.OrdinalIgnoreCase))
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine("Current tier is Standard. Preparing to downgrade to Basic...");

			await foreach (var group in EventHubResource.GetEventHubConsumerGroups().GetAllAsync())
			{
				if (group.Data.Name != "$Default")
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"Deleting Consumer Group: {group.Data.Name}");
					await group.DeleteAsync(WaitUntil.Completed);
				}
			}

			Console.WriteLine("Updating tier to Basic...");
			await NamespaceResource.SetSkuAsync(new EventHubsSku("Basic"));
		}
		else if (currentSku.Equals("Basic", StringComparison.OrdinalIgnoreCase))
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Current tier is Basic. Upgrading to Standard...");
			await NamespaceResource.SetSkuAsync(new EventHubsSku("Standard"));
		}
		else
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine($"Unsupported SKU type: {currentSku}");
		}

		NamespaceResource = await NamespaceResource.GetAsync(); // refresh
		Console.ResetColor();
		Console.WriteLine($"Updated pricing tier: {NamespaceResource.Data.Sku.Name}");
	}
}
