# Exploring New JSON Capabilities in SQL Server 2025

SQL Server 2025 introduces several powerful enhancements to its native JSON support, addressing long-standing gaps in performance, usability, and standards compliance. These features make JSON a first-class citizen alongside traditional relational data types.

This hands-on lab walks you through the new JSON features introduced in SQL Server 2025:

* Native `json` data type with in-place updates
* Declarative JSON path indexing via `CREATE JSON INDEX`
* The `sys.json_index_paths` metadata view
* The new `JSON_CONTAINS` predicate
* JSON path wildcard improvements for arrays
* The new `WITH ARRAY WRAPPER` clause in `JSON_QUERY`
* The new ANSI SQL `RETURNING` clause in `JSON_VALUE`

> **Prerequisites:** Ensure that you are running SQL Server 2025 and have the `AdventureWorks2022` sample database installed.

---

## Set Up the Sample Environment

While some examples will query real data in `AdventureWorks2022`, most of our exploration will use a simple demo table with native JSON columns. This allows us to focus on the JSON capabilities in isolation.

Create the `Sales.JsonDemo` table with a native JSON column:

```sql
USE AdventureWorks2022
GO

DROP TABLE IF EXISTS Sales.JsonDemo
GO

CREATE TABLE Sales.JsonDemo
(
    JsonDemoId int IDENTITY PRIMARY KEY,
    OrderNumber nvarchar(20),
    OrderDetails json
)
```

This table uses the new `json` data type for the `OrderDetails` column. Unlike `nvarchar(max)`, the `json` type enforces that all stored values are valid JSON. This improves performance, enables rich in-place modifications, and allows JSON-specific indexing and path enforcement.

Now insert some sample documents:

```sql
INSERT INTO Sales.JsonDemo (OrderNumber, OrderDetails)
VALUES
('SO-1001', N'
{
  "customer": "Contoso",
  "lines": [
    { "productId": 712, "quantity": 2 },
    { "productId": 937, "quantity": 1 }
  ],
  "shipped": false
}'),
('SO-1002', N'
{
  "customer": "Fabrikam",
  "lines": [
    { "productId": 870, "quantity": 3 }
  ],
  "shipped": true
}')
```

We now have a table where each row contains an order with nested line items and a boolean flag. Let's explore how SQL Server 2025 lets us interact with these documents natively and efficiently.

---

## Native `json` Data Type and `.modify()` Method

Previously, JSON was stored as `nvarchar`, which treated the JSON payload as an unstructured string. This meant updates required overwriting the entire value and offered no storage or performance optimizations. SQL Server 2025 introduces a true `json` type that supports partial document updates via the `.modify()` method.

Another major advantage of the `json` data type is its **internal compact binary format**, which is more space-efficient than storing JSON text in `nvarchar(max)`. SQL Server parses and stores the document in a binary representation optimized for traversal and indexing, reducing memory usage and I/O.

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

* `varchar(max)` size: 721 bytes
* `nvarchar(max)` size: 1442 bytes (due to 2 bytes per Unicode character)
* `json` size: 529 bytes (thanks to its compact binary storage format)

> This highlights a dramatic advantage of the `json` type: **even while preserving full Unicode fidelity like `nvarchar(max)`, it consumes significantly less space**—in this example, a reduction from 1442 bytes to just 529 bytes. This efficiency translates to reduced I/O, lower memory pressure, and better performance overall when working with large-scale JSON datasets.

> The `json` data type in SQL Server inherently supports Unicode, similar to `nvarchar`. This makes it safer than `varchar` for multilingual data and avoids the risk of character loss or corruption.

> ⚠️ Storing JSON in `varchar(max)` can result in **data loss** if the JSON contains non-ASCII or multilingual Unicode characters. Always use `nvarchar` or `json` to preserve text integrity.

GO

````

Depending on the structure and length of the JSON, the difference may range from modest to substantial.

Let’s update an order to mark it as shipped:

```sql
UPDATE Sales.JsonDemo
SET OrderDetails.modify('replace $.shipped with true')
WHERE OrderNumber = 'SO-1001'
```

Unlike `nvarchar`, the `json` column understands JSON structure and can surgically replace a value inside the document without rewriting the entire blob.

You can also insert or remove paths:

```sql
-- Add a new field
UPDATE Sales.JsonDemo
SET OrderDetails.modify('insert $.priority = "high"')
WHERE OrderNumber = 'SO-1001'

-- Remove an existing field
UPDATE Sales.JsonDemo
SET OrderDetails.modify('remove $.priority')
WHERE OrderNumber = 'SO-1001'
```

This makes JSON updates as natural as relational updates, with less overhead and cleaner syntax.

---

## JSON Path Indexing with `CREATE JSON INDEX`

To enable fast filtering, SQL Server 2025 introduces native JSON indexing. Instead of writing computed columns or `OPENJSON` wrappers, you can declare a JSON index directly on paths.

This provides an alternative to the legacy approach used in earlier versions of SQL Server. Previously, developers would create **computed columns** based on `JSON_VALUE()` expressions and then index those columns. While effective, this method required more schema scaffolding and introduced some maintenance overhead. It also persisted the computed values, which could consume additional storage depending on the use case. However, this approach remains viable—especially when dealing with fixed, flat JSON paths and when targeting compatibility with SQL Server versions prior to 2025.

In contrast, `CREATE JSON INDEX` in SQL Server 2025 allows you to define **JSON path indexes directly** against native `json` columns. SQL Server transparently builds and maintains a specialized index structure behind the scenes, with full awareness of the JSON document structure. These indexes are more efficient, easier to maintain, and allow more expressive querying without schema bloat.

This index will accelerate queries that filter or extract product IDs from the JSON array.

Try a simple filter:

```sql
SELECT OrderNumber
FROM Sales.JsonDemo
WHERE JSON_VALUE(OrderDetails, '$.lines[*].productId') = '712'
```

The optimizer can now leverage the JSON index, improving performance without complex schema refactoring.

Let’s walk through how to confirm this using an execution plan:

1. In SQL Server Management Studio (SSMS), click on the **Include Actual Execution Plan** button in the toolbar, or press **Ctrl+M**.
2. **Do not create the index yet.** First, run the query below to capture a baseline execution plan:

```sql
SELECT OrderNumber
FROM Sales.JsonDemo
WHERE JSON_VALUE(OrderDetails, '$.lines[*].productId') = '712'
```

3. Examine the execution plan. You should see a **Table Scan** or other expensive operator, indicating that SQL Server must examine every row and parse every JSON document at runtime.
4. Now, create the JSON index:

```sql
CREATE JSON INDEX IX_JsonDemo_ProductIds
ON Sales.JsonDemo (OrderDetails)
ON PATH '$.lines[*].productId'
```

5. Rerun the same query again:

```sql
SELECT OrderNumber
FROM Sales.JsonDemo
WHERE JSON_VALUE(OrderDetails, '$.lines[*].productId') = '712'
```

6. Examine the updated execution plan. You should now see a more efficient operator, such as **Index Seek (JSON Index)**, indicating that SQL Server is leveraging the new path-based index.
7. To disable execution plan capture after you're done, click the **Include Actual Execution Plan** button again or press **Ctrl+M** a second time.

To confirm that the index is being used, you can examine the execution plan for the query. In SQL Server Management Studio (SSMS), click on "Include Actual Execution Plan" (or press Ctrl+M) before running the query. After execution, look for an operator like **Index Seek (JSON Index)** in the graphical plan. This indicates that the query engine is utilizing your declared JSON path index rather than performing a full table scan or evaluating every JSON value manually.

---

## Inspecting Index Paths with `sys.json_index_paths`

To understand what paths are indexed and how they’re interpreted, use the new system view `sys.json_index_paths`:

```sql
SELECT *
FROM sys.json_index_paths
WHERE object_id = OBJECT_ID('Sales.JsonDemo')
```

This view shows the full JSON path, data type, and whether the path is nullable. This metadata helps you validate and troubleshoot JSON indexing configurations.

---

## Flexible Matching with `JSON_CONTAINS`

The new `JSON_CONTAINS` predicate checks whether one JSON document is semantically contained within another.

Example: Find all orders that include a line with `productId = 712`.

```sql
SELECT OrderNumber
FROM Sales.JsonDemo
WHERE JSON_CONTAINS(OrderDetails, '{ "productId": 712 }')
```

This works even for nested arrays and complex objects, avoiding brittle path expressions.

You can also match nested properties:

```sql
-- Match a full subobject
SELECT OrderNumber
FROM Sales.JsonDemo
WHERE JSON_CONTAINS(OrderDetails, '{ "lines": [ { "productId": 712 } ] }')
```

This is ideal for validating JSON document structure or presence of subcomponents.

---

## Array Wildcards in Path Expressions

SQL Server 2025 supports the ANSI SQL `[*]` wildcard for arrays, enabling dynamic traversal without positional indexes.

List all product IDs in every order:

```sql
SELECT OrderNumber,
       JSON_VALUE(OrderDetails, '$.lines[*].productId') AS AnyProductId
FROM Sales.JsonDemo
```

Note that `JSON_VALUE(..., '$.lines[*].productId')` returns the first match only. Use `OPENJSON` for full expansion:

```sql
SELECT OrderNumber, v.value AS ProductId
FROM Sales.JsonDemo
CROSS APPLY OPENJSON(OrderDetails, '$.lines') WITH (productId int '$.productId') AS v
```

The wildcard is most useful for filtering and scalar extraction without knowing the array length.

---

## Forcing Arrays with `WITH ARRAY WRAPPER`

`JSON_QUERY` now supports `WITH ARRAY WRAPPER` to guarantee that results are returned as an array, even if a single value is selected.

This is useful for downstream APIs expecting JSON arrays.

```sql
SELECT JSON_QUERY(OrderDetails, '$.lines[0]' WITH ARRAY WRAPPER) AS FirstLineAsArray
FROM Sales.JsonDemo
```

This ensures consistency regardless of the number of matched items.

---

## Type-Safe Extraction with `RETURNING` Clause

The new `RETURNING` clause in `JSON_VALUE` allows explicit type casting inline.

Get the first quantity as an integer:

```sql
SELECT JSON_VALUE(OrderDetails, '$.lines[0].quantity' RETURNING int) AS FirstQuantity
FROM Sales.JsonDemo
```

This removes the need for external `CAST()` calls and provides better validation and error reporting.

---

## Summary

This lab explored the newest JSON capabilities in SQL Server 2025:

* Strong typing, validation, and partial updates via the `json` data type
* Declarative indexing and metadata discovery
* Flexible semantic containment with `JSON_CONTAINS`
* More expressive queries with wildcards and type-safe extraction
* Cleaner interop with `WITH ARRAY WRAPPER`

Together, these features make SQL Server a powerful engine for both structured and semi-structured data workloads.

> Next steps: Experiment with these features in your own tables or extend this demo to include nested objects, arrays of arrays, and integration with full-text search or AI workloads.
