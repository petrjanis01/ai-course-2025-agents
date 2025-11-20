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
        var enableSensitiveData = _configuration
            .GetSection("Observability")
            .GetValue<bool>("EnableSensitiveData", true);

        // Create Ollama chat client with OpenTelemetry instrumentation
        var chatClient = new OllamaChatClient(new Uri(baseUrl), model)
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "TransactionManagement", configure: cfg =>
            {
                cfg.EnableSensitiveData = enableSensitiveData;
            })
            .Build();

        _logger.LogDebug("Created Ollama chat client with model {Model} (OpenTelemetry: EnableSensitiveData={EnableSensitiveData})",
            model, enableSensitiveData);

        return chatClient;
    }
}
