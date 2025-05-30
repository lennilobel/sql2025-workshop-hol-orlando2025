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

## Step 1: Create and Configure the Database

```sql
USE master
GO

CREATE DATABASE CesDemo
GO

ALTER AUTHORIZATION ON DATABASE::CesDemo TO sa
GO

USE CesDemo
GO

ALTER DATABASE CesDemo SET RECOVERY FULL
```

### Create Database Master Key

```sql
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'H@rd2Gue$$P@$$w0rd'
```

### Create Scoped Credential

Replace the SAS token string with the one provided by your instructor.

```sql
CREATE DATABASE SCOPED CREDENTIAL SqlCesCredential
WITH
    IDENTITY = 'SHARED ACCESS SIGNATURE',
    SECRET = '<SharedAccessSignature ...>'
```

### Enable Change Event Streaming

```sql
SELECT * FROM sys.databases WHERE is_event_stream_enabled = 1

EXEC sys.sp_enable_event_stream

SELECT * FROM sys.databases WHERE is_event_stream_enabled = 1
```

### Create an Event Stream Group

```sql
EXEC sys.sp_create_event_stream_group
    @stream_group_name      = 'SqlCesGroup',
    @destination_type       = 'AzureEventHubsAmqp',
    @destination_location   = '<eh-namespace>.servicebus.windows.net/<eh-name>',
    @destination_credential = SqlCesCredential,
    @max_message_size_bytes = 1000000,
    @partition_key_scheme   = 'None'
```

## Step 2: Create Schema and Load Data

Create the demo schema by executing the following T-SQL scripts. These include several interrelated tables, a trigger, and a few stored procedures to simulate real-world data changes.

> Break the execution into blocks to avoid overwhelming SSMS.

**Create Tables**

```sql
-- Product
CREATE TABLE Product (...)
-- Customer
CREATE TABLE Customer (...)
-- Order
CREATE TABLE [Order] (...)
-- OrderLine
CREATE TABLE OrderLine (...)
-- TableWithNoPK
CREATE TABLE TableWithNoPK (...)
```

**Create Trigger**

```sql
CREATE TRIGGER trg_UpdateItemsInStock ON OrderLine AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON
    -- Insert / Update / Delete logic
END
```

**Create Stored Procedures**

```sql
CREATE OR ALTER PROC CreateOrder ...
CREATE OR ALTER PROC CancelOrder ...
CREATE OR ALTER PROC AddOrderLine ...
```

**Insert Sample Data**

```sql
INSERT INTO Product ...
INSERT INTO Customer ...
INSERT INTO TableWithNoPK ...
```

## Step 3: Add Tables to the Stream Group

```sql
EXEC sys.sp_add_object_to_event_stream_group 'SqlCesGroup', 'dbo.Product', 1, 0
EXEC sys.sp_add_object_to_event_stream_group 'SqlCesGroup', 'dbo.Customer', 0, 1
EXEC sys.sp_add_object_to_event_stream_group 'SqlCesGroup', 'dbo.Order', 1, 0
EXEC sys.sp_add_object_to_event_stream_group 'SqlCesGroup', 'dbo.OrderLine', 1, 0
EXEC sys.sp_add_object_to_event_stream_group 'SqlCesGroup', 'dbo.TableWithNoPK', 0, 0
```

Use `sp_help_change_feed_table` to inspect configuration:

```sql
EXEC sp_help_change_feed_table @source_schema = 'dbo', @source_name = 'Product'
```

## Step 4: Build a C# Console Consumer

1. Create a new Console App in Visual Studio: **CESClient**
2. Install NuGet packages:

   * `Azure.Messaging.EventHubs`
   * `Azure.Messaging.EventHubs.Processor`
   * `Azure.Storage.Blobs`
   * `Microsoft.Extensions.Configuration.*`
3. Add the provided `Program.cs`, `EventData.cs`, `Data.cs`, etc.
4. Create `appsettings.json`:

```json
{
  "EventHub": {
    "HostName": "<eh-namespace>.servicebus.windows.net",
    "Name": "<eh-name>",
    "SasToken": "<SharedAccessSignature ...>"
  },
  "BlobStorage": {
    "ConnectionString": "<storage-connection-string>",
    "ContainerName": "<container-name>"
  }
}
```

Your instructor will provide valid credentials.

## Step 5: Run and Test

Start the listener:

```
dotnet run
```

Then execute the following SQL commands to generate changes:

```sql
USE CesDemo
GO

-- Create an order
EXEC CreateOrder @CustomerId = 1000

-- Add order lines
EXEC AddOrderLine @OrderId = 2000, @ProductId = 1, @Quantity = 2
EXEC AddOrderLine @OrderId = 2000, @ProductId = 2, @Quantity = 1

-- Cancel order
EXEC CancelOrder @OrderId = 2000

-- Update with no old values
UPDATE Customer SET City = 'NY' WHERE CustomerId = 1000
UPDATE Customer SET City = 'NY' WHERE CustomerId = 1000 -- Won't emit event

-- Batch update
UPDATE Product SET UnitPrice = UnitPrice * 0.8 WHERE Category = 'Accessory'

-- Multi-row delete
DELETE FROM Product WHERE Colour = 'Black'

-- Updates to table with no PK and minimal column tracking
UPDATE TableWithNoPK SET Datacol = 'Slow' WHERE Id = 3
DELETE FROM TableWithNoPK WHERE Id IN (1, 3)
```

> Observe the CES client: events will appear with INSERT, UPDATE, or DELETE operations, depending on the change type. Look for column values (current vs. old) and the table that triggered the event.

If issues arise:

```sql
SELECT * FROM sys.dm_change_feed_errors ORDER BY entry_time DESC
```

## Step 6: Cleanup

```sql
USE CesDemo
GO

-- Remove objects from stream group
EXEC sys.sp_remove_object_from_event_stream_group 'SqlCesGroup', 'dbo.Product'
EXEC sys.sp_remove_object_from_event_stream_group 'SqlCesGroup', 'dbo.Customer'
EXEC sys.sp_remove_object_from_event_stream_group 'SqlCesGroup', 'dbo.Order'
EXEC sys.sp_remove_object_from_event_stream_group 'SqlCesGroup', 'dbo.OrderLine'
EXEC sys.sp_remove_object_from_event_stream_group 'SqlCesGroup', 'dbo.TableWithNoPK'

-- Drop stream group and disable CES
EXEC sys.sp_drop_event_stream_group 'SqlCesGroup'
EXEC sys.sp_disable_event_stream

-- Drop database
USE master
ALTER DATABASE CesDemo SET SINGLE_USER WITH ROLLBACK IMMEDIATE
DROP DATABASE CesDemo
```

## Summary

In this lab, you learned how to:

* Enable Change Event Streaming in SQL Server 2025
* Register database tables for event streaming
* Create a real-time event processor using C#
* Analyze changes with event metadata and payloads

Change Event Streaming delivers a first-class push model for change tracking—durable, scalable, and ready for cloud-native solutions.
