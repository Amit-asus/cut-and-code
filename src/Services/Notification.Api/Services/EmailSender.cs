using SendGrid;
using SendGrid.Helpers.Mail;

namespace SalonBooking.NotificationApi;

// Sends the actual booking confirmation email via SendGrid. ApiKey comes from
// SendGrid:ApiKey (set via dotnet user-secrets locally, same pattern as Jwt:SigningKey —
// never committed). FromEmail/FromName are not secret, so they live in appsettings.json.
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
{
    public async Task SendBookingConfirmationAsync(EnrichedBooking booking, BookingCreatedMessage message)
    {
        var apiKey = configuration["SendGrid:ApiKey"]
            ?? throw new InvalidOperationException("SendGrid:ApiKey is not configured.");
        var fromEmail = configuration["SendGrid:FromEmail"]
            ?? throw new InvalidOperationException("SendGrid:FromEmail is not configured.");
        var fromName = configuration["SendGrid:FromName"] ?? "SalonBooking";

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress(booking.CustomerEmail);
        var subject = $"Booking confirmed: {booking.ServiceName} at {booking.SalonName}";
        var plainTextContent =
            $"Your booking for {booking.ServiceName} at {booking.SalonName} on {message.StartTime} is confirmed.";
        var htmlContent =
            $"<p>Your booking for <strong>{booking.ServiceName}</strong> at <strong>{booking.SalonName}</strong> on {message.StartTime} is confirmed.</p>";

        var email = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await client.SendEmailAsync(email);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync();
            logger.LogWarning(
                "SendGrid send failed for booking {BookingId}: {StatusCode} {Body}",
                message.BookingId, response.StatusCode, body);
            return;
        }

        logger.LogInformation("Booking confirmation email sent to {Email} for booking {BookingId}",
            booking.CustomerEmail, message.BookingId);
    }
}
