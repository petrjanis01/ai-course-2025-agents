# 07 - Chat Agenti (Microsoft Agent Framework)

## Cíl
Implementovat ChatOrchestrator, DatabaseAgent a DocumentSearchAgent pomocí Microsoft Agent Framework pro RAG chat funkcionalitu.

## Prerekvizity
- Dokončený krok 06 (document processing)
- Microsoft Agent Framework balíčky nainstalovány

## Důležité informace o Agent Framework

**Microsoft Agent Framework** je nástupce Semantic Kernel a AutoGen, vyvinutý stejnými týmy. Kombinuje:
- **AutoGen**: Jednoduchá abstrakce pro single a multi-agent vzory
- **Semantic Kernel**: Enterprise funkcionalita (state management, telemetrie)
- **Nové funkce**: Workflows s explicitní kontrolou nad multi-agent orchestrací

**Klíčové rozdíly oproti Semantic Kernel:**
- Používá `IChatClient` místo `Kernel`
- Agenti se vytváří pomocí `CreateAIAgent()` 
- Tools (funkce) se registrují přímo při vytváření agenta
- Jednodušší syntax - `[Description]` místo `[KernelFunction]`
- Podpora Workflows pro složitou orchestraci

## Architektura

```
User Query
    ↓
ChatOrchestrator (main agent)
    ├─ Database Functions (jako AI tools)
    │   ├─ GetTransactionCount
    │   ├─ GetTransactionSum
    │   ├─ GetTransactionsList
    │   ├─ GetTransactionDetails
    │   └─ GetTopCompaniesByTransactionCount
    │
    └─ Document Search Functions (jako AI tools)
        ├─ SearchDocuments
        ├─ GetDocumentContent
        └─ CountDocumentsByCategory
    ↓
Response to User
```

**Poznámka:** Agent Framework automaticky volá správné funkce na základě uživatelského dotazu. Není potřeba ruční routing mezi agenty.

## Kroky implementace

### 1. NuGet balíčky

Přidej do `.csproj`:

```xml
<ItemGroup>
    <!-- Microsoft Agent Framework Core -->
    <PackageReference Include="Microsoft.Agents.AI" Version="1.0.0-*" />
    
    <!-- Microsoft.Extensions.AI pro IChatClient -->
    <PackageReference Include="Microsoft.Extensions.AI" Version="9.0.0-*" />
    <PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.0.0-*" />
    
    <!-- Alternativně: Semantic Kernel Ollama connector (kompatibilní) -->
    <!-- <PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.0.0-*" /> -->
</ItemGroup>
```

Nebo pomocí CLI:

```bash
dotnet add package Microsoft.Agents.AI --prerelease
dotnet add package Microsoft.Extensions.AI --prerelease
dotnet add package Microsoft.Extensions.AI.Ollama --prerelease
```

### 2. Chat Client Service

**Services/Agents/ChatClientService.cs:**
```csharp
using Microsoft.Extensions.AI;

namespace TransactionManagement.Api.Services.Agents;

public interface IChatClientService
{
    IChatClient CreateChatClient();
}

public class ChatClientService : IChatClientService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatClientService> _logger;

    public ChatClientService(
        IConfiguration configuration,
        ILogger<ChatClientService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public IChatClient CreateChatClient()
    {
        var baseUrl = _configuration["LLM:BaseUrl"] ?? "http://localhost:11434";
        var model = _configuration["LLM:Model"] ?? "llama3.1:8b";

        // Microsoft.Extensions.AI.Ollama connector
        var chatClient = new OllamaChatClient(new Uri(baseUrl), model);

        _logger.LogDebug("Created Ollama chat client with model {Model}", model);

        return chatClient;
    }
}
```

**appsettings.json:**
```json
{
  "LLM": {
    "BaseUrl": "http://localhost:11434",
    "Model": "llama3.1:8b"
  }
}
```

### 3. Database Functions (Text-to-SQL přístup)

**Proč Text-to-SQL?**
Místo předpřipravených funkcí pro konkrétní dotazy (GetTransactionCount, GetTransactionSum, atd.) použijeme **flexibilnější přístup**: agent dostane znalost databázového schématu a může **dynamicky generovat a vykonávat SQL dotazy** podle potřeb uživatele.

**Výhody:**
- ✅ Agent zvládne **libovolné** dotazy, nejen předpřipravené
- ✅ Podporuje komplexní SQL (JOINy, GROUP BY, agregace, subqueries)
- ✅ Méně kódu k údržbě (2-3 funkce místo 10+)
- ✅ Agent se adaptuje na nové požadavky bez změn kódu

**Services/Agents/DatabaseFunctions.cs:**
```csharp
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
- TransactionType (string) - typ transakce, hodnoty: 'Income' (příjem) nebo 'Expense' (výdaj)
- TransactionDate (DateTime) - datum transakce (kdy byla transakce provedena)
- CreatedAt (DateTime) - datum vytvoření záznamu v systému
- UpdatedAt (DateTime) - datum poslední aktualizace záznamu

Table: Attachments
Columns:
- Id (GUID, Primary Key) - unique identifier přílohy
- TransactionId (GUID, Foreign Key) - odkaz na transakci (Transactions.Id)
- FileName (string) - název souboru (např. 'faktura_2025_01.pdf')
- FilePath (string) - cesta k souboru v storage
- Category (string, nullable) - kategorie dokumentu: 'invoice' (faktura), 'contract' (smlouva), 'purchase_order' (objednávka), 'unknown' (neznámý)
- ProcessingStatus (string) - stav zpracování: 'pending', 'processing', 'completed', 'failed'
- CreatedAt (DateTime) - datum vytvoření přílohy v systému
- ProcessedAt (DateTime, nullable) - datum dokončení zpracování přílohy

Relationships:
- Transaction má 0..N Attachments (one-to-many)
- JOIN: FROM Transactions t LEFT JOIN Attachments a ON t.Id = a.TransactionId

Important Notes:
- All amounts are in Czech Crowns (Kč)
- TransactionType is stored as enum but query as string: WHERE TransactionType = 'Income' or WHERE TransactionType = 'Expense'
- For date filtering use: WHERE TransactionDate >= '2025-01-01' AND TransactionDate < '2025-02-01'
- Czech month names: 1=leden, 2=únor, 3=březen, 4=duben, 5=květen, 6=červen, 7=červenec, 8=srpen, 9=září, 10=říjen, 11=listopad, 12=prosinec

Example Queries:
-- Count all transactions
SELECT COUNT(*) as TotalCount FROM Transactions

-- Sum by transaction type
SELECT TransactionType, SUM(Amount) as Total, COUNT(*) as Count 
FROM Transactions 
GROUP BY TransactionType

-- Top 5 companies by transaction count
SELECT TOP 5 CompanyName, COUNT(*) as TransactionCount, SUM(Amount) as TotalAmount
FROM Transactions
GROUP BY CompanyName
ORDER BY TransactionCount DESC

-- Monthly income summary for 2025
SELECT MONTH(TransactionDate) as Month, SUM(Amount) as TotalIncome
FROM Transactions
WHERE YEAR(TransactionDate) = 2025 AND TransactionType = 'Income'
GROUP BY MONTH(TransactionDate)
ORDER BY Month

-- Transactions with invoice attachments
SELECT t.Id, t.Description, t.Amount, COUNT(a.Id) as AttachmentCount
FROM Transactions t
LEFT JOIN Attachments a ON t.Id = a.TransactionId
WHERE a.Category = 'invoice'
GROUP BY t.Id, t.Description, t.Amount
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
- Always include TOP/LIMIT to avoid returning too much data (e.g., TOP 100)
- Use proper SQL syntax for your database (SQL Server / PostgreSQL)
- TransactionType must be compared as string: WHERE TransactionType = 'Income'
- For date filtering use: WHERE TransactionDate >= '2025-01-01'
- Always validate your SQL before calling this function

The function will return results as JSON array of objects.")]
    public async Task<string> ExecuteSqlQuery(
        [Description(@"SQL SELECT query to execute. Must be valid SELECT statement.
Examples:
- SELECT COUNT(*) FROM Transactions WHERE TransactionType = 'Income'
- SELECT TOP 10 * FROM Transactions ORDER BY TransactionDate DESC
- SELECT CompanyName, SUM(Amount) FROM Transactions GROUP BY CompanyName")] 
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
            return $"Error executing query: {ex.Message}\n\nMake sure your SQL syntax is correct and matches the database schema.";
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
```

### 4. Document Search Functions

**Účel jednotlivých funkcí:**

| Funkce | Kdy se používá | Co vrací | Proč je potřeba |
|--------|---------------|----------|-----------------|
| **SearchDocuments** | Sémantické vyhledávání podle významu | Chunks s preview (300 znaků) | Rychlé nalezení relevantních částí dokumentů |
| **GetDocumentContent** | Follow-up po SearchDocuments | Celý text dokumentu | Když agent potřebuje detaily, které nejsou v preview |
| **CountDocumentsByCategory** | Statistické dotazy | Počty podle kategorií | Přehled o typech a počtu dokumentů |

**Typický workflow:**
```
User: "Jaká je výpovědní lhůta v naší smlouvě s firmou XYZ?"

1. Agent: SearchDocuments("výpovědní lhůta XYZ", category: "contract")
   → Najde: AttachmentId: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            preview: "...výpovědní lhůta je stanovena..."

2. Agent vidí relevantní dokument, ale preview nestačí pro přesnou odpověď

3. Agent: GetDocumentContent("a1b2c3d4-e5f6-7890-abcd-ef1234567890")
   → Získá celý text: "...výpovědní lhůta 3 měsíce..."

4. Agent odpoví: "Ve smlouvě s firmou XYZ je výpovědní lhůta 3 měsíce."
```

**Services/Agents/DocumentFunctions.cs:**
```csharp
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using TransactionManagement.Api.Data;

namespace TransactionManagement.Api.Services.Agents;

/// <summary>
/// Funkce pro práci s dokumenty a jejich sémantické vyhledávání.
/// Podporuje RAG (Retrieval Augmented Generation) workflow.
/// </summary>
public class DocumentFunctions
{
    private readonly IQdrantService _qdrantService;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DocumentFunctions> _logger;

    public DocumentFunctions(
        IQdrantService qdrantService,
        IFileStorageService fileStorage,
        ApplicationDbContext context,
        ILogger<DocumentFunctions> logger)
    {
        _qdrantService = qdrantService;
        _fileStorage = fileStorage;
        _context = context;
        _logger = logger;
    }

    [Description(@"Search for documents using semantic similarity via Qdrant vector database.
This is the PRIMARY function for finding documents based on meaning/context, not exact keywords.

Use cases:
- 'Najdi smlouvy o dodávkách' - finds contracts about deliveries
- 'Které dokumenty zmiňují výpovědní lhůtu?' - finds documents mentioning notice period  
- 'Faktury od ACME' - finds invoices from ACME company

Returns: List of document chunks with similarity scores and content preview (300 chars).
If you need full document content, use GetDocumentContent() with the AttachmentId from results.")]
    public async Task<string> SearchDocuments(
        [Description("Search query describing what you're looking for (in Czech or English)")] 
        string query,
        [Description("Document category filter: 'invoice', 'contract', 'purchase_order', or null for all categories")] 
        string? category = null,
        [Description("Filter for documents containing monetary amounts (true/false/null)")] 
        bool? hasAmounts = null,
        [Description("Filter for documents containing dates (true/false/null)")] 
        bool? hasDates = null,
        [Description("Maximum number of results to return (default: 5, max: 20)")] 
        int limit = 5)
    {
        _logger.LogInformation("=== SearchDocuments called ===");
        _logger.LogInformation("Query: '{Query}', Category: {Category}, Limit: {Limit}", 
            query, category ?? "all", limit);

        var filters = new SearchFilters
        {
            Category = category,
            HasAmounts = hasAmounts,
            HasDates = hasDates
        };

        var results = await _qdrantService.SearchAsync(query, filters, limit);

        _logger.LogInformation("Found {Count} results", results.Count);

        var formattedResults = results.Select(r => new
        {
            AttachmentId = r.AttachmentId,
            TransactionId = r.TransactionId,
            FileName = r.FileName,
            Category = r.Category,
            Score = Math.Round(r.Score, 3),
            ChunkIndex = r.ChunkIndex,
            TotalChunks = r.TotalChunks,
            ContentPreview = r.Content.Length > 300
                ? r.Content.Substring(0, 300) + "..."
                : r.Content,
            HasAmounts = r.HasAmounts,
            HasDates = r.HasDates
        }).ToList();

        return System.Text.Json.JsonSerializer.Serialize(formattedResults, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    [Description(@"Get full content of a specific document by its attachment ID.
Use this function as a FOLLOW-UP after SearchDocuments when you need:
- Complete document text (not just 300 char preview)
- Exact values, amounts, dates that may be outside the preview
- Full context to answer detailed questions

Typical workflow:
1. SearchDocuments() finds relevant document → returns AttachmentId (GUID)
2. GetDocumentContent(AttachmentId) retrieves full text
3. Analyze full text and provide detailed answer

Returns: Complete document content with metadata (filename, category, etc.)")]
    public async Task<string> GetDocumentContent(
        [Description("Attachment ID (GUID) of the document to retrieve (from SearchDocuments results)")]
        Guid attachmentId)
    {
        _logger.LogInformation("=== GetDocumentContent called ===");
        _logger.LogInformation("AttachmentId: {AttachmentId}", attachmentId);
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId);

        if (attachment == null)
        {
            return $"Attachment with ID {attachmentId} not found.";
        }

        try
        {
            using var stream = await _fileStorage.GetFileAsync(attachment.FilePath);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var result = new
            {
                AttachmentId = attachment.Id,
                TransactionId = attachment.TransactionId,
                FileName = attachment.FileName,
                Category = attachment.Category,
                Content = content
            };

            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading document {AttachmentId}", attachmentId);
            return $"Error reading document: {ex.Message}";
        }
    }

    [Description(@"Get aggregated counts of documents grouped by category.
Use this for STATISTICAL queries, not for searching specific documents.

Use cases:
- 'Kolik máme faktur?' - How many invoices do we have?
- 'Jaké typy dokumentů máme?' - What types of documents do we have?
- 'Máme víc smluv nebo objednávek?' - Do we have more contracts or purchase orders?

Returns: JSON array with category names and their counts.
Only counts 'completed' documents (successfully processed).")]
    public async Task<string> CountDocumentsByCategory()
    {
        _logger.LogInformation("=== CountDocumentsByCategory called ===");

        var counts = await _context.Attachments
            .Where(a => a.ProcessingStatus == "completed")
            .GroupBy(a => a.Category ?? "unknown")
            .Select(g => new
            {
                Category = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        _logger.LogInformation("Found {CategoryCount} categories with {TotalCount} total documents", 
            counts.Count, counts.Sum(c => c.Count));

        return System.Text.Json.JsonSerializer.Serialize(counts, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
```

### 5. Chat Orchestrator

**Services/Agents/ChatOrchestrator.cs:**
```csharp
using Microsoft.Extensions.AI;

namespace TransactionManagement.Api.Services.Agents;

public interface IChatOrchestrator
{
    Task<ChatResponse> ProcessMessageAsync(
        string userMessage, 
        List<ChatMessageDto>? conversationHistory = null);
}

public class ChatOrchestrator : IChatOrchestrator
{
    private readonly IChatClientService _chatClientService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatOrchestrator> _logger;

    public ChatOrchestrator(
        IChatClientService chatClientService,
        IServiceProvider serviceProvider,
        ILogger<ChatOrchestrator> logger)
    {
        _chatClientService = chatClientService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessMessageAsync(
        string userMessage,
        List<ChatMessageDto>? conversationHistory = null)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Získej chat client
            var chatClient = _chatClientService.CreateChatClient();

            // Vytvoř scope pro DI závislosti
            using var scope = _serviceProvider.CreateScope();
            var dbFunctions = scope.ServiceProvider.GetRequiredService<DatabaseFunctions>();
            var docFunctions = scope.ServiceProvider.GetRequiredService<DocumentFunctions>();

            // Vytvoř AI tools z funkcí
            var tools = new List<AIFunction>
            {
                // Database tools (Text-to-SQL)
                AIFunctionFactory.Create(dbFunctions.GetDatabaseSchema),
                AIFunctionFactory.Create(dbFunctions.ExecuteSqlQuery),
                AIFunctionFactory.Create(dbFunctions.GetTransactionStatistics),
                
                // Document search tools (RAG)
                AIFunctionFactory.Create(docFunctions.SearchDocuments),
                AIFunctionFactory.Create(docFunctions.GetDocumentContent),
                AIFunctionFactory.Create(docFunctions.CountDocumentsByCategory)
            };

            // Vytvoř agenta s tools
            var agent = chatClient.AsBuilder()
                .UseFunctionInvocation() // Automatické volání funkcí
                .Build()
                .CreateAIAgent(
                    name: "TransactionAssistant",
                    instructions: @"You are a helpful assistant for a transaction management system.
You have access to a SQL database and document search capabilities.

## DATABASE QUERIES (Text-to-SQL approach)

When user asks about transaction data:

1. **ALWAYS start with GetDatabaseSchema()** to understand the database structure
2. Then construct appropriate SQL query based on user's question
3. Execute query using ExecuteSqlQuery()
4. For common statistics, you can use GetTransactionStatistics() as shortcut

SQL Guidelines:
- Only use SELECT statements (no INSERT, UPDATE, DELETE)
- Always include TOP or LIMIT (e.g., TOP 10, TOP 100)
- TransactionType values: 'Income' or 'Expense' (compare as strings)
- For date filtering: WHERE TransactionDate >= '2025-01-01'
- Czech months: 1=leden, 2=únor, 3=březen, 4=duben, 5=květen, 6=červen, 7=červenec, 8=srpen, 9=září, 10=říjen, 11=listopad, 12=prosinec
- Use proper SQL syntax (SQL Server / PostgreSQL)

Example queries:
- Count: SELECT COUNT(*) FROM Transactions WHERE TransactionType = 'Income'
- Sum: SELECT SUM(Amount) FROM Transactions WHERE YEAR(TransactionDate) = 2025
- Top companies: SELECT TOP 5 CompanyName, COUNT(*) as Count FROM Transactions GROUP BY CompanyName ORDER BY Count DESC
- Monthly: SELECT MONTH(TransactionDate) as Month, SUM(Amount) FROM Transactions GROUP BY MONTH(TransactionDate)

## DOCUMENT SEARCH (RAG approach)

When user asks about document content:

1. Use SearchDocuments() for semantic search (finds relevant chunks with preview)
2. If preview is not enough, use GetDocumentContent() to get full text
3. For statistics about documents, use CountDocumentsByCategory()

Document search workflow:
- SearchDocuments finds relevant chunks → you see AttachmentId and preview
- If you need details, call GetDocumentContent(AttachmentId) → get full text
- Analyze and provide answer

## COMBINED QUERIES

For queries needing both data AND documents:
- First query database for transaction data
- Then search documents for related information
- Combine insights from both sources

## RESPONSE GUIDELINES

- Always respond in Czech language
- Format numbers with Czech formatting (space as thousands separator: 1 000 000)
- Use Czech date format: DD.MM.YYYY (e.g., 15.03.2025)
- Be concise but comprehensive
- When you use tools, briefly mention what you found
- If query requires multiple steps, execute them all to provide complete answer",
                    tools: tools.ToArray());

            // Sestav historii konverzace
            var messages = new List<ChatMessage>();

            // Přidej historii konverzace
            if (conversationHistory != null)
            {
                foreach (var msg in conversationHistory)
                {
                    if (msg.Role == "user")
                    {
                        messages.Add(new ChatMessage(ChatRole.User, msg.Content));
                    }
                    else if (msg.Role == "assistant")
                    {
                        messages.Add(new ChatMessage(ChatRole.Assistant, msg.Content));
                    }
                }
            }

            // Přidej aktuální zprávu
            messages.Add(new ChatMessage(ChatRole.User, userMessage));

            // Zavolej agenta (automaticky volá potřebné tools)
            var response = await agent.RunAsync(messages);

            var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogInformation(
                "Chat query processed in {Time}s: {Query}",
                responseTime,
                userMessage.Substring(0, Math.Min(100, userMessage.Length)));

            return new ChatResponse
            {
                Message = response.Text ?? "No response generated",
                ResponseTime = responseTime,
                TokensUsed = 0, // TODO: Pokud model poskytuje info o tokenech
                AgentsUsed = new List<string> { "TransactionAssistant" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message: {Message}", userMessage);
            throw;
        }
    }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public double ResponseTime { get; set; }
    public int TokensUsed { get; set; }
    public List<string> AgentsUsed { get; set; } = new();
}
```

### 6. DTOs

**Models/DTOs/ChatDtos.cs:**
```csharp
namespace TransactionManagement.Api.Models.DTOs;

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public List<ChatMessageDto>? ConversationHistory { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty; // "user" nebo "assistant"
    public string Content { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public ChatMetadataDto? Metadata { get; set; }
}

public class ChatMetadataDto
{
    public int TokensUsed { get; set; }
    public double ResponseTime { get; set; }
    public List<string> AgentsUsed { get; set; } = new();
}
```

### 7. Chat Controller

**Controllers/ChatController.cs:**
```csharp
using Microsoft.AspNetCore.Mvc;
using TransactionManagement.Api.Models.DTOs;
using TransactionManagement.Api.Services.Agents;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatOrchestrator chatOrchestrator,
        ILogger<ChatController> logger)
    {
        _chatOrchestrator = chatOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/chat/message - Process chat message
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<ChatResponseDto>> SendMessage([FromBody] ChatRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { message = "Message cannot be empty" });
        }

        try
        {
            var response = await _chatOrchestrator.ProcessMessageAsync(
                request.Message,
                request.ConversationHistory);

            var sessionId = request.SessionId ?? Guid.NewGuid();

            return Ok(new ChatResponseDto
            {
                Message = response.Message,
                SessionId = sessionId,
                Metadata = new ChatMetadataDto
                {
                    TokensUsed = response.TokensUsed,
                    ResponseTime = response.ResponseTime,
                    AgentsUsed = response.AgentsUsed
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new 
            { 
                message = "Error processing message", 
                error = ex.Message 
            });
        }
    }

    /// <summary>
    /// GET /api/chat/health - Health check
    /// </summary>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
```

### 8. Registrace v Program.cs

```csharp
// Program.cs

using TransactionManagement.Api.Services.Agents;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// ... existující služby ...

// === Agent Framework Services ===

// Chat Client Service pro Ollama
builder.Services.AddSingleton<IChatClientService, ChatClientService>();

// Alternativně: Přímá registrace IChatClient
// builder.Services.AddSingleton<IChatClient>(sp =>
// {
//     var config = sp.GetRequiredService<IConfiguration>();
//     return new OllamaChatClient(
//         new Uri(config["LLM:BaseUrl"] ?? "http://localhost:11434"),
//         config["LLM:Model"] ?? "llama3.1:8b"
//     );
// });

// Function services (scoped - kvůli DbContext)
builder.Services.AddScoped<DatabaseFunctions>();
builder.Services.AddScoped<DocumentFunctions>();

// Chat Orchestrator
builder.Services.AddScoped<IChatOrchestrator, ChatOrchestrator>();

// ... zbytek konfigurace ...

var app = builder.Build();

// ... middleware pipeline ...

app.Run();
```

## Testování

### 1. Test zdraví API

```bash
curl http://localhost:5000/api/chat/health
```

### 2. Test jednoduchého dotazu

```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Ahoj! Jak mi můžeš pomoci?"
  }'
```

Očekávaná odpověď:
```json
{
  "message": "Ahoj! Jsem asistent pro správu transakcí. Mohu ti pomoci s:\n\n1. Dotazy na transakce - počty, sumy, seznamy transakcí\n2. Statistiky firem - top firmy podle počtu transakcí\n3. Vyhledávání v dokumentech - faktury, smlouvy, objednávky\n\nCo tě zajímá?",
  "sessionId": "...",
  "metadata": {
    "tokensUsed": 0,
    "responseTime": 0.8,
    "agentsUsed": ["TransactionAssistant"]
  }
}
```

### 3. Test databázových dotazů (Text-to-SQL)

```bash
# Jednoduchý počet
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Kolik transakcí bylo vytvořeno v roce 2025?"
  }'

# Očekávaný flow:
# 1. Agent zavolá GetDatabaseSchema()
# 2. Agent vytvoří SQL: SELECT COUNT(*) FROM Transactions WHERE YEAR(TransactionDate) = 2025
# 3. Agent zavolá ExecuteSqlQuery(sql)
# 4. Odpoví: "V roce 2025 bylo vytvořeno 47 transakcí."

# Suma příjmů za období
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaká byla celková částka příjmů za únor 2025?"
  }'

# Agent SQL: SELECT SUM(Amount) FROM Transactions 
#           WHERE TransactionType = 'Income' 
#           AND MONTH(TransactionDate) = 2 
#           AND YEAR(TransactionDate) = 2025

# Top firmy podle počtu transakcí
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Které 3 firmy mají nejvíce transakcí?"
  }'

# Agent SQL: SELECT TOP 3 CompanyName, COUNT(*) as TransactionCount 
#           FROM Transactions 
#           GROUP BY CompanyName 
#           ORDER BY TransactionCount DESC

# Pokročilý dotaz - průměrná výše transakce
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaká je průměrná výše výdajů za každý měsíc v roce 2025?"
  }'

# Agent SQL: SELECT MONTH(TransactionDate) as Month, AVG(Amount) as AverageAmount
#           FROM Transactions 
#           WHERE TransactionType = 'Expense' AND YEAR(TransactionDate) = 2025
#           GROUP BY MONTH(TransactionDate)
#           ORDER BY Month

# Složitý dotaz s JOINem
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Zobraz mi transakce, které mají alespoň 2 přílohy"
  }'

# Agent SQL: SELECT t.Id, t.Description, t.Amount, COUNT(a.Id) as AttachmentCount
#           FROM Transactions t
#           LEFT JOIN Attachments a ON t.Id = a.TransactionId
#           GROUP BY t.Id, t.Description, t.Amount
#           HAVING COUNT(a.Id) >= 2
```

### 4. Test sémantického vyhledávání (RAG)

```bash
# Základní vyhledávání
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Najdi všechny faktury od společnosti ACME"
  }'

# Očekávaný flow:
# 1. Agent zavolá SearchDocuments("faktury ACME", category: "invoice")
# 2. Dostane chunky s preview
# 3. Pokud potřebuje detaily, zavolá GetDocumentContent(attachmentId)

# Vyhledávání s follow-up pro detaily
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Jaká je výpovědní lhůta v naší smlouvě s firmou XYZ?"
  }'

# Agent flow:
# 1. SearchDocuments("výpovědní lhůta XYZ", category: "contract")
# 2. Najde relevantní dokument, ale preview nestačí
# 3. GetDocumentContent(attachmentId) pro celý text
# 4. Extrahuje přesnou hodnotu: "3 měsíce"

# Statistika dokumentů
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Kolik máme celkem dokumentů a jaké jsou jejich typy?"
  }'

# Agent zavolá: CountDocumentsByCategory()
```

### 5. Test multi-turn konverzace

```bash
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "A kolik z nich má přílohy?",
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "conversationHistory": [
      {
        "role": "user",
        "content": "Kolik transakcí máme celkem?"
      },
      {
        "role": "assistant",
        "content": "Celkem máte 150 transakcí v systému."
      }
    ]
  }'

# Agent si pamatuje kontext a vytvoří SQL:
# SELECT COUNT(*) FROM Transactions t 
# LEFT JOIN Attachments a ON t.Id = a.TransactionId 
# WHERE a.Id IS NOT NULL
```

### 6. Test kombinovaných dotazů (SQL + RAG)

```bash
# Kombinace databáze a dokumentů
curl -X POST http://localhost:5000/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Které transakce od firmy ACME mají přílohy typu faktura a jaká je celková částka?"
  }'

# Agent flow:
# 1. SQL: SELECT t.*, a.Category FROM Transactions t 
#         JOIN Attachments a ON t.Id = a.TransactionId 
#         WHERE t.CompanyId = 'ACME' AND a.Category = 'invoice'
# 2. SearchDocuments("ACME faktura", category: "invoice") pro ověření
# 3. Kombinuje výsledky obou zdrojů
```

## Testovací scénáře

### Databázové dotazy (Text-to-SQL)
1. ✅ **Základní počty**: "Kolik transakcí bylo vytvořeno v roce 2025?"
   - Agent: GetDatabaseSchema() → ExecuteSqlQuery("SELECT COUNT(*) WHERE YEAR = 2025")

2. ✅ **Agregace podle období**: "Jaký byl celkový výdělek za každý měsíc?"
   - Agent: SQL s GROUP BY MONTH(TransactionDate)

3. ✅ **Top N dotazy**: "Která společnost má nejvíce transakcí?"
   - Agent: SQL s GROUP BY CompanyName ORDER BY COUNT(*) DESC

4. ✅ **Statistiky**: "Kolik máme výdajů a kolik příjmů?"
   - Agent: GetTransactionStatistics() nebo SQL s GROUP BY TransactionType

5. ✅ **Poslední záznamy**: "Zobraz mi posledních 5 transakcí"
   - Agent: SQL s ORDER BY TransactionDate DESC TOP 5

6. ✅ **Filtrování podle firmy**: "Jaká je celková částka transakcí od firmy XYZ?"
   - Agent: SQL s WHERE CompanyId = 'XYZ' AND SUM(Amount)

7. ✅ **Pokročilé agregace**: "Jaká je průměrná výše příjmů pro každou firmu?"
   - Agent: SQL s AVG(Amount) GROUP BY CompanyName WHERE TransactionType = 'Income'

8. ✅ **JOINy**: "Které transakce mají více než 2 přílohy?"
   - Agent: SQL s JOIN Attachments a.TransactionId GROUP BY HAVING COUNT > 2

### Dokumentové dotazy (RAG)
9. ✅ **Sémantické vyhledávání**: "Najdi všechny smlouvy o dodávkách"
   - Agent: SearchDocuments("smlouvy dodávky", category: "contract")

10. ✅ **Vyhledávání s detaily**: "Jaká je výpovědní lhůta v naší smlouvě s XYZ?"
    - Agent: SearchDocuments() → GetDocumentContent() → extrakce hodnoty

11. ✅ **Statistiky dokumentů**: "Kolik máme faktur?"
    - Agent: CountDocumentsByCategory()

12. ✅ **Obsahové dotazy**: "Které dokumenty zmiňují údržbu?"
    - Agent: SearchDocuments("údržba") → analýza výsledků

13. ✅ **Specifický dokument**: "Zobraz mi obsah dokumentu s ID a1b2c3d4-e5f6-7890-abcd-ef1234567890"
    - Agent: GetDocumentContent("a1b2c3d4-e5f6-7890-abcd-ef1234567890")

### Kombinované dotazy (SQL + RAG)
14. ✅ **Data + dokumenty**: "Které transakce mají přílohy typu faktura?"
    - Agent: SQL JOIN + volitelně SearchDocuments pro ověření

15. ✅ **Komplexní analýza**: "Najdi faktury od ACME a řekni mi jejich celkovou hodnotu"
    - Agent: SearchDocuments("ACME faktura") → extrahuje AttachmentIds → SQL pro sumy

16. ✅ **Časové dotazy s dokumenty**: "Jaké smlouvy byly přiřazeny k transakcím z března?"
    - Agent: SQL WHERE MONTH = 3 + JOIN Attachments WHERE Category = 'contract'

### Pokročilé scénáře
17. ✅ **Multi-step reasoning**: "Která firma má nejvyšší průměrnou hodnotu transakce a máme od ní smlouvu?"
    - Agent: SQL AVG() → najde firmu → SearchDocuments pro smlouvu

18. ✅ **Časová analýza**: "Porovnej příjmy mezi Q1 a Q2 2025"
    - Agent: 2× SQL dotazy s WHERE MONTH BETWEEN → porovnání

## Proč Text-to-SQL přístup?

### Výhody oproti předpřipravným funkcím

| Aspekt | Předpřipravené funkce | Text-to-SQL |
|--------|----------------------|-------------|
| **Flexibilita** | Pouze předem definované dotazy | Libovolné SQL dotazy |
| **Kód k údržbě** | 10-20 funkcí | 2-3 funkce |
| **Komplexita dotazů** | Omezená kombinacemi parametrů | JOINy, subqueries, window functions |
| **Adaptabilita** | Vyžaduje změny kódu | Agent se adaptuje sám |
| **Pokročilé operace** | Musí být implementovány jednotlivě | AVG, STDDEV, PERCENTILE atd. automaticky |

### Příklad: "Jaká je průměrná hodnota transakcí v každém měsíci?"

**Předpřipravené funkce:**
```csharp
// Musíš implementovat:
GetAverageTransactionByMonth(int year)
GetAverageTransactionByMonthAndType(int year, string type)
GetAverageTransactionByMonthAndCompany(int year, string companyId)
// ... a další kombinace
```

**Text-to-SQL:**
```csharp
// Agent sám vytvoří:
SELECT MONTH(TransactionDate) as Month, AVG(Amount) as Average
FROM Transactions 
WHERE YEAR(TransactionDate) = 2025
GROUP BY MONTH(TransactionDate)

// A zvládne i varianty bez dalšího kódu:
// - Jen pro příjmy: WHERE TransactionType = 'Income'
// - Pro konkrétní firmu: WHERE CompanyId = 'XYZ'
// - S mediánem: PERCENTILE_CONT(0.5)
```

## Pokročilé funkce (volitelné)

### Streaming odpovědí

Pokud chceš streaming (postupné generování odpovědi):

```csharp
public async IAsyncEnumerable<string> ProcessMessageStreamingAsync(string userMessage)
{
    var chatClient = _chatClientService.CreateChatClient();
    var agent = chatClient.CreateAIAgent(/* ... */);
    
    await foreach (var update in agent.RunStreamAsync(userMessage))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}
```

### Workflow pro složitější orchestraci

Pokud potřebuješ explicitní kontrolu nad tím, který agent se kdy spustí:

```csharp
using Microsoft.Agents.AI.Workflows;

public class AdvancedOrchestrator
{
    public async Task<string> ProcessWithWorkflow(string query)
    {
        // Vytvoř specializované agenty
        var routerAgent = _chatClient.CreateAIAgent(
            name: "Router",
            instructions: "Analyze query and route to appropriate handler"
        );
        
        var dbAgent = _chatClient.CreateAIAgent(
            name: "DatabaseAgent",
            instructions: "Handle database queries",
            tools: databaseTools
        );
        
        var docAgent = _chatClient.CreateAIAgent(
            name: "DocumentAgent",
            instructions: "Handle document searches",
            tools: documentTools
        );

        // Vytvoř workflow
        var workflow = WorkflowBuilder
            .CreateBuilder()
            .AddAgent(routerAgent, "router")
            .AddAgent(dbAgent, "database")
            .AddAgent(docAgent, "documents")
            .AddSequentialEdge("router", "database") // router → database
            .AddSequentialEdge("database", "documents") // database → documents
            .Build();

        var result = await workflow.RunAsync(query);
        return result.ToString();
    }
}
```

## Ověření funkčnosti

Po dokončení implementace ověř:

1. ✅ Chat Client je správně nakonfigurován s Ollama
2. ✅ **DatabaseFunctions má 3 klíčové funkce:**
   - GetDatabaseSchema() - vrací kompletní schéma
   - ExecuteSqlQuery() - vykonává SELECT dotazy bezpečně
   - GetTransactionStatistics() - rychlé statistiky
3. ✅ **DocumentFunctions má 3 funkce pro RAG:**
   - SearchDocuments() - sémantické vyhledávání s preview
   - GetDocumentContent() - načtení celého dokumentu
   - CountDocumentsByCategory() - statistiky dokumentů
4. ✅ ChatOrchestrator správně vytváří agenta s tools
5. ✅ **Agent dostává správné instrukce:**
   - Rozumí Text-to-SQL workflow (schema → SQL → execute)
   - Zná RAG workflow (search → preview → get content)
   - Umí kombinovat oba přístupy
6. ✅ Automatické volání funkcí (function calling) funguje
7. ✅ Odpovědi jsou v češtině s českým formátováním
8. ✅ Multi-turn konverzace zachovává kontext
9. ✅ **Agent dokáže:**
   - Generovat komplexní SQL dotazy (JOIN, GROUP BY, subqueries)
   - Vyhledávat dokumenty sémanticky
   - Kombinovat data z DB a dokumentů
10. ✅ **Bezpečnost:**
    - IsValidSelectQuery() blokuje nebezpečné operace
    - Pouze SELECT dotazy jsou povoleny

## Debugging tipy

### Logování SQL dotazů

Agent automaticky loguje SQL dotazy v `ExecuteSqlQuery()`:

```csharp
_logger.LogInformation("=== Executing SQL query ===");
_logger.LogInformation("Query: {Query}", sqlQuery);
_logger.LogInformation("Query returned {Count} rows", rows.Count);
```

Pro debugging můžeš nastavit log level na `Debug` v `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "TransactionManagement.Api.Services.Agents": "Debug"
    }
  }
}
```

### Testování SQL funkcí manuálně

```csharp
// V unit testu nebo konzolové aplikaci
var dbFunctions = new DatabaseFunctions(context, logger);

// Test GetDatabaseSchema
var schema = dbFunctions.GetDatabaseSchema();
Console.WriteLine(schema);

// Test ExecuteSqlQuery
var result = await dbFunctions.ExecuteSqlQuery(
    "SELECT TOP 5 * FROM Transactions ORDER BY TransactionDate DESC");
Console.WriteLine(result);
```

### Zachycení chyb v orchestrátoru

```csharp
try
{
    var response = await agent.RunAsync(messages);
    _logger.LogInformation("Agent response: {Response}", response.Text);
    return new ChatResponse { Message = response.Text };
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error details: {Message}", ex.Message);
    _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
    throw;
}
```

### Ověření SQL bezpečnosti

Testuj, že `IsValidSelectQuery()` správně blokuje nebezpečné dotazy:

```csharp
// ✅ Mělo by projít
IsValidSelectQuery("SELECT * FROM Transactions") // true

// ❌ Mělo by být zablokováno
IsValidSelectQuery("DROP TABLE Transactions") // false
IsValidSelectQuery("DELETE FROM Transactions") // false
IsValidSelectQuery("SELECT * FROM Transactions; DROP TABLE Users") // false
```

## Výstup této fáze

✅ **ChatClientService** - správa Ollama chat klienta  
✅ **DatabaseFunctions** s Text-to-SQL přístupem:
   - GetDatabaseSchema() - kompletní DB schéma
   - ExecuteSqlQuery() - bezpečné vykonávání SELECT dotazů
   - GetTransactionStatistics() - rychlé statistiky
✅ **DocumentFunctions** s RAG workflow:
   - SearchDocuments() - sémantické vyhledávání
   - GetDocumentContent() - načtení celého dokumentu
   - CountDocumentsByCategory() - statistiky dokumentů
✅ **ChatOrchestrator** s automatickou orchestrací
✅ **Automatic function calling** - agent sám rozhoduje strategie
✅ **Text-to-SQL capabilities** - flexibilní SQL generování
✅ **RAG capabilities** - sémantické vyhledávání dokumentů
✅ **REST API endpoint** pro chat
✅ **Support pro conversation history**  
✅ **Czech language responses** s českým formátováním
✅ **Security** - validace SQL dotazů

## Porovnání: Semantic Kernel vs Agent Framework

| Aspekt | Semantic Kernel | Agent Framework |
|--------|----------------|-----------------|
| **Hlavní objekt** | `Kernel` | `IChatClient` + `AIAgent` |
| **Funkce** | `[KernelFunction]` | `[Description]` |
| **Registrace** | `kernel.Plugins.Add()` | `tools: [AIFunctionFactory.Create()]` |
| **Vytvoření agenta** | `new ChatCompletionAgent() { Kernel = kernel }` | `chatClient.CreateAIAgent(tools: [...])` |
| **Orchestrace** | Ruční nebo SK patterns | Workflows + automatic routing |
| **Syntaxe** | Více boilerplate kódu | Jednodušší, méně kódu |

## Další krok

→ **08_langfuse_monitoring.md** - Integrace Langfuse pro monitoring LLM calls a function invocations
