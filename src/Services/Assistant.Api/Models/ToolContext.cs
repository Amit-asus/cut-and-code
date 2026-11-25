namespace SalonBooking.AssistantApi;

public class ToolContext(string? authorization)
{
    public string? Authorization { get; } = authorization;

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Authorization);

    public BookingProposal? Proposal { get; set; }
}
