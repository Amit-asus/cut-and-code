using System.Text.Json;

namespace SalonBooking.BookingApi;

public interface IBookingEventPublisher
{
    Task PublishBookingCreatedAsync(Booking booking);

    protected static string ToJson(Booking booking) => JsonSerializer.Serialize(new
    {
        BookingId = booking.Id,
        CustomerId = booking.CustomerId,
        SalonId = booking.SalonId,
        ServiceId = booking.ServiceId,
        StartTime = booking.StartTime,
        EndTime = booking.EndTime
    });
}
