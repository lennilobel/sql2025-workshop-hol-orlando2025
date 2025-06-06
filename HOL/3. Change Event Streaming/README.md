﻿# Change Event Streaming

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

## Shared Lab Resources and the Need for Isolation

In this lab, all **attendees will be working concurrently** with a **shared Azure Event Hubs namespace** and a **shared Azure Blob Storage container** for checkpointing. This approach conserves resources and simplifies setup, but does introduces a few important concerns:

- **Event Stream Overlap**

   Without proper isolation, events generated by one attendee will be seen by all other attendees. This would break the integrity of individual lab results and lead to confusion.

- **Checkpoint Collision**

   Azure Event Hubs uses **consumer groups** and **blob storage checkpoints** to track progress. If two attendees share the same consumer group or storage path, their progress can overwrite each other’s, causing data loss or duplication.

To ensure that each attendee sees only their own events and progress, we implement the following strategies:

- **Unique Database Name per Attendee**

   Each attendee will use different name for their database, based on their first and last names (for example, `CesDemo_john_smith`, and `CesDemo_jane_doe`).

   > This allows filtering on database name in the client application to ensure that you only process changes that are made in your database.

- **Unique Event Hub Consumer Group Name per Attendee**

   Similarly, each attendee will be assigned a unique Event Hub consumer group (for example, `cg-john-smith`, `cg-jane-doe`).

   > All checkpoint data is stored in the same Azure Blob Storage container, but because each consumer group is unique, each attendee has their own isolated set of checkpoint blobs, ensuring you receive your own event stream without interference from other attendees.

___

▶ [Lab: Create the Sample CES Database](https://github.com/lennilobel/sql2025-workshop-hol-orlando2025/blob/main/HOL/3.%20Change%20Event%20Streaming/1.%20Create%20the%20Sample%20CES%20Database.md)

