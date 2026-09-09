namespace SalonBooking.SalonApi;

public class Salon
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ImageUrl { get; set; }

    public List<Service> Services { get; set; } = new();
}
