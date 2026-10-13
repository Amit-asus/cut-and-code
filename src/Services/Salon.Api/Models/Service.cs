namespace SalonBooking.SalonApi;

public class Service
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Salon? Salon { get; set; }
}


