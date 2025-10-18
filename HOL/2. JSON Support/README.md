# Exploring New JSON Capabilities in SQL Server 2025

SQL Server 2025 introduces several powerful enhancements to its native JSON support, addressing long-standing gaps in performance, usability, and standards compliance. These features make JSON a first-class citizen alongside traditional relational data types.

This hands-on lab walks you through the new JSON features introduced in SQL Server 2025:

* Native `json` data type
* In-place updates using the `.modify()` method
* Native JSON indexes `CREATE JSON INDEX`
* JSON path expression array enhancements
* JSON aggregates
* The new `JSON_CONTAINS` function for containment checks

## JSON Formatter

SQL Server Management Studio (SSMS) displays raw JSON that can be hard to read. To improve readability, you can use the JSON Formatter utility. This online tool formats JSON strings returned by SQL queries into a more human-friendly format, making it easier to view the data structure than it is in SSMS.

### Try it out

* Copy the following JSON string:
   ```json
   {"name":"John","age":30,"city":"New York","hobbies":["reading","traveling","swimming"]}
   ```
* Point your browser to the JSON Formatter tool at <a href="https://www.toptal.com/developers/json-formatter" target="_blank">https://www.toptal.com/developers/json-formatter</a>.
* Paste the JSON string into the input box on the JSON Formatter page
* Observe how the tool formats the JSON string into a more readable structure.

It's recommended that you keep this tool open in a separate browser tab while working through the JSON labs for easy access to format and view JSON data.

___

▶ [Lab: Native JSON Data Type](https://github.com/lennilobel/sql2025-workshop-hol-orlando2025/blob/main/HOL/2.%20JSON%20Support/1.%20Native%20JSON%20Data%20Type.md)
