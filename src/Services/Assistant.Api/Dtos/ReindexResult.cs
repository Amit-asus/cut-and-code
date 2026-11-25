namespace SalonBooking.AssistantApi;

public record ReindexResult(bool Ran, int Added, int Updated, int Removed, int Unchanged, string? Message = null);
