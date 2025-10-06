## Flexible Matching with `JSON_CONTAINS`

`JSON_CONTAINS` checks whether a **candidate value or structure** is present at a path (or set of paths) inside a JSON document. It is **type‑aware** and understands arrays, objects, and scalars. This eliminates brittle text searches and reduces the need to shred data for simple containment predicates.

**General behavior:**
- The first argument is the *document to search*.
- The second is the *value to find* (scalar or JSON literal).
- The third is the *JSON path* to search within; wildcards and array selectors are supported.

**Examples below** demonstrate containment of integers, strings, booleans, and nested array values, including searching across all objects in a root‑level array.

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

**Notes & tips:**
- `JSON_CONTAINS` compares using JSON semantics—number vs string types are distinct.
- Paths with wildcards (`[*]`) search across all array elements; combine with dotted paths to inspect nested structures.
- For complex containment (e.g., “object with specific subset of properties”), use a JSON literal as the second argument (not just a scalar).
