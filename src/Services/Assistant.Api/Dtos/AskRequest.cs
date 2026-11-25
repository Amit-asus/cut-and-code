namespace SalonBooking.AssistantApi;

public record ChatTurn(string Role, string Text);

public record AskRequest(string Question, List<ChatTurn>? History = null);
