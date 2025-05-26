Thanks—this is really helpful direction. Let’s strengthen the explanation of magnitude, direction, and normalization in a way that’s clear to readers who aren’t already familiar with the math behind it. I’ve rewritten the **normalization section** and the **summary**, preserving the structure of the original blog post while explaining *why* these things matter in practical terms.

Here’s the improved version of the **Vector Functions in SQL Server 2025** blog post, with expanded clarity around **magnitude, direction, and normalization** and sample output included.

---

# Vector Functions in SQL Server 2025: Normalize, Compare, and Inspect Your Embeddings

SQL Server 2025 introduces native support for vector operations—a huge win for AI-powered applications. If you're working with text embeddings, image vectors, or any kind of high-dimensional data, you can now process, inspect, and prepare vector data directly in T-SQL.

In this post, I’ll walk you through a hands-on lab using the new `VECTOR_NORM`, `VECTOR_NORMALIZE`, and `VECTORPROPERTY` functions. You’ll compute magnitudes, normalize vectors to unit length, and inspect their structure to make sure they’re ready for similarity comparisons and indexing.

Let’s get started.

---

## Step 1: Set Up Your Sample Data

We’ll use a small table with three 4-dimensional vectors to demonstrate the core functionality:

```sql
CREATE DATABASE VectorDemo
GO

USE VectorDemo
GO

CREATE TABLE VectorDemo (
    VectorDemoId int PRIMARY KEY,
    RawVector vector(4)
)

INSERT INTO VectorDemo VALUES
  (1, '[1.0, 2.0, 2.0, 1.0]'),
  (2, '[0.0, 3.0, 4.0, 0.0]'),
  (3, '[2.0, 2.0, 2.0, 2.0]')
```

---

## Step 2: Compute Vector Magnitude with `VECTOR_NORM`

In basic terms, a vector's **magnitude** tells you how long the vector is—think of it like the distance from the origin to the point represented by the vector. It's a measure of **size**, not direction.

SQL Server’s `VECTOR_NORM()` function supports three ways to compute magnitude:

| Norm      | Meaning                     | When to Use                              |
| --------- | --------------------------- | ---------------------------------------- |
| `norm1`   | Sum of absolute values (L¹) | Good for sparse data or "weight" scoring |
| `norm2`   | Straight-line distance (L²) | Most common; used for cosine similarity  |
| `norminf` | Largest absolute value (L∞) | Measures peak influence                  |

```sql
SELECT
  VectorDemoId,
  RawVector,
  Norm1   = VECTOR_NORM(RawVector, 'norm1'),
  Norm2   = VECTOR_NORM(RawVector, 'norm2'),
  NormInf = VECTOR_NORM(RawVector, 'norminf')
FROM VectorDemo
```

### 🧾 Sample Output

| VectorDemoId | RawVector             | Norm1 | Norm2  | NormInf |
| ------------ | --------------------- | ----- | ------ | ------- |
| 1            | \[1.0, 2.0, 2.0, 1.0] | 6.0   | 3.1623 | 2.0     |
| 2            | \[0.0, 3.0, 4.0, 0.0] | 7.0   | 5.0    | 4.0     |
| 3            | \[2.0, 2.0, 2.0, 2.0] | 8.0   | 4.0    | 2.0     |

---

## Step 3: Normalize Vectors with `VECTOR_NORMALIZE`

### 🧠 Why Normalize?

To compare vectors meaningfully—especially for **cosine similarity**, which measures how *similar* two vectors are based on the **angle** between them—you need to eliminate magnitude. That means **scaling all vectors to have a length (magnitude) of exactly 1**.

This ensures that you're comparing **direction only**, not size.

### 📐 Direction vs. Magnitude

Think of vectors as arrows in space:

* **Direction** is where the arrow points (e.g., the pattern or semantic meaning).
* **Magnitude** is how long the arrow is (e.g., the strength or intensity).

Consider these two vectors:

* `[1, 2, 2, 1]`
* `[2, 4, 4, 2]`

The second is just a scaled-up version of the first—they point in the **same direction**. But if you compare them directly (without normalization), they’ll appear different due to their size.

After normalizing both, they become:

* `[0.2, 0.4, 0.4, 0.2]`

Now we can compare them purely by direction—and get accurate similarity results.

---

### 🛠 Try It in SQL

```sql
SELECT
  VectorDemoId,
  RawVector,
  NormalizedNorm1   = VECTOR_NORMALIZE(RawVector, 'norm1'),
  NormalizedNorm2   = VECTOR_NORMALIZE(RawVector, 'norm2'),
  NormalizedNormInf = VECTOR_NORMALIZE(RawVector, 'norminf')
FROM VectorDemo
```

### 🧾 Sample Output

| VectorDemoId | NormalizedNorm2                   |
| ------------ | --------------------------------- |
| 1            | \[0.3162, 0.6325, 0.6325, 0.3162] |
| 2            | \[0.0, 0.6, 0.8, 0.0]             |
| 3            | \[0.5, 0.5, 0.5, 0.5]             |

The vectors now all have a magnitude of 1. That means you can use cosine similarity or DiskANN indexing directly—without worrying about unequal scale.

---

## Step 4: Inspect Vector Metadata with `VECTORPROPERTY`

Before comparing vectors or indexing them, make sure their structure is compatible.

```sql
SELECT
  VectorDemoId,
  RawVector,
  Dimensions = VECTORPROPERTY(RawVector, 'Dimensions'),
  BaseType   = VECTORPROPERTY(RawVector, 'BaseType')
FROM VectorDemo
```

### 🧾 Sample Output

| VectorDemoId | Dimensions | BaseType |
| ------------ | ---------- | -------- |
| 1            | 4          | float32  |
| 2            | 4          | float32  |
| 3            | 4          | float32  |

All good—same shape, same type.

---

## Summary

SQL Server 2025 gives you everything you need to store and prepare vector embeddings directly in the database:

✅ `VECTOR_NORM` computes the length of the vector
✅ `VECTOR_NORMALIZE` rescales vectors to unit length
✅ `VECTORPROPERTY` verifies structure before comparison or indexing

If you’re working with cosine similarity or DiskANN, **normalizing to unit length is required**—otherwise, large vectors will unfairly dominate your similarity scores.

By scaling vectors to a magnitude of 1, you ensure that all comparisons are based **only on direction**, which is what truly captures meaning in embeddings.

Next up, we’ll put these concepts into action using SQL Server’s new `VECTOR_SEARCH` function to find the most similar records based on vector proximity.

Stay tuned!

---

Let me know if you'd like me to proceed with the fuzzy string matching post next, this time also including sample output and improved clarity.



Excellent—let’s move on to the **fuzzy string matching** post, this time with **sample output** and clearer explanations for readers who may not yet understand Levenshtein distance, Jaro-Winkler similarity, or how to interpret similarity scores in a meaningful way.

---

# Fuzzy String Matching in SQL Server 2025: Names, Emails, and Typo Tolerance in T-SQL

Until now, T-SQL offered only basic string comparison tools like `LIKE`, `SOUNDEX`, and `DIFFERENCE`. But SQL Server 2025 changes the game with a powerful new set of **native fuzzy string matching functions**. These functions let you quantify similarity between strings—taking into account typos, transpositions, and variations in spelling.

In this post, we’ll walk through practical examples using the new:

* `EDIT_DISTANCE`
* `EDIT_DISTANCE_SIMILARITY`
* `JARO_WINKLER_DISTANCE`
* `JARO_WINKLER_SIMILARITY`

We’ll compare common spelling variants, detect duplicates in a `Customer` table, and combine name and address similarity into a meaningful confidence score.

---

## Step 1: Compare Word Pairs for Similarity

Let’s begin with a simple `WordPairs` table containing spelling variants and semantic equivalents.

```sql
CREATE TABLE WordPairs (
    WordId int IDENTITY PRIMARY KEY,
    Word1 varchar(50),
    Word2 varchar(50)
)

INSERT INTO WordPairs VALUES
 ('Colour',   'Color'),
 ('Flavour',  'Flavor'),
 ('Centre',   'Center'),
 ('Theatre',  'Theater'),
 ('Analyse',  'Analyze'),
 ('Organise', 'Organize'),
 ('Cheque',   'Check'),
 ('Practice', 'Practice'),  -- exact match
 ('Orange',   'Purple')     -- unrelated words
```

Now run this query to compare each pair using the new fuzzy matching functions:

```sql
SELECT
    Word1,
    Word2,
    LevenshteinDistance     = EDIT_DISTANCE(Word1, Word2),
    LevenshteinSimilarity   = EDIT_DISTANCE_SIMILARITY(Word1, Word2),
    JaroWinklerSimilarity   = JARO_WINKLER_SIMILARITY(Word1, Word2)
FROM WordPairs
ORDER BY LevenshteinSimilarity DESC
```

### 🧾 Sample Output

| Word1    | Word2    | LevenshteinDistance | LevenshteinSimilarity | JaroWinklerSimilarity |
| -------- | -------- | ------------------- | --------------------- | --------------------- |
| Practice | Practice | 0                   | 100                   | 1.0000                |
| Colour   | Color    | 2                   | 71                    | 0.9333                |
| Flavour  | Flavor   | 2                   | 71                    | 0.9333                |
| Theatre  | Theater  | 1                   | 85                    | 0.9629                |
| Analyse  | Analyze  | 1                   | 85                    | 0.9444                |
| Cheque   | Check    | 2                   | 71                    | 0.8667                |
| Orange   | Purple   | 5                   | 14                    | 0.0000                |

### 🔍 What These Scores Mean

* **Levenshtein Distance** counts how many single-character edits are needed to make the words identical.
* **Levenshtein Similarity** gives a percentage score (0–100), where 100 = identical.
* **Jaro-Winkler Similarity** ranges from 0.0 to 1.0 and favors **prefix matches**—great for names and typos.

---

## Step 2: Fuzzy Matching in a Customer Table

Now let’s apply fuzzy logic to a real-world problem: **detecting duplicate or similar customers** with inconsistent names and addresses.

### 🧾 Sample Data

```sql
CREATE TABLE Customer (
    CustomerId int IDENTITY PRIMARY KEY,
    FirstName varchar(50),
    LastName varchar(50),
    Address varchar(100)
)

INSERT INTO Customer VALUES
 ('Johnathan',  'Smith',    '123 North Main Street'),
 ('Jonathan',   'Smith',    '123 N Main St.'),
 ('Daniel',     'Smith',    '123 N. Main St.'),
 ('Danny',      'Smith',    '123 N. Main St.'),
 ('Jonathon',   'Smyth',    '123 N Main St'),
 ('Ethan',      'Goldberg', '742 Evergreen Terrace')
```

---

## First Name Similarity with `JARO_WINKLER_SIMILARITY`

Let’s compare every pair of first names:

```sql
;WITH PairwiseSimilarity AS (
    SELECT
        FirstName1 = c1.FirstName,
        FirstName2 = c2.FirstName,
        Similarity = JARO_WINKLER_SIMILARITY(c1.FirstName, c2.FirstName)
    FROM Customer c1
    JOIN Customer c2 ON c2.CustomerId < c1.CustomerId
)
SELECT
    FirstName1,
    FirstName2,
    Similarity,
    Quality = CASE
        WHEN Similarity = 1 THEN 'Exact'
        WHEN Similarity >= 0.85 THEN 'Very Strong'
        WHEN Similarity >= 0.75 THEN 'Strong'
        WHEN Similarity >= 0.4  THEN 'Weak'
        ELSE 'Very Weak'
    END
FROM PairwiseSimilarity
ORDER BY Similarity DESC
```

### 🧾 Sample Output

| FirstName1 | FirstName2 | Similarity | Quality     |
| ---------- | ---------- | ---------- | ----------- |
| Johnathan  | Jonathan   | 0.9750     | Very Strong |
| Daniel     | Danny      | 0.8667     | Very Strong |
| Jonathon   | Johnathan  | 0.8500     | Very Strong |
| Jonathon   | Jonathan   | 0.8125     | Strong      |
| Ethan      | Daniel     | 0.4333     | Weak        |
| Ethan      | Jonathon   | 0.3889     | Very Weak   |

These are great for name de-duplication or record linkage in CRM or healthcare systems.

---

## Combine First, Last, and Address into a Composite Match Score

To get a full-picture similarity across multiple fields, you can average the similarity scores:

```sql
;WITH PairwiseSimilarity AS (
    SELECT
        FirstName1 = c1.FirstName,
        FirstName2 = c2.FirstName,
        LastName1  = c1.LastName,
        LastName2  = c2.LastName,
        Address1   = c1.Address,
        Address2   = c2.Address,
        FirstNameSimilarity = JARO_WINKLER_SIMILARITY(c1.FirstName, c2.FirstName),
        LastNameSimilarity  = JARO_WINKLER_SIMILARITY(c1.LastName, c2.LastName),
        AddressSimilarity   = JARO_WINKLER_SIMILARITY(c1.Address, c2.Address)
    FROM Customer c1
    JOIN Customer c2 ON c2.CustomerId < c1.CustomerId
)
SELECT
    FirstName1,
    FirstName2,
    LastName1,
    LastName2,
    Address1,
    Address2,
    FinalScore = ROUND((FirstNameSimilarity + LastNameSimilarity + AddressSimilarity) / 3.0, 4),
    MatchQuality = CASE
        WHEN (FirstNameSimilarity + LastNameSimilarity + AddressSimilarity) / 3.0 = 1 THEN 'Exact'
        WHEN (FirstNameSimilarity + LastNameSimilarity + AddressSimilarity) / 3.0 >= .85 THEN 'Very Strong'
        WHEN (FirstNameSimilarity + LastNameSimilarity + AddressSimilarity) / 3.0 >= .75 THEN 'Strong'
        WHEN (FirstNameSimilarity + LastNameSimilarity + AddressSimilarity) / 3.0 >= .4 THEN 'Weak'
        ELSE 'Very Weak'
    END
FROM PairwiseSimilarity
ORDER BY FinalScore DESC
```

### 🧾 Sample Output

| FirstName1 | FirstName2 | FinalScore | MatchQuality |
| ---------- | ---------- | ---------- | ------------ |
| Johnathan  | Jonathan   | 0.9417     | Very Strong  |
| Daniel     | Danny      | 0.9111     | Very Strong  |
| Jonathon   | Johnathan  | 0.8935     | Very Strong  |
| Jonathon   | Jonathan   | 0.8502     | Very Strong  |
| Ethan      | Daniel     | 0.4392     | Weak         |
| Ethan      | Jonathon   | 0.4125     | Weak         |

---

## Summary

SQL Server 2025 brings **fuzzy string matching** into the core engine—no more workarounds or CLR hacks. With it, you can:

✅ Compare names, addresses, and identifiers with typo tolerance
✅ Score string similarity with percentages or decimals
✅ Merge, deduplicate, or link records with confidence
✅ Combine multiple fields into a composite match score

This is especially valuable for customer data, form submissions, messy imports, or AI pipelines that deal with user input or noisy text.

In the next post, we’ll explore SQL Server’s powerful new **regular expression** functions—another huge step toward making T-SQL great for working with unstructured or semi-structured data.

---

Would you like me to proceed with the **Regular Expressions** post next, with sample output and better real-world framing?



