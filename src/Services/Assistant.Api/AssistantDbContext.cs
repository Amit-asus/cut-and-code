using Microsoft.EntityFrameworkCore;

namespace SalonBooking.AssistantApi;

public class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<KnowledgeChunk>(chunk =>
        {
            chunk.HasIndex(c => c.SourceKey).IsUnique();
            chunk.Property(c => c.SourceType).HasConversion<string>();
            chunk.Property(c => c.Embedding).HasColumnType($"vector({KnowledgeChunk.EmbeddingDimensions})");
            chunk.HasIndex(c => c.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
        });
    }
}
