using System.Net.Http.Json;
using System.Text.Json;

namespace SalonBooking.NotificationApi;

public record AiEmail(string Subject, string Body);

public class AiEmailWriter(HttpClient httpClient, IConfiguration configuration, ILogger<AiEmailWriter> logger)
{
    public async Task<AiEmail?> WriteBookingConfirmationAsync(EnrichedBooking booking, DateTime startLocal)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Gemini:ApiKey is not configured, skipping AI email.");
            return null;
        }

        var models = configuration.GetSection("Gemini:Models").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : ["gemini-3.8-flash"];

        var prompt = $$"""
            You write short, warm booking confirmation emails for a salon booking app.
            Booking details:
            - Service: {{booking.ServiceName}}
            - Salon: {{booking.SalonName}}
            - Date and time: {{startLocal:dddd, d MMMM yyyy 'at' h:mm tt}} (IST)
            Greet the customer as "Hi there". Confirm the details, add one practical tip to
            prepare for this specific service, and sign off as "The SalonBooking Team".
            Keep the body under 120 words. Plain text only: no markdown, no HTML.
            Respond with JSON only: {"subject": "...", "body": "..."}
            """;

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        foreach (var model in models)
        {
            var email = await TryModelAsync(model, apiKey, requestBody);
            if (email is not null)
            {
                logger.LogInformation("AI email written by {Model}", model);
                return email;
            }
        }

        return null;
    }

    private async Task<AiEmail?> TryModelAsync(string model, string apiKey, object requestBody)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-goog-api-key", apiKey);

        try
        {
            var response = await httpClient.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Gemini call to {Model} failed: {StatusCode} {Body}", model, response.StatusCode, raw);
                return null;
            }

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var email = JsonSerializer.Deserialize<AiEmail>(text ?? "", JsonSerializerOptions.Web);
            if (string.IsNullOrWhiteSpace(email?.Subject) || string.IsNullOrWhiteSpace(email.Body))
            {
                logger.LogWarning("Gemini {Model} returned an empty or malformed email: {Text}", model, text);
                return null;
            }

            return email;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gemini call to {Model} threw.", model);
            return null;
        }
    }
}
