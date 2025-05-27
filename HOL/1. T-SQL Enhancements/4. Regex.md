# Regular Expressions in SQL Server 2025

SQL Server 2025 introduces first-class support for regular expressions via a suite of new built-in functions that allow you to search, extract, count, split, and replace text patterns—all directly within T-SQL. This hands-on lab will guide you through each of these powerful capabilities using a realistic `Review` table of customer feedback.

In this lab, you will:

* Use `REGEXP_LIKE` to validate email and phone formats
* Use `REGEXP_COUNT` to analyze the presence of specific words or patterns
* Extract substrings with `REGEXP_SUBSTR`
* Locate patterns using `REGEXP_INSTR`
* Clean or transform data using `REGEXP_REPLACE`
* Extract multiple values using `REGEXP_MATCHES`
* Tokenize text with `REGEXP_SPLIT_TO_TABLE`

By the end, you'll be equipped to perform advanced text parsing and validation scenarios natively in T-SQL—no CLR or external libraries required.

## Set Up the Review Table

We'll begin by creating a table named `Review` containing simulated user reviews, some of which include invalid emails, inconsistent phone numbers, or hashtags.

```sql
USE MyDB
GO

DROP TABLE IF EXISTS Review

CREATE TABLE Review(
    ReviewId    int IDENTITY PRIMARY KEY,
    Name        varchar(50) NOT NULL,
    Email       varchar(150),
    Phone       varchar(20),
    ReviewText  varchar(1000)
)
GO

INSERT INTO Review
 (Name, Email, Phone, ReviewText) VALUES
 ('John Doe', 'john@contoso.com', '123-4567890', 'This product is excellent! I really like the build quality and design. #excellent #quality'),
 ('Alice Smith', 'alice@fabrikam@com', '234-567-81', 'Good value for money, but the software is terrible.'),
 ('Mary Jo Anne Erickson', 'mary.jo.anne@acme.co.uk', '456-789-1234', 'Poor battery life,   bad camera performance,   and poor build quality. #poor'),
 ('Max Wong', 'max@fabrikam.com', NULL, 'Excellent service from the support team, highly recommended!\t#goodservice #recommended'),
 ('Bob Johnson', 'bob.fabrikam.net', '345-678-9012', 'The product is good, but delivery was delayed.\r\nOverall, decent experience.'),
 ('Terri S Duffy', 'terri.duffy@acme.com', '678-901-2345', 'Battery life is weak, camera quality is poor. #aweful'),
 ('Eve Jones', NULL, '456-789-0123', 'I love this product, it''s great! #fantastic #amazing'),
 ('Charlie Brown', 'charlie@contoso.co.in', '587-890-1234', 'I hate this product, it''s terrible!')
```

## Validate Email and Phone with `REGEXP_LIKE`

The `REGEXP_LIKE` function is used in `WHERE` clauses to test whether a column matches a given regular expression. Below, we check for valid email and phone formats.

```sql
-- Find reviews with valid email addresses
SELECT ReviewId, Name, Email
FROM Review
WHERE REGEXP_LIKE(Email, '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$')

-- Find reviews with emails ending in ".com"
SELECT ReviewId, Name, Email
FROM Review
WHERE REGEXP_LIKE(Email, '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.com$')

-- Find reviews with properly formatted phone numbers (e.g., 123-456-7890)
SELECT ReviewId, Name, Phone
FROM Review
WHERE REGEXP_LIKE(Phone, '^(\d{3})-(\d{3})-(\d{4})$')
```

### Expected Results

You should only see rows where emails follow the correct format and where phone numbers are fully structured with hyphens and correct digit groups. Malformed addresses like `bob.fabrikam.net` or `alice@fabrikam@com` will be excluded.

## Analyze Patterns with `REGEXP_COUNT`

Use `REGEXP_COUNT` when you want to count the number of times a pattern appears in a column or expression. It's useful for validation or keyword analysis.

```sql
-- Show if each email and phone is valid (1 if valid, 0 if not)
SELECT
  ReviewId,
  Name,
  Email,
  IsEmailValid = CASE WHEN REGEXP_COUNT(Email, '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$') = 1 THEN 1 ELSE 0 END,
  Phone,
  IsPhoneValid = CASE WHEN REGEXP_COUNT(Phone, '^(\d{3})-(\d{3})-(\d{4})$') = 1 THEN 1 ELSE 0 END
FROM Review

-- Count the number of vowels in each name
SELECT Name,
       VowelCount = REGEXP_COUNT(Name, '[AEIOU]', 1, 'i')
FROM Review

-- Count positive and negative sentiment keywords in ReviewText
SELECT
  Name,
  ReviewText,
  GoodSentimentWordCount = REGEXP_COUNT(ReviewText, '\b(excellent|great|good|love|like)\b', 1, 'i'),
  BadSentimentWordCount  = REGEXP_COUNT(ReviewText, '\b(bad|poor|terrible|hate)\b', 1, 'i')
FROM Review
```

### Expected Results

You’ll see how many valid fields are present, how many vowels appear in names, and which reviews use words like "excellent" or "terrible". This gives a quantitative look at review sentiment.

## Extract Substrings with `REGEXP_SUBSTR`

Use `REGEXP_SUBSTR` to extract parts of strings that match a regular expression pattern. For example, you can isolate email domain names.

```sql
-- Extract domain name from each email
SELECT
  ReviewId,
  Name,
  Email,
  DomainName = REGEXP_SUBSTR(Email, '@(.+)$', 1, 1, 'c', 1)
FROM Review

-- Show how many users have email addresses from each domain
;WITH DomainNameCte AS (
  SELECT DomainName = REGEXP_SUBSTR(Email, '@(.+)$', 1, 1, 'c', 1)
  FROM Review
)
SELECT DomainName, DomainCount = COUNT(*)
FROM DomainNameCte
GROUP BY DomainName
ORDER BY DomainName
```

### Expected Results

You’ll extract domain names like `contoso.com` or `acme.co.uk`, and see a breakdown of how many reviews use each domain.

## Locate Patterns with `REGEXP_INSTR`

Use `REGEXP_INSTR` to find the character position where a pattern occurs. It's useful for indexing into strings or diagnosing structural issues.

```sql
-- Find position of @ and first . after it
SELECT
  ReviewId,
  Name,
  Email,
  At         = REGEXP_INSTR(Email, '@'),
  DotAfterAt = REGEXP_INSTR(Email, '@[^@]*?(\.)', 1, 1, 0, 'c', 1)
FROM Review
```

### Expected Results

Each row will show the numeric index of the `@` and the first dot after it. This can help verify email structure and debug malformed addresses.

## Transform Strings with `REGEXP_REPLACE`

Use `REGEXP_REPLACE` to modify strings based on pattern matches. For instance, you can strip out middle names or punctuation.

```sql
-- Remove middle names from Name column
SELECT
  ReviewId,
  Name,
  ShortName = REGEXP_REPLACE(Name, '^(\S+)\s+.*\s+(\S+)$', '\1 \2', 1, 1, 'i')
FROM Review
```

### Expected Results

Names like "Mary Jo Anne Erickson" will become "Mary Erickson", removing the middle names and leaving only first and last.

## Extract Multiple Matches with `REGEXP_MATCHES`

Use `REGEXP_MATCHES` to return a table of all matches of a pattern in a string. You can apply it with `CROSS APPLY` to explode hashtags or keywords from longer text fields.

```sql
-- Extract hashtags from each review
SELECT
  r.ReviewId,
  r.ReviewText,
  m.*
FROM Review AS r
CROSS APPLY REGEXP_MATCHES(r.ReviewText, '#([A-Za-z0-9_]+)') AS m
```

### Why `CROSS APPLY`?

The `REGEXP_MATCHES` function returns multiple rows for each input row (one for each match). `CROSS APPLY` joins these result sets to the outer `Review` rows, preserving the relationship between the review and its extracted hashtags.

### Expected Results

Each hashtag will appear in its own row, alongside the `ReviewId` and original `ReviewText`.

## Split Text with `REGEXP_SPLIT_TO_TABLE`

Use `REGEXP_SPLIT_TO_TABLE` to tokenize long strings into individual components. This is useful for word frequency analysis, indexing, or keyword detection.

```sql
-- Split review text into words
SELECT
  r.ReviewId,
  r.ReviewText,
  WordText     = s.value,
  WordPosition = s.ordinal
FROM Review AS r
CROSS APPLY REGEXP_SPLIT_TO_TABLE(r.ReviewText, '\s+') AS s

-- Strip punctuation after splitting
SELECT
  r.ReviewId,
  r.ReviewText,
  WordText     = REGEXP_REPLACE(s.value, '[^\w]', '', 1, 0, 'i'),
  WordPosition = s.ordinal
FROM Review AS r
CROSS APPLY REGEXP_SPLIT_TO_TABLE(r.ReviewText, '\s+') AS s
```

### Expected Results

Each word in the review will appear as its own row, with `WordPosition` indicating its order. The second query further removes punctuation for clean analysis.

## Summary

* Use `REGEXP_LIKE` for pattern validation in filters
* Use `REGEXP_COUNT` to analyze frequency of patterns
* Use `REGEXP_SUBSTR` and `REGEXP_INSTR` to extract and locate matches
* Use `REGEXP_REPLACE` for cleanup and transformation
* Use `REGEXP_MATCHES` and `REGEXP_SPLIT_TO_TABLE` with `CROSS APPLY` to explode text into rows

Regular expressions bring powerful pattern-matching capabilities directly into your SQL Server 2025 workflows. Use them to clean, validate, extract, and analyze text—all from inside T-SQL.
