namespace SalonBooking.SalonApi;

public record CreateSalonRequest(string Name, string Address, Guid OwnerId);
