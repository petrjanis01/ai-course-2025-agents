using System.ComponentModel;
using System.Data;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;
using TransactionManagement.Api.Models.Enums;

namespace TransactionManagement.Api.Services.Agents;

/// <summary>
/// Database agent s možností generovat a vykonávat SQL dotazy (Text-to-SQL).
/// Agent dostane znalost databázového schématu a může dynamicky sestavovat dotazy.
/// </summary>
public class DatabaseFunctions
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseFunctions> _logger;

    // Schema definice pro LLM
    private const string DatabaseSchema = @"
DATABASE SCHEMA - Transaction Management System

Table: Transactions
Columns:
- Id (GUID, Primary Key) - unique identifier transakce
- Description (string) - popis transakce (např. 'Faktura za služby', 'Platba za materiál')
- Amount (decimal) - částka v Kč (Czech Crowns)
- CompanyId (string) - IČO společnosti (8 číslic)
- CompanyName (string, nullable) - název společnosti
- TransactionType (int) - typ transakce, hodnoty: 'Income' (příjem - 0) nebo 'Expense' (výdaj - 1)
- TransactionDate (DateTime) - datum transakce (kdy byla transakce provedena)
- CreatedAt (DateTime) - datum vytvoření záznamu v systému
- UpdatedAt (DateTime) - datum poslední aktualizace záznamu

Table: Attachments
Columns:
- Id (GUID, Primary Key) - unique identifier přílohy
- TransactionId (GUID, Foreign Key) - odkaz na transakci (Transactions.Id)
- FileName (string) - název souboru (např. 'faktura_2025_01.pdf')
- FilePath (string) - cesta k souboru v storage
- Category (int, nullable) - kategorie dokumentu: 'invoice' (faktura - 1), 'contract' (smlouva - 2), 'PurchaseOrder' (objednávka - 3), 'unknown' (neznámý - 0)
- ProcessingStatus (int) - stav zpracování: 'pending' - 0, 'processing' - 1, 'completed' - 2, 'failed' - 3 
- CreatedAt (DateTime) - datum vytvoření přílohy v systému
- ProcessedAt (DateTime, nullable) - datum dokončení zpracování přílohy

Relationships:
- Transaction má 0..N Attachments (one-to-many)
- JOIN: FROM Transactions t LEFT JOIN Attachments a ON t.Id = a.TransactionId

Important Notes:
- All amounts are in Czech Crowns (Kč)
- For date filtering use: WHERE TransactionDate >= '2025-01-01' AND TransactionDate < '2025-02-01'
- Czech month names: 1=leden, 2=únor, 3=březen, 4=duben, 5=květen, 6=červen, 7=červenec, 8=srpen, 9=září, 10=říjen, 11=listopad, 12=prosinec

Example Queries:
-- Count all transactions
SELECT COUNT(*) as TotalCount FROM ""Transactions""

-- Sum by transaction type
SELECT ""TransactionType"", SUM(""Amount"") as Total, COUNT(*) as Count
FROM ""Transactions""
GROUP BY ""TransactionType""

-- Top 5 companies by transaction count
SELECT ""CompanyName"", COUNT(*) as TransactionCount, SUM(""Amount"") as TotalAmount
FROM ""Transactions""
WHERE ""CompanyName"" IS NOT NULL
GROUP BY ""CompanyName""
ORDER BY TransactionCount DESC
LIMIT 5

-- Monthly income summary for 2025
SELECT EXTRACT(MONTH FROM ""TransactionDate"") as Month, SUM(""Amount"") as TotalIncome
FROM ""Transactions""
WHERE EXTRACT(YEAR FROM ""TransactionDate"") = 2025 AND ""TransactionType"" = 'Income'
GROUP BY EXTRACT(MONTH FROM ""TransactionDate"")
ORDER BY Month

-- Transactions with invoice attachments
SELECT t.""Id"", t.""Description"", t.""Amount"", COUNT(a.""Id"") as AttachmentCount
FROM ""Transactions"" t
LEFT JOIN ""Attachments"" a ON t.""Id"" = a.""TransactionId""
WHERE a.""Category"" = 'invoice'
GROUP BY t.""Id"", t.""Description"", t.""Amount""
";

    public DatabaseFunctions(
        ApplicationDbContext context,
        ILogger<DatabaseFunctions> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Description(@"Get database schema information for Transactions and Attachments tables.
Use this FIRST to understand the database structure before constructing SQL queries.
Returns: detailed schema with column descriptions, relationships, and example queries.")]
    public string GetDatabaseSchema()
    {
        _logger.LogInformation("=== GetDatabaseSchema called ===");
        return DatabaseSchema;
    }

    [Description(@"Execute a SQL query against the Transactions database and return results as JSON.
This function allows querying transaction data with full SQL capabilities.

IMPORTANT RULES:
- Only SELECT queries are allowed (no INSERT, UPDATE, DELETE, DROP, etc.)
- Always include LIMIT to avoid returning too much data (e.g., LIMIT 100)
- Use PostgreSQL syntax with double-quoted table and column names
- TransactionType must be compared as string: WHERE ""TransactionType"" = 'Income'
- For date filtering use: WHERE ""TransactionDate"" >= '2025-01-01'
- Always validate your SQL before calling this function

The function will return results as JSON array of objects.")]
    public async Task<string> ExecuteSqlQuery(
        [Description(@"SQL SELECT query to execute. Must be valid SELECT statement with PostgreSQL syntax.
Examples:
- SELECT COUNT(*) FROM ""Transactions"" WHERE ""TransactionType"" = 'Income'
- SELECT * FROM ""Transactions"" ORDER BY ""TransactionDate"" DESC LIMIT 10
- SELECT ""CompanyName"", SUM(""Amount"") FROM ""Transactions"" GROUP BY ""CompanyName""")]
        string sqlQuery)
    {
        try
        {
            // Security: Validate query
            if (!IsValidSelectQuery(sqlQuery))
            {
                var error = "Error: Only SELECT queries are allowed. Query must start with SELECT and not contain dangerous keywords (DROP, DELETE, INSERT, UPDATE, etc.).";
                _logger.LogWarning("Invalid SQL query rejected: {Query}", sqlQuery);
                return error;
            }

            _logger.LogInformation("=== Executing SQL query ===");
            _logger.LogInformation("Query: {Query}", sqlQuery);

            // Execute raw SQL query
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sqlQuery;
            command.CommandType = CommandType.Text;

            await _context.Database.OpenConnectionAsync();

            using var result = await command.ExecuteReaderAsync();

            // Convert result to JSON
            var rows = new List<Dictionary<string, object?>>();

            while (await result.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < result.FieldCount; i++)
                {
                    var value = result.GetValue(i);
                    row[result.GetName(i)] = value is DBNull ? null : value;
                }
                rows.Add(row);
            }

            var jsonResult = System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            _logger.LogInformation("Query returned {Count} rows", rows.Count);
            _logger.LogDebug("Result: {Result}", jsonResult);

            return jsonResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL query: {Query}", sqlQuery);
            return $"Error executing query: {ex.Message}\n\nMake sure your SQL syntax is correct and matches the database schema (PostgreSQL with double-quoted names).";
        }
    }

    [Description(@"Get aggregated transaction statistics - a convenience function for common queries.
Returns total count, income/expense breakdown, and net balance.
This is faster than constructing SQL for basic statistics.")]
    public async Task<string> GetTransactionStatistics(
        [Description("Optional date from (YYYY-MM-DD format)")] string? dateFrom = null,
        [Description("Optional date to (YYYY-MM-DD format)")] string? dateTo = null,
        [Description("Optional company ID (IČO)")] string? companyId = null)
    {
        _logger.LogInformation("=== GetTransactionStatistics called ===");
        _logger.LogInformation("Filters: dateFrom={DateFrom}, dateTo={DateTo}, companyId={CompanyId}",
            dateFrom, dateTo, companyId);

        var query = _context.Transactions.AsQueryable();

        if (DateTime.TryParse(dateFrom, out var from))
        {
            query = query.Where(t => t.TransactionDate >= from);
        }

        if (DateTime.TryParse(dateTo, out var to))
        {
            query = query.Where(t => t.TransactionDate <= to);
        }

        if (!string.IsNullOrEmpty(companyId))
        {
            query = query.Where(t => t.CompanyId == companyId);
        }

        var stats = await query
            .GroupBy(t => 1) // Group all for aggregation
            .Select(g => new
            {
                TotalCount = g.Count(),
                TotalIncome = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount),
                TotalExpense = g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount),
                NetBalance = g.Where(t => t.TransactionType == TransactionType.Income).Sum(t => t.Amount) -
                           g.Where(t => t.TransactionType == TransactionType.Expense).Sum(t => t.Amount),
                IncomeCount = g.Count(t => t.TransactionType == TransactionType.Income),
                ExpenseCount = g.Count(t => t.TransactionType == TransactionType.Expense),
                AverageAmount = g.Average(t => t.Amount)
            })
            .FirstOrDefaultAsync();

        if (stats == null)
        {
            var emptyStats = new
            {
                TotalCount = 0,
                TotalIncome = 0m,
                TotalExpense = 0m,
                NetBalance = 0m,
                IncomeCount = 0,
                ExpenseCount = 0,
                AverageAmount = 0m
            };

            _logger.LogInformation("No transactions found for given filters");
            return System.Text.Json.JsonSerializer.Serialize(emptyStats, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        _logger.LogInformation("Statistics: TotalCount={Count}, NetBalance={Balance}",
            stats.TotalCount, stats.NetBalance);

        return System.Text.Json.JsonSerializer.Serialize(stats, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Validace, že dotaz je bezpečný SELECT dotaz
    /// </summary>
    private bool IsValidSelectQuery(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
            return false;

        var normalizedQuery = sqlQuery.Trim().ToUpperInvariant();

        // Must start with SELECT
        if (!normalizedQuery.StartsWith("SELECT"))
            return false;

        // Dangerous keywords that could modify data or structure
        var dangerousKeywords = new[]
        {
            "DROP", "DELETE", "INSERT", "UPDATE", "TRUNCATE",
            "ALTER", "CREATE", "EXEC", "EXECUTE", "SCRIPT",
            "xp_", "sp_",  // SQL Server stored procedures
            "INTO OUTFILE", "INTO DUMPFILE", // MySQL file operations
            "BULK INSERT", "OPENROWSET" // SQL Server bulk operations
        };

        foreach (var keyword in dangerousKeywords)
        {
            if (normalizedQuery.Contains(keyword))
            {
                return false;
            }
        }

        return true;
    }
}
