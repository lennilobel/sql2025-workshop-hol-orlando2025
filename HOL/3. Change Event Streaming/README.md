# Change Event Streaming

## Overview

This hands-on lab demonstrates the new **Change Event Streaming (CES)** feature in **SQL Server 2025**, which provides reliable, partitioned change events to **Azure Event Hubs**, enabling push-based integrations without polling or ETL jobs. You will:

* Set up a database and configure it for CES.
* Define a stream group and associate it with various tables.
* Build a .NET consumer application to listen for and process changes.
* Execute T-SQL operations that generate change events.
* Observe how CES differs from traditional triggers.

**Pre-requisites**: Your instructor has provisioned:

* An Event Hub namespace and hub
* A shared access policy with required claims
* A blob storage account and container
* A SAS token (shared separately)

