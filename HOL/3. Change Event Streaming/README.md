# Change Event Streaming

## Overview

This hands-on lab demonstrates the new **Change Event Streaming (CES)** feature in **SQL Server 2025**, which provides reliable, partitioned change events to **Azure Event Hubs**, enabling push-based integrations without polling or ETL jobs. You will:

* Set up a database and configure it for CES.
* Define a stream group and associate it with various tables.
* Build a .NET consumer application to listen for and process changes.
* Execute T-SQL operations that generate change events.
* Observe how CES sends events to Azure Event Hubs in real-time.

**Pre-requisites**: Your instructor has provisioned:

* An Azure Event Hub namespace, hub, and access policy to support Change Event Streaming in SQL Server
* An Azure Blob Storage account and container for maintaining change event checkpoints

Your instructor has separately provided you with the SAS token and connection string needed to access the Event Hub and Azure BLob Storage account respectively.

> **Note:** If you wish to run these labs after the training event, the instructor-provisioned pre-requisite resources will not be available. Instead, you will need to provision these resources in your Azure subscription. For details, refer to [Configure change event streaming](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/change-event-streaming/configure?view=sql-server-ver17) in the CES documentation.
