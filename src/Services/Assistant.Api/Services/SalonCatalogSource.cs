using System.Globalization;
using System.Net.Http.Json;

namespace SalonBooking.AssistantApi;

public class SalonCatalogSource(HttpClient httpClient)
{
    public async Task<IReadOnlyList<SalonSnapshot>> GetSalonsAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<SalonSnapshot>>(
            "https+http://salon-api/internal/knowledge/salons", ct) ?? [];

    public async Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(CancellationToken ct = default)
    {
        var salons = await GetSalonsAsync(ct);

        var documents = new List<KnowledgeDocument>();
        foreach (var salon in salons)
        {
            var url = $"/salons/{salon.Id}";
            var serviceList = salon.Services.Count == 0
                ? "No services listed yet."
                : string.Join(", ", salon.Services.Select(s => $"{s.Name} ({FormatPrice(s.Price)})"));

            documents.Add(new KnowledgeDocument(
                $"salon:{salon.Id}",
                KnowledgeSourceType.Salon,
                salon.Name,
                $"Salon: {salon.Name}\nAddress: {salon.Address}\nServices offered: {serviceList}",
                url));

            foreach (var service in salon.Services)
            {
                documents.Add(new KnowledgeDocument(
                    $"service:{service.Id}",
                    KnowledgeSourceType.Service,
                    $"{service.Name} at {salon.Name}",
                    $"{salon.Name} (address: {salon.Address}) offers the service \"{service.Name}\" for {FormatPrice(service.Price)}.",
                    url));
            }
        }

        return documents;
    }

    private static string FormatPrice(decimal price) =>
        "₹" + price.ToString("0.##", CultureInfo.InvariantCulture);
}
