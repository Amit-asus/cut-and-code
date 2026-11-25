namespace SalonBooking.AssistantApi;

public record KnowledgeDocument(
    string SourceKey,
    KnowledgeSourceType SourceType,
    string Title,
    string Content,
    string? Url);
