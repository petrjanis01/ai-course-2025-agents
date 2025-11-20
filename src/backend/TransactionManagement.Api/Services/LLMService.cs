using System.Text;
using System.Text.Json;

namespace TransactionManagement.Api.Services;

public interface ILLMService
{
    Task<string> CategorizeDocumentAsync(string content, string? traceId = null);
}

public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<LLMService> _logger;

    public LLMService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LLMService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _model = configuration["LLM:Model"] ?? "llama3.1:8b";
        _logger = logger;
    }

    public async Task<string> CategorizeDocumentAsync(string content, string? traceId = null)
    {

        // Take first 2000 characters for categorization
        var sampleContent = content.Length > 2000 ? content.Substring(0, 2000) : content;

        var systemPrompt = @"You are a document classifier. Analyze the document and classify it into one of these categories:
- invoice (faktura)
- contract (smlouva)
- purchase_order (objednávka)
- unknown (if you cannot determine)

Respond with ONLY the category name, nothing else.";

        var userPrompt = $"Classify this document:\n\n{sampleContent}";
        var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var request = new
        {
            model = _model,
            prompt = fullPrompt,
            stream = false,
            options = new
            {
                temperature = 0.1,
                num_predict = 20
            }
        };

        var json = JsonSerializer.Serialize(request);
        var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsync("/api/generate", requestContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

            var endTime = DateTime.UtcNow;
            var category = result?.Response?.Trim().ToLower() ?? "unknown";
            var duration = (endTime - startTime).TotalMilliseconds;

            // Validate category
            var validCategories = new[] { "invoice", "contract", "purchase_order", "unknown" };
            if (!validCategories.Contains(category))
            {
                _logger.LogWarning("Invalid category returned: {Category}, defaulting to unknown", category);
                category = "unknown";
            }

            _logger.LogDebug("Categorized document as {Category} in {Duration}ms", category, duration);

            return category;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error categorizing document");
            return "unknown";
        }
    }

    private class OllamaResponse
    {
        public string Response { get; set; } = string.Empty;
    }
}
