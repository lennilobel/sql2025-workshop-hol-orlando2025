# Exploring New JSON Capabilities in SQL Server 2025

SQL Server 2025 introduces several powerful enhancements to its native JSON support, addressing long-standing gaps in performance, usability, and standards compliance. These features make JSON a first-class citizen alongside traditional relational data types.

This hands-on lab walks you through the new JSON features introduced in SQL Server 2025:

* Native `json` data type
* In-place updates using the `.modify()` method
* JSON path indexing with `CREATE JSON INDEX`
* JSON path expression array enhancements
* JSON aggregates
* The new `RETURNING` clause for type-safe extraction with `JSON_VALUE`
* The new `JSON_CONTAINS` function for containment checks

In SSMS, right-click on the **AdventureWorks2022** database in Object Explorer and select **New Query** to open a new query window for this lab.

## Native JSON data type

Previously, JSON was stored as `nvarchar`, which treated the JSON payload as an unstructured string. This meant that updates required overwriting the entire value and offered no storage or performance optimizations.

SQL Server 2025 introduces a true `json` data type with an **internal compact binary format**, which is significantly more space-efficient than storing JSON text in `nvarchar(max)`. SQL Server parses and stores the document in a binary representation optimized for traversal and indexing, reducing memory usage and I/O.

You can observe this difference with `DATALENGTH`, which returns the number of bytes used to store a value. The following example compares storage sizes for the same JSON content stored as `varchar(max)`, `nvarchar(max)`, and native `json`:

```sql
DECLARE @JsonData_nvarchar nvarchar(max) = N'
[
  {
    "OrderId": 5,
    "CustomerId": 6,
    "OrderDate": "2024-10-10T14:22:27.25-05:00",
    "OrderAmount": 25.9
  },
  {
    "OrderId": 6,
    "CustomerId": 76,
    "OrderDate": "2024-12-10T11:02:36.12-08:00",
    "OrderAmount": 350.25
  },
  {
    "OrderId": 7,
    "CustomerId": 9,
    "OrderDate": "2024-10-10T14:22:27.25-05:00",
    "OrderAmount": 862.75
  },
  {
    "OrderId": 8,
    "CustomerId": 7,
    "OrderDate": "2024-12-10T11:02:36.12-08:00",
    "OrderAmount": 591.95
  },
  {
    "OrderId": 9,
    "CustomerId": 15,
    "OrderDate": "2024-10-10T14:22:27.25-05:00",
    "OrderAmount": 8510.00
  },
  {
    "OrderId": 10,
    "CustomerId": 2,
    "OrderDate": "2024-12-10T11:02:36.12-08:00",
    "OrderAmount": 871.10
  }
]'

DECLARE @JsonData_varchar  varchar(max)   = CAST(@JsonData_nvarchar AS varchar(max))
DECLARE @JsonData_json     json           = CAST(@JsonData_nvarchar AS json)

SELECT
  Length_varchar  = DATALENGTH(@JsonData_varchar),
  Length_nvarchar = DATALENGTH(@JsonData_nvarchar),
  Length_json     = DATALENGTH(@JsonData_json)
```

The results typically show:

* `varchar(max)` size: 781 bytes
* `nvarchar(max)` size: 1,562 bytes (due to 2 bytes per Unicode character)
* `json` size: 529 bytes (thanks to its compact binary storage format)

This highlights a dramatic advantage of the `json` type: **even while preserving full Unicode fidelity like `nvarchar(max)`, it consumes significantly less space**—in this case, a reduction from 1,562 bytes to just 529 bytes. This efficiency translates to reduced I/O, lower memory pressure, and better performance overall when working with large-scale JSON datasets.

The `json` data type in SQL Server inherently supports Unicode, similar to `nvarchar`. This makes it safer than `varchar` for multilingual data and avoids the risk of character loss or corruption. Yet, even while stripped of the overhead of Unicode, it still consumes more space at 781 bytes, compared to the Unicode-supporting version of the same content stored in the `json` data type at 529 bytes.

## The `.modify()` Method

Previously, JSON was stored as `nvarchar`, which treated the JSON payload as an unstructured Unicode string. This meant updates required overwriting the entire value and offered no storage or performance optimizations. The new `json` data type supports partial document updates via the `.modify()` method.

To demonstrate, let's first create a table with a native JSON column:

```sql
CREATE TABLE Customer
(
  CustomerId    int PRIMARY KEY,
  CustomerJson  json
)
```

This table uses the new `json` data type for the `CustomerJson` column. Unlike `nvarchar(max)`, the `json` type enforces that all stored values are valid JSON. This improves performance, enables rich in-place modifications, and allows JSON-specific indexing and path enforcement.

First, try and insert some invalid JSON to see the validation in action. This statement should fail because the JSON is malformed:

```sql
INSERT INTO Customer
  VALUES (1, '{ "name": "Alice", "age": 30 ')  -- Missing closing brace
```

This statement will also fail because it's not valid JSON at all:

```sql
INSERT INTO Customer
  VALUES (1, 'Just a plain string')
```


Valid JSON will load perfectly of course. Run the following code to add two customers with nested orders and credit cards stored as JSON. This works by using `OPENJSON` to parse and insert multiple rows from the JSON array stored in the `@CustomerJson` variable. Notice how the customer ID is extracted using `JSON_VALUE` for the primary key column, while the entire JSON object is stored in the `CustomerJson` column.

```sql
DECLARE @CustomerJson nvarchar(max) = '
  [
    {
      "customerName": "John Doe",
      "customerId": 1001,
      "orders": [
        { "productId": 712, "quantity": 2 },
        { "productId": 937, "quantity": 1 },
        { "productId": 101, "quantity": 4 },
        { "productId": 214, "quantity": 7 },
        { "productId": 325, "quantity": 1 },
        { "productId": 476, "quantity": 5 },
        { "productId": 583, "quantity": 3 },
        { "productId": 699, "quantity": 2 },
        { "productId": 805, "quantity": 6 },
        { "productId": 912, "quantity": 1 },
        { "productId": 1033, "quantity": 8 },
        { "productId": 1144, "quantity": 2 },
        { "productId": 1205, "quantity": 4 },
        { "productId": 1310, "quantity": 3 },
        { "productId": 1458, "quantity": 5 },
        { "productId": 1520, "quantity": 6 },
        { "productId": 1629, "quantity": 1 },
        { "productId": 1740, "quantity": 7 },
        { "productId": 1833, "quantity": 2 },
        { "productId": 1902, "quantity": 4 },
        { "productId": 2011, "quantity": 8 },
        { "productId": 2155, "quantity": 3 },
        { "productId": 2288, "quantity": 5 },
        { "productId": 2390, "quantity": 2 },
        { "productId": 2401, "quantity": 1 },
        { "productId": 2533, "quantity": 6 },
        { "productId": 2689, "quantity": 3 },
        { "productId": 2754, "quantity": 7 },
        { "productId": 2861, "quantity": 8 },
        { "productId": 2977, "quantity": 2 },
        { "productId": 3050, "quantity": 4 },
        { "productId": 3198, "quantity": 1 }
      ],
      "creditCards": [
        {
          "type": "American Express",
          "number": "675984450768756054",
          "currency": "USD"
        },
        {
          "type": "Visa",
          "number": "3545138777072343",
          "currency": "USD"
        },
        {
          "type": "MasterCard",
          "number": "6397068371771473",
          "currency": "CAD"
        },
        {
          "type": "Discover",
          "number": "6011000990139424",
          "currency": "EUR"
        },
        {
          "type": "JCB",
          "number": "3530111333300000",
          "currency": "GBP"
        }
      ],
      "balance": 25.99,
      "status": "processing",
      "basket": {
        "status": "PENDING",
        "lastUpdated": "2025-06-07T07:32:00Z"
      },
      "preferred": false
    },
    {
      "customerName": "Jane Smith",
      "customerId": 1002,
      "orders": [
        { "productId": 894, "quantity": 5 },
        { "productId": 3001, "quantity": 1 }
      ],
      "creditCards": [
        {
          "type": "Visa",
          "number": "4111111111111111",
          "currency": "USD"
        },
        {
          "type": "MasterCard",
          "number": "5500000000000004",
          "currency": "CAD"
        }
      ],
      "balance": 99.95,
      "status": "processing",
      "basket": {
        "status": "PENDING",
        "lastUpdated": "2025-05-18T13:18:00Z"
      },
      "preferred": false
    }
  ]
'

INSERT INTO Customer
SELECT
  CustomerId = JSON_VALUE(e.value, '$.customerId'),
  CustomerJson = e.value
FROM
  OPENJSON(@CustomerJson) AS e
```

Now run a quick query to see the inserted data:

```sql
SELECT * FROM Customer
```

You'll need to widen the `CustomerJson` column in the results pane to view the full JSON content. Then scroll right to see all the properties.

> Tip: Double-click the column divider to automatically widen the column to fit the full JSON content.

Let’s use `.modify()` to change several JSON properties of a customer. The following code changes the `preferred` property from `false` to `true` and updates the `basket.status` from `"PENDING"` to `"DEAD"` for the customer with `CustomerId = 1002`. Notice how dotted notation is used to navigate into nested objects.

```sql
SELECT * FROM Customer WHERE CustomerId = 1002

UPDATE Customer
SET CustomerJson.modify('$.preferred', 'true')
WHERE CustomerId = 1002

UPDATE Customer
SET CustomerJson.modify('$.basket.status', 'DEAD')
WHERE CustomerId = 1002

SELECT * FROM Customer WHERE CustomerId = 1002
```

Observe the difference between the two `SELECT` results. The `preferred` property has been changed to `true`, and the `basket.status` has been updated to `"DEAD"`.

You can also create or delete properties. For example, run this code to add a new `priority` property with the value `"high"` to the customer:

```sql
SELECT * FROM Customer WHERE CustomerId = 1002

UPDATE Customer
SET CustomerJson.modify('$.priority', 'high')
WHERE CustomerId = 1002

SELECT * FROM Customer WHERE CustomerId = 1002
```

Observe that the new `priority` property has been appended to the end of the JSON object.

Similarly, `.modify()` can also be used to remove the `priority` property, by supplying `NULL` as the property value:

```sql
SELECT * FROM Customer WHERE CustomerId = 1002

-- Remove an existing field
UPDATE Customer
SET CustomerJson.modify('$.priority', NULL)
WHERE CustomerId = 1002

SELECT * FROM Customer WHERE CustomerId = 1002
```

And now you can see that the `priority` property has been removed.

These examples demonstrate that the `.modify()` method makes JSON updates as natural as relational updates.

## JSON Path Indexing

To enable fast filtering, SQL Server 2025 introduces native JSON indexing. Instead of creating and indexing computed columns, you can declare a JSON index directly on property paths.

This provides an alternative to the legacy approach used in earlier versions of SQL Server. Previously, developers would create **computed columns** based on `JSON_VALUE()` expressions and then index those columns. While effective, this method required more schema scaffolding and introduced some maintenance overhead. It also persisted the computed values, which would consume additional storage. However, this approach remains viable—especially when dealing with fixed, flat JSON paths and when targeting compatibility with SQL Server versions prior to 2025.

In contrast, `CREATE JSON INDEX` in SQL Server 2025 allows you to define **JSON path indexes directly** against native `json` columns. SQL Server transparently builds and maintains a specialized index structure behind the scenes, with full awareness of the JSON document structure. These indexes are more efficient, easier to maintain, and allow more expressive querying without schema bloat.

GPT: Generate the explanatory text for the rest of this section

```sql
SELECT * FROM Customer

-- Before 2025, we could only index scalar properties by creating and then indexing computed columns with JSON_VALUE
ALTER TABLE Customer
 ADD BasketStatus AS JSON_VALUE(CustomerJson, '$.basket.status')
GO

CREATE INDEX IX_Order_BasketStatus
 ON Customer(BasketStatus)

SELECT * FROM Customer
WHERE BasketStatus = 'PENDING'

DROP INDEX IX_Order_BasketStatus
 ON Customer

ALTER TABLE Customer
 DROP COLUMN BasketStatus
```

```sql
-- Now we have native JSON indexes that can point either scalar or complex (nested object/array) properties

-- Create a JSON index that covers the basket property (and all nested properties)
CREATE JSON INDEX IX_Customer_CustomerJson_Basket
ON Customer (CustomerJson)
FOR ('$.basket')
```

```sql
SELECT * FROM sys.indexes WHERE type = 9
SELECT * FROM sys.json_index_paths
```

```sql
-- Execution plan shows no index being used; we must explicitly reference the index with a hint
--  (Preview note: the heuristics for using rowcount and statistics to pick a JSON index for query plan is not complete)
SELECT *
FROM Customer
WHERE JSON_VALUE(CustomerJson, '$.basket.status') = 'PENDING'

-- This query will leverage the JSON index
--  (Preview note: the index hint is required for now, but in the future it will be automatically picked up by the query optimizer)
SELECT *
FROM Customer WITH (INDEX (IX_Customer_CustomerJson_Basket))
WHERE JSON_VALUE(CustomerJson, '$.basket.status') = 'PENDING'

-- This query generates an error because the JSON index it references does not cover the JSON property being queried ($.status)
/*
SELECT *
FROM Customer WITH (INDEX (IX_Customer_CustomerJson_Basket))
WHERE JSON_VALUE(CustomerJson, '$.status') = 'processing'
*/
```

```sql
-- Only one JSON index can be created per table, so we must drop the previous one before creating a new one
DROP INDEX IX_Customer_CustomerJson_Basket ON Customer
```

```sql
-- Create a JSON index that covers the entire JSON document (with all nested properties)
CREATE JSON INDEX IX_Customer_CustomerJson_All
ON Customer (CustomerJson)
FOR ('$')
```

```sql
SELECT * FROM sys.indexes WHERE type = 9
SELECT * FROM sys.json_index_paths
```

```sql
-- The JSON index will be leveraged for any query that references any property in the JSON document
SELECT *
FROM Customer WITH (INDEX (IX_Customer_CustomerJson_All))
WHERE JSON_VALUE(CustomerJson, '$.status') = 'processing'

SELECT *
FROM Customer WITH (INDEX (IX_Customer_CustomerJson_All))
WHERE JSON_VALUE(CustomerJson, '$.basket.status') = 'PENDING'
```

```sql
DROP INDEX IX_Customer_CustomerJson_All ON Customer
```

## JSON Path Expression Array Enhancements

GPT: Generate the explanatory text for this section

```sql
-- How many credit cards does each customer have?

-- The JSON_ARRAY_LENGTH function returns is not (yet?) supported in SQL Server 2025
/*
SELECT
    CustomerId,
    CreditCardCount = JSON_ARRAY_LENGTH(CustomerJson, '$.creditCards')
FROM
    Customer
*/

-- So we need to use OPENJSON to count the number of elements in the array (very hackey)
SELECT
    CustomerId,
    CreditCardCount = (SELECT COUNT(*) FROM OPENJSON(CustomerJson, '$.creditCards'))
FROM
    Customer
```

```sql
-- Before SQL Server 2025, array references in JSON path expressions were very limited
SELECT
    CustomerId,
    AllCreditCards              = JSON_QUERY(e.CustomerJson, '$.creditCards'),
    FirstCreditCard             = JSON_QUERY(e.CustomerJson, '$.creditCards[0]'),
    FirstCreditCardType         = JSON_VALUE(e.CustomerJson, '$.creditCards[0].type'),
    SecondCreditCard            = JSON_QUERY(e.CustomerJson, '$.creditCards[1]'),
    SecondCreditCardType        = JSON_VALUE(e.CustomerJson, '$.creditCards[1].type')
FROM
    Customer AS e
```

```sql
-- SQL Server 2025 supports array wildcard and range references with JSON_QUERY, JSON_PATH_EXISTS, and JSON_CONTAINS

SELECT
    CustomerId,
    -- Before SQL Server 2025, array references in JSON path expressions were very limited
    AllCreditCardsBad           = JSON_QUERY(e.CustomerJson, '$.creditCards[*]'),
    AllCreditCards              = JSON_QUERY(e.CustomerJson, '$.creditCards[*]' WITH ARRAY WRAPPER),
    AllCreditCardTypes          = JSON_QUERY(e.CustomerJson, '$.creditCards[*].type' WITH ARRAY WRAPPER),
    FirstThreeCreditCardTypes   = JSON_QUERY(e.CustomerJson, '$.creditCards[0 to 2].type' WITH ARRAY WRAPPER),
    LastCreditCardType          = JSON_VALUE(e.CustomerJson, '$.creditCards[last].type'),
    FirstAndLastCreditCardType  = JSON_QUERY(e.CustomerJson, '$.creditCards[0, last].type' WITH ARRAY WRAPPER),
    LastAndFirstCreditCardType  = JSON_QUERY(e.CustomerJson, '$.creditCards[last, 0].type' WITH ARRAY WRAPPER),
    EveryOtherCreditCardType    = JSON_QUERY(e.CustomerJson, '$.creditCards[0, 2, 4, 6, 8].type' WITH ARRAY WRAPPER)
FROM
    Customer AS e
```

## JSON Aggregates

SQL Server 2025 introduces new JSON aggregate functions that simplify the construction of JSON arrays and objects from relational data.

GPT: Generate the explanatory text for the rest of this section

```sql
DROP TABLE IF EXISTS Customer
```

```sql
CREATE TABLE Customer (
  CustomerId     varchar(10) NOT NULL PRIMARY KEY,
  OrderTime       datetime2,
  AccountNumber   varchar(10) REFERENCES Account (AccountNumber),
  Price           decimal(10, 2),
  Quantity        int
)
```

```sql
CREATE TABLE Account (
  AccountNumber varchar(10) NOT NULL PRIMARY KEY,
  Phone1 varchar(20),
  Phone2 varchar(20),
  Phone3 varchar(20)
)
```

```sql
INSERT INTO Account VALUES
  ('AW29825', '(123) 456-7890', '(123) 567-8901', NULL),
  ('AW73565', '(234) 987-6543', NULL,             NULL)
```

```sql
-- Input JSON document containing a JSON array where each element is a JSON object
DECLARE @OrdersJson json = '
[
  {
    "CustomerId": "S043659",
    "Date":"2022-05-24T08:01:00",
    "AccountNumber":"AW29825",
    "Price":59.99,
    "Quantity":1
  },
  {
    "CustomerId": "S043661",
    "Date":"2022-05-20T12:20:00",
    "AccountNumber":"AW73565",
    "Price":24.99,
    "Quantity":3
  }
]'

INSERT INTO Customer
SELECT
  CustomerId    = JSON_VALUE(value, '$.CustomerId'),
  OrderTime     = JSON_VALUE(value, '$.Date'),
  AccountNumber = JSON_VALUE(value, '$.AccountNumber'),
  Price         = JSON_VALUE(value, '$.Price'),
  Quantity      = JSON_VALUE(value, '$.Quantity')
FROM
  OPENJSON(@OrdersJson)
GO
```

```sql
SELECT * FROM Customer
SELECT * FROM Account
```

```sql
-- JSON_OBJECTAGG

SELECT
  *
FROM
  Customer AS o
  INNER JOIN Account AS a ON a.AccountNumber = o.AccountNumber

SELECT
  JSON_OBJECT(
    'CustomerId': o.CustomerId,
    'Date': o.OrderTime,
    'Price': o.Price,
    'Quantity': o.Quantity, 
    'AccountDetails': JSON_OBJECT(
      'AccountNumber': o.AccountNumber,
      'PhoneNumbers': JSON_ARRAY(
        a.Phone1,
        a.Phone2,
        a.Phone3
      )
    )
  )  
FROM
  Customer AS o
  INNER JOIN Account AS a ON a.AccountNumber = o.AccountNumber

SELECT
  JSON_OBJECTAGG(CustomerId:
    JSON_OBJECT(
      'CustomerId': o.CustomerId,
      'Date': o.OrderTime,
      'Price': o.Price,
      'Quantity': o.Quantity, 
      'AccountDetails': JSON_OBJECT(
        'AccountNumber': o.AccountNumber,
        'PhoneNumbers': JSON_ARRAY(
          a.Phone1,
          a.Phone2,
          a.Phone3
        )
      )
    )  
  )
FROM
  Customer AS o
  INNER JOIN Account AS a ON a.AccountNumber = o.AccountNumber
```

```sql
-- JSON_ARRAYAGG

SELECT
  JSON_ARRAY(
    a.Phone1,
    a.Phone2,
    a.Phone3
  ) AS Phones
  FROM
  Customer AS o
  INNER JOIN Account AS a ON a.AccountNumber = o.AccountNumber

SELECT
  AccountNumber,
  JSON_ARRAY(
    Phone1,
    Phone2,
    Phone3
  ) AS Phones
  FROM
  Account

SELECT
  JSON_ARRAYAGG(
    JSON_ARRAY(
      Phone1,
      Phone2,
      Phone3
    )
  ) AS Phones
  FROM
  Account
```

```sql
-- More JSON aggregates
CREATE TABLE SampleData (
  Category int,
  Name varchar(10),
  Type varchar(10),
  Amount int
)

INSERT INTO SampleData VALUES
 (1, 'Item 1.1', 'TypeA', 100),
 (1, 'Item 1.2', 'TypeB', 200),
 (1, 'Item 1.3', 'TypeB', 300),
 (2, 'Item 2.1', 'TypeD', 400),
 (2, 'Item 2.2', 'TypeD', 500)

SELECT * FROM SampleData

SELECT
  AllAmountsArray         = JSON_ARRAYAGG(Amount),
  AllAmountsObjectByName  = JSON_OBJECTAGG(Name:Amount),
  AllAmountsTotal         = SUM(Amount)
FROM
  SampleData
```

```sql
-- JSON aggregates with GROUP BY

SELECT
  Category,
  CategoryAmountsArray    = JSON_ARRAYAGG(Amount),
  CategoryAmountsObjectByName = JSON_OBJECTAGG(Name:Amount),
  CategoryAmountsTotal    = SUM(Amount)
FROM
  SampleData
GROUP BY
  Category

SELECT
  Category,
  Type,
  CategoryAndTypeAmountsArray     = JSON_ARRAYAGG(Amount),
  CategoryAndTypeAmountsObjectByName  = JSON_OBJECTAGG(Name:Amount),
  CategoryAndTypeAmountsTotal     = SUM(Amount)
FROM
  SampleData
GROUP BY
  Category,
  Type
```

```sql
-- JSON aggregates with OVER

SELECT
  Category,
  Type,
  CategoryAmountsArray    = JSON_ARRAYAGG(Amount)     OVER (PARTITION BY Category),
  CategoryAmountsObjectByName = JSON_OBJECTAGG(Name:Amount)   OVER (PARTITION BY Category),
  CategoryAmountsTotal    = SUM(Amount)           OVER (PARTITION BY Category)
FROM
  SampleData
```

```sql
-- JSON aggregates with GROUPING SETS

SELECT
  Category,
  Type,
  GroupAmountsArray    = JSON_ARRAYAGG(Amount),
  GroupAmountsObjectByName = JSON_OBJECTAGG(Name:Amount),
  GroupAmountsTotal    = SUM(Amount)
FROM
  SampleData
GROUP BY
  GROUPING SETS(
    (Category),     -- Group by Category only
    (Type),       -- Group by Type only
    (Category, Type),   -- Group by both Category and Type
    ()          -- No GROUP BY (grand balance)
  )
ORDER BY
  Category,
  Type
```

```sql
-- Clean up
DROP TABLE SampleData
DROP TABLE Customer
DROP TABLE Account
```

## Type-Safe Extraction with `RETURNING` Clause

The `RETURNING` clause in `JSON_VALUE` allows you to specify the expected return type directly within the function call. This provides stronger typing, better validation, and clearer intent compared to the previous approach of using `CAST()` or `TRY_CAST()` externally.

GPT: Generate the explanatory text for the rest of this section

```sql
-- Throws error because the JSON_VALUE function return nvarchar by default, and so the numeric > operator fails
IF 1 = 0
    SELECT * FROM Customer WHERE JSON_VALUE(CustomerJson, '$.balance') > 50.5

-- Convert nvarchar to money data type using CAST or CONVERT
SELECT * FROM Customer WHERE CAST(JSON_VALUE(CustomerJson, '$.balance') AS money) > 50.5

-- Now we can use the (ANSI-compliant) RETURNING clause, but only for integer types
SELECT * FROM Customer WHERE JSON_VALUE(CustomerJson, '$.balance' RETURNING int) > 50
```

## Flexible Matching with `JSON_CONTAINS`

The new `JSON_CONTAINS` predicate checks whether one JSON document is semantically contained within another.

GPT: Generate the explanatory text for the rest of this section

```sql
-- Search for an integer value in a JSON path
DECLARE @JsonData json = '{
  "customerId": 1001,
  "customerId": 2002,
  "basket": {
    "totalItems": 4,
    "labels": ["fragile"]
  },
  "items": [1, 3, {"quantities": [89]}, false],
  "discount": null,
  "preferred": true
}'

SELECT Found =
  JSON_CONTAINS(
    @JsonData,    -- Scan this JSON content
    1001,       -- Search for this value
    '$.customerId'   -- In a root-level property named "customerId"
  )

GO
```

```sql
-- Search for a string value in a JSON path
DECLARE @JsonData json = '{
  "customerId": 1001,
  "customerId": 2002,
  "basket": {
    "totalItems": 4,
    "labels": ["fragile"]
  },
  "items": [1, 3, {"quantities": [89]}, false],
  "discount": null,
  "preferred": true
}'

SELECT Found =
  JSON_CONTAINS(
    @JsonData,        -- Scan this JSON content
    'fragile',        -- Search for this value
    '$.basket.labels[*]'  -- In all elements in the "labels" array inside the "basket" object
  )

GO
```

```sql
-- Search for a bit (boolean) value in a JSON array
DECLARE @JsonData json = '{
  "customerId": 1001,
  "customerId": 2002,
  "basket": {
    "totalItems": 4,
    "labels": ["fragile"]
  },
  "items": [1, 3, {"quantities": [89]}, true],
  "discount": null,
  "preferred": true
}'

SELECT Found =
  JSON_CONTAINS(
    @JsonData,      -- Scan this JSON content
    CAST(1 AS bit),   -- Search for this value (true)
    '$.items[*]'    -- In all elements in the "items" array
  )

GO
```

```sql
-- Search for an integer value contained within a nested JSON array
DECLARE @JsonData json = '{
  "customerId": 1001,
  "customerId": 2002,
  "basket": {
    "totalItems": 4,
    "labels": ["fragile"]
  },
  "items": [1, 3, {"quantities": [89]}, false],
  "discount": null,
  "preferred": true
}'

SELECT Found =
  JSON_CONTAINS(
    @JsonData,          -- Scan this JSON content
    89,             -- Search for this value
    '$.items[*].quantities[*]'  -- In all values inside "quantities" arrays found in any object within the "items" array
  )

GO
```

```sql
-- Search for an integer value contained within a JSON object in a JSON array
DECLARE @JsonData json = '[
  {"customerId": 1001, "customerId": 2002, "priority": 1},
  {"customerId": 329, "customerId": 1343, "priority": 1},
  {"customerId": 1056, "customerId": 80, "priority": 3},
  {"customerId": 871, "customerId": 232, "priority": 2}
]'

SELECT Found =
  JSON_CONTAINS(
    @JsonData,    -- Scan this JSON content
    1056,       -- Search for this value
    '$[*].customerId'  -- In the "customerId" field in every object in the root-level array
  )

GO
```
