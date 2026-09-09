using Microsoft.EntityFrameworkCore;

namespace SalonBooking.SalonApi;

public class SalonDbContext : DbContext
{
    public SalonDbContext(DbContextOptions<SalonDbContext> options) : base(options)
    {
    }

    public DbSet<Salon> Salons => Set<Salon>();
    public DbSet<Service> Services => Set<Service>();
}
