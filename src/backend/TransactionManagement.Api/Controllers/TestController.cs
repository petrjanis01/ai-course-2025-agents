using Microsoft.AspNetCore.Mvc;
using TransactionManagement.Api.Services;

namespace TransactionManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    /// <summary>
    /// POST /api/test/embedding - Test embedding generation
    /// </summary>
    [HttpPost("embedding")]
    public async Task<IActionResult> TestEmbedding(
        [FromBody] string text,
        [FromServices] IEmbeddingService embeddingService)
    {
        var embedding = await embeddingService.CreateEmbeddingAsync(text);

        return Ok(new
        {
            text,
            dimensions = embedding.Length,
            embedding = embedding.Take(10).ToArray(), // First 10 values
            sample = $"[{string.Join(", ", embedding.Take(5).Select(x => x.ToString("F4")))}...]"
        });
    }

    /// <summary>
    /// POST /api/test/search - Test Qdrant search
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> TestSearch(
        [FromBody] string query,
        [FromServices] IQdrantService qdrantService)
    {
        var results = await qdrantService.SearchAsync(query, limit: 5);

        return Ok(new
        {
            query,
            resultsCount = results.Count,
            results = results.Select(r => new
            {
                attachmentId = r.AttachmentId.ToString(),
                fileName = r.FileName,
                category = r.Category,
                score = r.Score,
                contentPreview = r.Content.Length > 200
                    ? r.Content.Substring(0, 200) + "..."
                    : r.Content,
                hasAmounts = r.HasAmounts,
                hasDates = r.HasDates
            })
        });
    }
}
