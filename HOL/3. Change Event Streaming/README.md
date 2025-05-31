# Change Event Streaming

## Overview

This hands-on lab demonstrates the new **Change Event Streaming (CES)** feature in **SQL Server 2025**, which provides reliable, partitioned change events to **Azure Event Hubs**, enabling push-based integrations without polling or ETL jobs. You will:

* Set up a database and configure it for CES.
* Define a stream group and associate it with various tables.
* Build a .NET consumer application to listen for and process changes.
* Execute T-SQL operations that generate change events.
* Observe how CES sends events to Azure Event Hubs in real-time.

**Pre-requisites**: Your instructor has provisioned:

* An Event Hub namespace and hub
* A shared access policy with required claims
* A blob storage account and container
* A SAS token (shared separately)

> **Note:** If you wish to run these labs after the training event, the instructor-provisioned pre-requisite resources will not be available. Instead, you will need to provision these resources in your Azure subscription. For details, refer to [Configure change event streaming](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/change-event-streaming/configure?view=sql-server-ver17) in the CES documentation.
