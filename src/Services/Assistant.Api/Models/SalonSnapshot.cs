namespace SalonBooking.AssistantApi;

public record SalonSnapshot(Guid Id, string Name, string Address, List<ServiceSnapshot> Services);

public record ServiceSnapshot(Guid Id, string Name, decimal Price);
