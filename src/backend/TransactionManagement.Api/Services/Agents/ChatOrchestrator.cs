using Microsoft.Extensions.AI;
using TransactionManagement.Api.Models.DTOs;

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
            // Získej chat client (s OpenTelemetry)
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
- Always include LIMIT (e.g., LIMIT 10, LIMIT 100)
- Use PostgreSQL syntax with double-quoted table and column names
- TransactionType values: 'Income' or 'Expense' (compare as strings)
- For date filtering: WHERE ""TransactionDate"" >= '2025-01-01'
- Czech months: 1=leden, 2=únor, 3=březen, 4=duben, 5=květen, 6=červen, 7=červenec, 8=srpen, 9=září, 10=říjen, 11=listopad, 12=prosinec

Example queries:
- Count: SELECT COUNT(*) FROM ""Transactions"" WHERE ""TransactionType"" = 'Income'
- Sum: SELECT SUM(""Amount"") FROM ""Transactions"" WHERE EXTRACT(YEAR FROM ""TransactionDate"") = 2025
- Top companies: SELECT ""CompanyName"", COUNT(*) as Count FROM ""Transactions"" GROUP BY ""CompanyName"" ORDER BY Count DESC LIMIT 5
- Monthly: SELECT EXTRACT(MONTH FROM ""TransactionDate"") as Month, SUM(""Amount"") FROM ""Transactions"" GROUP BY EXTRACT(MONTH FROM ""TransactionDate"")

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
            // OpenTelemetry automaticky trackuje všechno
            var response = await agent.RunAsync(messages);

            var responseTime = (DateTime.UtcNow - startTime).TotalSeconds;

            _logger.LogInformation(
                "Chat query processed in {Time}s: {Query}",
                responseTime,
                userMessage.Substring(0, Math.Min(100, userMessage.Length)));

            var responseMessage = response.Text ?? "No response generated";

            // Extract usage info if available
            int tokensUsed = 0;
            if (response.AdditionalProperties?.TryGetValue("Usage", out var usageObj) == true)
            {
                try
                {
                    var usageDict = usageObj as IDictionary<string, object>;
                    if (usageDict?.ContainsKey("TotalTokenCount") == true)
                    {
                        tokensUsed = Convert.ToInt32(usageDict["TotalTokenCount"]);
                    }
                }
                catch
                {
                    // Ignore usage extraction errors
                }
            }

            return new ChatResponse
            {
                Message = responseMessage,
                ResponseTime = responseTime,
                TokensUsed = tokensUsed,
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
