using System.Text;
using System.Text.Json;

namespace TransactionManagement.Api.Services;

public interface IEmbeddingService
{
    Task<float[]> CreateEmbeddingAsync(string text, string? traceId = null);
    Task<List<float[]>> CreateEmbeddingsAsync(List<string> texts);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Ollama");
        _model = configuration["Embedding:Model"] ?? "all-minilm:l6-v2";
        _logger = logger;
    }

    public async Task<float[]> CreateEmbeddingAsync(string text, string? traceId = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be empty", nameof(text));
        }

        var request = new
        {
            model = _model,
            prompt = text
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsync("/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson, options);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new Exception("No embedding returned from Ollama");
            }

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogDebug("Created embedding: {Dimensions} dimensions in {Duration}ms",
                result.Embedding.Length, duration);

            return result.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating embedding for text: {Text}", text.Substring(0, Math.Min(100, text.Length)));
            throw;
        }
    }

    public async Task<List<float[]>> CreateEmbeddingsAsync(List<string> texts)
    {
        var embeddings = new List<float[]>();

        foreach (var text in texts)
        {
            var embedding = await CreateEmbeddingAsync(text);
            embeddings.Add(embedding);
        }

        return embeddings;
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }
}
