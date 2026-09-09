namespace SalonBooking.BookingApi;

public record CreateBookingRequest(Guid SalonId, Guid ServiceId, DateTime StartTime, DateTime EndTime);
