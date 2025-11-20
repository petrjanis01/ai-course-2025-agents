using System.Text.RegularExpressions;

namespace TransactionManagement.Api.Services;

public interface IChunkingService
{
    List<TextChunk> SplitIntoChunks(string text, int targetTokens = 800, int overlapTokens = 100);
}

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    public List<TextChunk> SplitIntoChunks(string text, int targetTokens = 800, int overlapTokens = 100)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextChunk>();
        }

        // Split text into sentences
        var sentences = SplitIntoSentences(text);
        var chunks = new List<TextChunk>();
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokenCount(sentence);

            // If adding this sentence would exceed target, save current chunk
            if (currentTokenCount + sentenceTokens > targetTokens && currentChunk.Any())
            {
                var chunkText = string.Join(" ", currentChunk);
                chunks.Add(new TextChunk
                {
                    Content = chunkText,
                    TokenCount = currentTokenCount
                });

                // Create overlap: keep last few sentences
                var overlapSentences = GetOverlapSentences(currentChunk, overlapTokens);
                currentChunk = overlapSentences.ToList();
                currentTokenCount = overlapSentences.Sum(EstimateTokenCount);
            }

            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokens;
        }

        // Add remaining chunk
        if (currentChunk.Any())
        {
            var chunkText = string.Join(" ", currentChunk);
            chunks.Add(new TextChunk
            {
                Content = chunkText,
                TokenCount = currentTokenCount
            });
        }

        _logger.LogInformation("Split text into {ChunkCount} chunks (avg {AvgTokens} tokens each)",
            chunks.Count,
            chunks.Any() ? chunks.Average(c => c.TokenCount) : 0);

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Split by sentence-ending punctuation followed by space/newline
        var pattern = @"(?<=[.!?])\s+(?=[A-ZÁČĎÉĚÍŇÓŘŠŤÚŮÝŽ])";
        var sentences = Regex.Split(text, pattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        return sentences;
    }

    private int EstimateTokenCount(string text)
    {
        // Rough estimation: ~4 characters per token
        return text.Length / 4;
    }

    private List<string> GetOverlapSentences(List<string> sentences, int targetOverlapTokens)
    {
        var overlap = new List<string>();
        var overlapTokenCount = 0;

        // Take sentences from the end until we reach target overlap
        for (int i = sentences.Count - 1; i >= 0; i--)
        {
            var sentence = sentences[i];
            var sentenceTokens = EstimateTokenCount(sentence);

            if (overlapTokenCount + sentenceTokens > targetOverlapTokens && overlap.Any())
            {
                break;
            }

            overlap.Insert(0, sentence);
            overlapTokenCount += sentenceTokens;
        }

        return overlap;
    }
}

public class TextChunk
{
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
}
