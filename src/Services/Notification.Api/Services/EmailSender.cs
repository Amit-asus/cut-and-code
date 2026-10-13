using System.Net;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SalonBooking.NotificationApi;

public class EmailSender(IConfiguration configuration, AiEmailWriter aiEmailWriter, ILogger<EmailSender> logger)
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
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(message.StartTime, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

        string subject, plainTextContent, htmlContent;
        AiEmail? aiEmail = await aiEmailWriter.WriteBookingConfirmationAsync(booking, startLocal);
        if (aiEmail is not null)
        {
            subject = aiEmail.Subject;
            plainTextContent = aiEmail.Body;
            htmlContent = $"<p>{WebUtility.HtmlEncode(aiEmail.Body).Replace("\n", "<br>")}</p>";
            logger.LogInformation("Using AI-written email for booking {BookingId}", message.BookingId);
        }
        else
        {
            subject = $"Booking confirmed: {booking.ServiceName} at {booking.SalonName}";
            plainTextContent =
                $"Your booking for {booking.ServiceName} at {booking.SalonName} on {startLocal:f} is confirmed.";
            htmlContent =
                $"<p>Your booking for <strong>{booking.ServiceName}</strong> at <strong>{booking.SalonName}</strong> on {startLocal:f} is confirmed.</p>";
        }

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
