using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace SalonBooking.AssistantApi;

public class KnowledgeIndexer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<KnowledgeIndexer> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ReindexResult> ReindexAsync(string trigger, CancellationToken ct = default)
    {
        if (!await gate.WaitAsync(0, ct))
        {
            return new ReindexResult(false, 0, 0, 0, 0, "A reindex is already running.");
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var gemini = scope.ServiceProvider.GetRequiredService<GeminiClient>();
            if (!gemini.IsConfigured)
            {
                logger.LogWarning("Skipping {Trigger} reindex: Gemini:ApiKey is not configured.", trigger);
                return new ReindexResult(false, 0, 0, 0, 0, "Gemini:ApiKey is not configured.");
            }

            var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
            var supportDocs = scope.ServiceProvider.GetRequiredService<SupportDocsSource>();
            var salonCatalog = scope.ServiceProvider.GetRequiredService<SalonCatalogSource>();

            var documents = supportDocs.Load()
                .Concat(await salonCatalog.LoadAsync(ct))
                .ToDictionary(d => d.SourceKey);

            var existing = await db.KnowledgeChunks
                .Select(c => new { c.Id, c.SourceKey, c.ContentHash })
                .ToDictionaryAsync(c => c.SourceKey, ct);

            var embeddingModel = configuration["Gemini:EmbeddingModel"] ?? "gemini-embedding-001";
            var changed = documents.Values
                .Select(d => (Document: d, Hash: Hash(d, embeddingModel)))
                .Where(x => !existing.TryGetValue(x.Document.SourceKey, out var current) || current.ContentHash != x.Hash)
                .ToList();

            var embeddings = changed.Count == 0
                ? []
                : await gemini.EmbedAsync(
                    changed.Select(x => new EmbeddingInput(x.Document.Content, x.Document.Title)).ToList(),
                    EmbeddingTask.Document,
                    ct);

            var removedKeys = existing.Keys.Except(documents.Keys).ToList();
            if (removedKeys.Count > 0)
            {
                await db.KnowledgeChunks.Where(c => removedKeys.Contains(c.SourceKey)).ExecuteDeleteAsync(ct);
            }

            int added = 0, updated = 0;
            for (var i = 0; i < changed.Count; i++)
            {
                var (document, hash) = changed[i];
                var chunk = new KnowledgeChunk
                {
                    SourceKey = document.SourceKey,
                    SourceType = document.SourceType,
                    Title = document.Title,
                    Content = document.Content,
                    Url = document.Url,
                    ContentHash = hash,
                    Embedding = new Vector(embeddings[i]),
                    UpdatedAt = DateTime.UtcNow
                };

                if (existing.TryGetValue(document.SourceKey, out var current))
                {
                    chunk.Id = current.Id;
                    db.KnowledgeChunks.Update(chunk);
                    updated++;
                }
                else
                {
                    chunk.Id = Guid.NewGuid();
                    db.KnowledgeChunks.Add(chunk);
                    added++;
                }
            }

            await db.SaveChangesAsync(ct);

            var result = new ReindexResult(true, added, updated, removedKeys.Count, documents.Count - changed.Count);
            logger.LogInformation(
                "Knowledge reindex ({Trigger}) done: {Added} added, {Updated} updated, {Removed} removed, {Unchanged} unchanged.",
                trigger, result.Added, result.Updated, result.Removed, result.Unchanged);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Knowledge reindex ({Trigger}) failed; existing index left as it was.", trigger);
            return new ReindexResult(false, 0, 0, 0, 0, $"Reindex failed: {ex.Message}");
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Hash(KnowledgeDocument document, string embeddingModel)
    {
        var input = $"{embeddingModel}|{KnowledgeChunk.EmbeddingDimensions}|{document.SourceType}|{document.Title}|{document.Url}|{document.Content}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}
