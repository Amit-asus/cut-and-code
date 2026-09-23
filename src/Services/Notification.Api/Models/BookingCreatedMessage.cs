namespace SalonBooking.NotificationApi;

// Mirrors the anonymous payload shape Booking.Api's BookingEventPublisher sends onto
// the booking-created queue. Property names must match (case-insensitively — System.Text.Json
// defaults to case-insensitive deserialization) since there's no shared contract project.
public record BookingCreatedMessage(
    Guid BookingId,
    Guid CustomerId,
    Guid SalonId,
    Guid ServiceId,
    DateTime StartTime,
    DateTime EndTime);
