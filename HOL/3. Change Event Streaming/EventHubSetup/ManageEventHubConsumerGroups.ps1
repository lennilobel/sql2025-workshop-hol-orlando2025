Connect-AzAccount

$resourceGroup = "demos-rg"
$eventHubNamespaceName = "sql2025-ces"
$eventHubName = "ces-hub"

$currentFolder = Split-Path -Parent $MyInvocation.MyCommand.Definition
$studentsFilePath = Join-Path $currentFolder "ManageEventHubConsumerGroups-students.txt"

if (!(Test-Path $studentsFilePath)) {
    Write-Host "ERROR: Student list file not found at '$studentsFilePath'" -ForegroundColor Red
    exit 1
}

# Get current pricing tier
try {
    $namespace = Get-AzEventHubNamespace -ResourceGroupName $resourceGroup -Name $eventHubNamespaceName
    $currentSku = $namespace.SkuTier
}
catch {
    Write-Host "ERROR: Could not retrieve Event Hub namespace. Check resource group and namespace name." -ForegroundColor Red
    exit 1
}

Write-Host "Manage Event Hub Consumer Groups for HOL Students (Current Tier: $currentSku)" -ForegroundColor Cyan

$students = Get-Content $studentsFilePath |
    Where-Object {
        $line = $_.Trim()
        $line -ne "" -and -not $line.StartsWith("#")
    }

do {
    Write-Host ""
    Write-Host "Choose an action:"
    Write-Host "  S = Show student list"
    Write-Host "  L = List all consumer groups"
    Write-Host "  C = Create student consumer groups"
    Write-Host "  D = Delete student consumer groups"
    Write-Host "  T = Toggle Event Hub pricing tier (Standard <-> Basic)"
    Write-Host "  Q = Quit"
    Write-Host ""

    $mode = Read-Host "Enter choice (S, L, C, D, T, Q)"

    switch ($mode.ToUpper()) {
        "S" {
            Write-Host ""
            Write-Host "Student list (used to name consumer groups):" -ForegroundColor White
            Write-Host ""

            foreach ($student in $students) {
                Write-Host $student -ForegroundColor Green
            }

            Write-Host ""
            Write-Host "Total Students: $($students.Count)" -ForegroundColor White
        }

        "L" {
            Write-Host ""
            Write-Host "Listing all consumer groups in '$eventHubName':" -ForegroundColor White
            Write-Host ""

            $groups = Get-AzEventHubConsumerGroup `
                -ResourceGroupName $resourceGroup `
                -NamespaceName $eventHubNamespaceName `
                -EventHubName $eventHubName

            $groups.Name | ForEach-Object { Write-Host $_ -ForegroundColor Green }

            Write-Host ""
            Write-Host "Total Consumer Groups: $($groups.Count)" -ForegroundColor White
        }

        "C" {
            Write-Host ""
            $confirm = Read-Host "Are you sure you want to create all student consumer groups? (Y/N)"
            if ($confirm.ToUpper() -ne "Y") { continue }

            Write-Host ""
            Write-Host "Creating all student consumer groups in '$eventHubName':" -ForegroundColor White
            Write-Host ""

            $existingGroups = (
                Get-AzEventHubConsumerGroup `
                    -ResourceGroupName $resourceGroup `
                    -NamespaceName $eventHubNamespaceName `
                    -EventHubName $eventHubName
            ).Name

            $createdCount = 0

            foreach ($student in $students) {
                $consumerGroupName = "cg-$student"

                if ($existingGroups -contains $consumerGroupName) {
                    Write-Host "Skipped (already exists): $consumerGroupName" -ForegroundColor Yellow
                }
                else {
                    Write-Host "Creating Consumer Group: $consumerGroupName" -ForegroundColor Green

                    $null = New-AzEventHubConsumerGroup `
                        -ResourceGroupName $resourceGroup `
                        -NamespaceName $eventHubNamespaceName `
                        -EventHubName $eventHubName `
                        -Name $consumerGroupName

                    $createdCount++
                }
            }

            Write-Host ""
            Write-Host "Total Consumer Groups Created: $createdCount" -ForegroundColor White
        }

        "D" {
            Write-Host ""
            $confirm = Read-Host "Are you sure you want to delete all student consumer groups? (Y/N)"
            if ($confirm.ToUpper() -ne "Y") { continue }

            Write-Host ""
            Write-Host "Deleting all student consumer groups in '$eventHubName':" -ForegroundColor White
            Write-Host ""

            $existingGroups = (
                Get-AzEventHubConsumerGroup `
                    -ResourceGroupName $resourceGroup `
                    -NamespaceName $eventHubNamespaceName `
                    -EventHubName $eventHubName
            ).Name

            $deletedCount = 0

            foreach ($student in $students) {
                $consumerGroupName = "cg-$student"

                if ($existingGroups -contains $consumerGroupName) {
                    Write-Host "Deleting Consumer Group: $consumerGroupName" -ForegroundColor Green

                    Remove-AzEventHubConsumerGroup `
                        -ResourceGroupName $resourceGroup `
                        -NamespaceName $eventHubNamespaceName `
                        -EventHubName $eventHubName `
                        -Name $consumerGroupName

                    $deletedCount++
                }
                else {
                    Write-Host "Skipped (not found): $consumerGroupName" -ForegroundColor Yellow
                }
            }

            Write-Host ""
            Write-Host "Total Consumer Groups Deleted: $deletedCount" -ForegroundColor White
        }

        "T" {
            Write-Host ""
            Write-Host "Toggling pricing tier for Event Hub Namespace '$eventHubNamespaceName'..." -ForegroundColor White

            $namespace = Get-AzEventHubNamespace -ResourceGroupName $resourceGroup -Name $eventHubNamespaceName
            $currentSku = $namespace.SkuTier

            if ($currentSku -eq "Standard") {
                Write-Host "Current tier is Standard. Preparing to downgrade to Basic..." -ForegroundColor Yellow

                $groupsToDelete = Get-AzEventHubConsumerGroup `
                    -ResourceGroupName $resourceGroup `
                    -NamespaceName $eventHubNamespaceName `
                    -EventHubName $eventHubName |
                    Where-Object { $_.Name -ne '$Default' }

                foreach ($group in $groupsToDelete) {
                    Write-Host "Deleting Consumer Group: $($group.Name)" -ForegroundColor Red
                    Remove-AzEventHubConsumerGroup `
                        -ResourceGroupName $resourceGroup `
                        -NamespaceName $eventHubNamespaceName `
                        -EventHubName $eventHubName `
                        -Name $group.Name
                }

                Write-Host "Updating tier to Basic..." -ForegroundColor Yellow

                Set-AzEventHubNamespace `
                    -ResourceGroupName $resourceGroup `
                    -Name $eventHubNamespaceName `
                    -SkuName Basic `
                    -Location $namespace.Location
            }
            elseif ($currentSku -eq "Basic") {
                Write-Host "Current tier is Basic. Upgrading to Standard..." -ForegroundColor Green

                Set-AzEventHubNamespace `
                    -ResourceGroupName $resourceGroup `
                    -Name $eventHubNamespaceName `
                    -SkuName Standard `
                    -Location $namespace.Location
            }
            else {
                Write-Host "Unsupported SKU type: $currentSku" -ForegroundColor Red
            }

            # Refresh and show new tier
            $namespace = Get-AzEventHubNamespace -ResourceGroupName $resourceGroup -Name $eventHubNamespaceName
            $currentSku = $namespace.SkuTier
            Write-Host "Updated pricing tier: $currentSku" -ForegroundColor Cyan
        }

        "Q" {
            Write-Host "Exiting..."
        }

        default {
            Write-Host "Invalid input." -ForegroundColor Red
        }
    }

} while ($mode.ToUpper() -ne "Q")
