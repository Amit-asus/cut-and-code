namespace SalonBooking.NotificationApi;

public record BookingCreatedMessage(
    Guid BookingId,
    Guid CustomerId,
    Guid SalonId,
    Guid ServiceId,
    DateTime StartTime,
    DateTime EndTime);
