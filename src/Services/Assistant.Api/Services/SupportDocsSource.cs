using System.Text;
using System.Text.RegularExpressions;

namespace SalonBooking.AssistantApi;

public partial class SupportDocsSource(IWebHostEnvironment environment, ILogger<SupportDocsSource> logger)
{
    public IReadOnlyList<KnowledgeDocument> Load()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Knowledge");
        if (!Directory.Exists(folder))
        {
            folder = Path.Combine(environment.ContentRootPath, "Knowledge");
        }

        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Knowledge folder not found; no support docs will be indexed.");
            return [];
        }

        var documents = new List<KnowledgeDocument>();
        foreach (var file in Directory.GetFiles(folder, "*.md").Order())
        {
            documents.AddRange(SplitIntoSections(Path.GetFileNameWithoutExtension(file), File.ReadAllLines(file)));
        }

        return documents;
    }

    private static IEnumerable<KnowledgeDocument> SplitIntoSections(string fileSlug, string[] lines)
    {
        var docTitle = lines.FirstOrDefault(l => l.StartsWith("# "))?[2..].Trim() ?? fileSlug;

        string? heading = null;
        var body = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("# "))
            {
                continue;
            }

            if (line.StartsWith("## "))
            {
                if (TryBuild(fileSlug, docTitle, heading, body, out var section))
                {
                    yield return section;
                }

                heading = line[3..].Trim();
                body.Clear();
                continue;
            }

            body.AppendLine(line);
        }

        if (TryBuild(fileSlug, docTitle, heading, body, out var last))
        {
            yield return last;
        }
    }

    private static bool TryBuild(string fileSlug, string docTitle, string? heading, StringBuilder body, out KnowledgeDocument document)
    {
        var text = body.ToString().Trim();
        document = null!;
        if (text.Length == 0)
        {
            return false;
        }

        var title = heading is null ? docTitle : $"{docTitle}: {heading}";
        var key = heading is null ? $"doc:{fileSlug}" : $"doc:{fileSlug}#{Slugify(heading)}";

        document = new KnowledgeDocument(key, KnowledgeSourceType.SupportDoc, title, $"{title}\n\n{text}", Url: null);
        return true;
    }

    private static string Slugify(string text) => NonSlugChars().Replace(text.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
