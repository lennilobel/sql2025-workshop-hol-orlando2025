# Manage Event Hub consumer groups per student inside a single Event Hub (Standard tier required)

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

Write-Host "Manage Event Hub Consumer Groups for HOL Students"

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
    Write-Host "  Q = Quit"
    Write-Host ""

    $mode = Read-Host "Enter choice (S, L, C, D, Q)"

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

        "Q" {
            Write-Host "Exiting..."
        }

        default {
            Write-Host "Invalid input." -ForegroundColor Red
        }
    }

} while ($mode.ToUpper() -ne "Q")
