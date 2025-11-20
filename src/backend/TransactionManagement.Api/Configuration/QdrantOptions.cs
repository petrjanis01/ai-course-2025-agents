namespace TransactionManagement.Api.Configuration;

public class QdrantOptions
{
    public string Url { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "transaction_documents";
    public int VectorSize { get; set; } = 384;
}
