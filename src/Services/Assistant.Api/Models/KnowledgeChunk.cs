using Pgvector;

namespace SalonBooking.AssistantApi;

public class KnowledgeChunk
{
    public const int EmbeddingDimensions = 768;

    public Guid Id { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public KnowledgeSourceType SourceType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
    public DateTime UpdatedAt { get; set; }
}

public enum KnowledgeSourceType
{
    SupportDoc,
    Salon,
    Service
}
