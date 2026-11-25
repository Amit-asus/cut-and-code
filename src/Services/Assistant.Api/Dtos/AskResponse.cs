namespace SalonBooking.AssistantApi;

public record AskResponse(
    bool Answered,
    string Answer,
    IReadOnlyList<AnswerSource> Sources,
    BookingProposal? Proposal = null);

public record AnswerSource(string Title, string? Url, string Snippet);

public record BookingProposal(
    Guid SalonId,
    Guid ServiceId,
    string SalonName,
    string ServiceName,
    decimal Price,
    DateTime StartTime,
    DateTime EndTime);
