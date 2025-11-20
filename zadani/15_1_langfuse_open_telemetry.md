Níž máš hotové „zadání pro programátora“ v češtině – můžeš to klidně hodit do Jira / DevOps jako task.

---

## Cíl

Napojit stávající .NET 8 aplikaci používající **Microsoft.Extensions.AI** s **Ollama** na **Langfuse** přes **OpenTelemetry**, tak aby:

* každé volání agenta/chat klienta vytvořilo v Langfuse **trace** se spanem(y) pro:

  * agenta,
  * LLM volání (vč. modelu a tokenů),
  * případné tool cally;
* v Langfuse byla vidět:

  * **prompt historie** (gen_ai.prompt.*),
  * **odpověď** (gen_ai.completion.*),
  * **model** (gen_ai.request.model / gen_ai.response.model),
  * **usage** (gen_ai.usage.prompt_tokens / completion_tokens / total_tokens).

Export bude probíhat **přímo z aplikace** pomocí OTLP HTTP exporteru na Langfuse OTel endpoint (bez samostatného OTel Collectoru).

---

## Vstupy / předpoklady

* Aplikace: .NET 8, používáme **Microsoft.Extensions.AI** s **Ollama** jako LLM provider.
* Langfuse účet/projekt existuje (máme **public** a **secret key**).
* K dispozici je prostředí **EU** v Langfuse (default `https://cloud.langfuse.com`).

---

## 1. NuGet balíčky

Doprogramuj/zkontroluj, že projekt obsahuje:

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Projekt už obsahuje:
- `Microsoft.Extensions.AI` (pro IChatClient abstrakci)
- `Microsoft.Extensions.AI.Ollama` (pro Ollama provider)

---

## 2. Zapnutí OpenTelemetry na chat klientovi

V `ChatClientService.cs`, kde vytváříme `IChatClient`, přidej `.UseOpenTelemetry()`:

```csharp
public IChatClient CreateChatClient()
{
    var baseUrl = _configuration["LLM:BaseUrl"] ?? "http://localhost:11434";
    var model = _configuration["LLM:Model"] ?? "llama3.1:8b";
    var enableSensitiveData = _configuration
        .GetSection("Observability")
        .GetValue<bool>("EnableSensitiveData", true);

    // Vytvoř Ollama chat client s OpenTelemetry
    var chatClient = new OllamaChatClient(new Uri(baseUrl), model)
        .AsBuilder()
        .UseOpenTelemetry(sourceName: "TransactionManagement", configure: cfg =>
        {
            cfg.EnableSensitiveData = enableSensitiveData;
        })
        .Build();

    return chatClient;
}
```

**Důležité:**
- `sourceName` (`"TransactionManagement"`) použijeme v OpenTelemetry konfiguraci pro `AddSource`
- `EnableSensitiveData = true` v dev/test (vidíme prompty/odpovědi), `false` v prod (jen metadata)

---

## 3. Konfigurace OpenTelemetry v Program.cs

V `Program.cs` (nebo ekvivalent startu) nastav **OpenTelemetry tracing** s OTLP HTTP exportem do Langfuse.

### 3.1. Konfigurace (appsettings)

Do `appsettings.json` přidej sekci:

```json
"Langfuse": {
  "BaseUrl": "https://cloud.langfuse.com",
  "PublicKey": "pk-lf-xxxxxxxxxxxxxxxxx",
  "SecretKey": "sk-lf-xxxxxxxxxxxxxxxxx"
}
```

V produkci tyto hodnoty ber z **Environment Variables** / Key Vault, ne hard-codem.

### 3.2. Registrace OpenTelemetry

Do `Program.cs`:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using System.Text;

// ...

var builder = WebApplication.CreateBuilder(args);

// Langfuse config
var langfuseSection = builder.Configuration.GetSection("Langfuse");
var langfuseBaseUrl = langfuseSection.GetValue<string>("BaseUrl") ?? "https://cloud.langfuse.com";
var langfusePublicKey = langfuseSection.GetValue<string>("PublicKey")!;
var langfuseSecretKey = langfuseSection.GetValue<string>("SecretKey")!;

// Basic Auth header pro Langfuse OTLP endpoint
var authString = Convert.ToBase64String(
    Encoding.UTF8.GetBytes($"{langfusePublicKey}:{langfuseSecretKey}"));

// Registrace OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "TransactionManagement", serviceVersion: "1.0.0"))
    .WithTracing(tracerBuilder =>
    {
        tracerBuilder
            // Musí odpovídat sourceName použitému v UseOpenTelemetry
            .AddSource("TransactionManagement")
            .SetSampler(new ParentBasedSampler(new AlwaysOnSampler()))
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri($"{langfuseBaseUrl}/api/public/otel/v1/traces");
                options.Protocol = OtlpExportProtocol.HttpProtobuf; // Langfuse vyžaduje HTTP/protobuf
                options.Headers = $"Authorization=Basic {authString}";
            });

        // volitelně: v dev i console exporter pro rychlé ladění
        // .AddConsoleExporter();
    });
```

Poznámky:

* Endpoint musí být **HTTP/protobuf**, ne gRPC (`OtlpExportProtocol.HttpProtobuf`), což je nutnost pro Langfuse.([langfuse.com][2])
* Alternativně bychom mohli použít env proměnné `OTEL_EXPORTER_OTLP_ENDPOINT` / `OTEL_EXPORTER_OTLP_HEADERS` přesně podle Langfuse dokumentace, ale výše uvedené řešení je self-contained v .NET kódu.([langfuse.com][2])

---

## 4. Konfigurace citlivých dat podle prostředí

Přidej do `appsettings.Development.json`:

```json
"Observability": {
  "EnableSensitiveData": true
}
```

A do `appsettings.json` (prod):

```json
"Observability": {
  "EnableSensitiveData": false
}
```

Tato konfigurace už je použita v `ChatClientService.CreateChatClient()` (viz sekce 2).

V **dev/test** může být `true` (vidíš prompty/odpovědi v Langfuse), v **prod** typicky `false` (jen metadata, usage, model, atd.).

---

## 5. Ověření implementace

### 5.1. Lokální test

1. Spusť aplikaci v dev režimu s `EnableSensitiveData = true`.
2. Spusť agenta (typicky přes existing endpoint / UI).
3. Zkontroluj logy – při prvním běhu by neměla být chyba z OTel exporteru.

### 5.2. Kontrola v Langfuse

V Langfuse UI:

* Otevři **Traces**.

* Najdi trace se `service.name = TransactionManagement`.

* U jednoho trace ověř:

  * existuje span pro chat completion,
  * má atributy:

    * `gen_ai.operation.name: chat`,
    * `gen_ai.request.model: llama3.1:8b` (nebo jiný model z konfigurace),
    * `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.usage.total_tokens`,
    * `gen_ai.prompt.0.*`, `gen_ai.completion.0.*` (pokud je `EnableSensitiveData = true`).

* Ověř, že:

  * každé volání chat API vytvoří trace,
  * tool calls (pokud jsou) jsou vidět jako child spany.

---

## 6. Akceptační kritéria

Implementace je hotová, když:

1. **Každé volání chat API** (přes `ChatOrchestrator`) vytvoří v Langfuse jeden trace.
2. Trace obsahuje:

   * span(y) pro LLM volání (s `gen_ai.request.model`, `gen_ai.usage.*`, `gen_ai.prompt.*`, `gen_ai.completion.*`),
   * span(y) pro tool calls (pokud agent volá funkce jako `ExecuteSqlQuery`, `SearchDocuments`, atd.).
3. V **dev** prostředí (`EnableSensitiveData = true`) jsou v Langfuse vidět celé prompty/odpovědi, v **prod** (`false`) jen metadata.
4. Export probíhá přes **OTLP HTTP** na `/api/public/otel/v1/traces` s Basic Auth, bez chyb v logách.
5. **Odstraněn vlastní manuální Langfuse tracking** (současný `LangfuseService` a všechny volání `LogGenerationAsync`, `StartSpanAsync`, atd.).

---

## 7. Cleanup - odstranění starého tracking kódu

Po implementaci OpenTelemetry **smaž nebo zakomentuj**:

1. `LangfuseService.cs` - celý soubor (nahrazeno OpenTelemetry)
2. V `ChatOrchestrator.cs` - všechna volání `_langfuseService.*`
3. V `DatabaseFunctions.cs` - všechny `SetTraceId`, `StartSpanAsync`, `EndSpanAsync`
4. V `DocumentFunctions.cs` - všechny `SetTraceId`, `StartSpanAsync`, `EndSpanAsync`
5. V `EmbeddingService.cs` - všechna volání `_langfuseService.*`
6. V `LLMService.cs` - všechna volání `_langfuseService.*`

OpenTelemetry automaticky trackuje všechno bez manuálního volání API.
