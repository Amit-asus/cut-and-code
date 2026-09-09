namespace SalonBooking.SalonApi;

public class Service
{
    public Guid Id { get; set; }
    public Guid SalonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    // Kept for EF (queries/includes) but excluded from JSON — otherwise Salon.Services[].Salon
    // points straight back to the parent Salon, and the serializer loops forever.
    [System.Text.Json.Serialization.JsonIgnore]
    public Salon? Salon { get; set; }
}


