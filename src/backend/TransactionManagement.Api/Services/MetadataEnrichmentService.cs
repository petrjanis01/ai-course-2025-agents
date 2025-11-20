using System.Text.RegularExpressions;

namespace TransactionManagement.Api.Services;

public interface IMetadataEnrichmentService
{
    DocumentMetadata EnrichMetadata(string content);
}

public class MetadataEnrichmentService : IMetadataEnrichmentService
{
    // Regex patterns
    private static readonly Regex AmountPattern = new(@"\d+[\s,]*\d*\s*(Kč|EUR|CZK|€|\$)", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"\d{1,2}\.\s*\d{1,2}\.\s*\d{4}", RegexOptions.Compiled);
    private static readonly Regex IsoDatePattern = new(@"\d{4}-\d{2}-\d{2}", RegexOptions.Compiled);

    public DocumentMetadata EnrichMetadata(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new DocumentMetadata();
        }

        var hasAmounts = AmountPattern.IsMatch(content);
        var hasDates = DatePattern.IsMatch(content) || IsoDatePattern.IsMatch(content);
        var wordCount = CountWords(content);

        return new DocumentMetadata
        {
            HasAmounts = hasAmounts,
            HasDates = hasDates,
            WordCount = wordCount
        };
    }

    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}

public class DocumentMetadata
{
    public bool HasAmounts { get; set; }
    public bool HasDates { get; set; }
    public int WordCount { get; set; }
}
