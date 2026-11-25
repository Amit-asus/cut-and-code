namespace SalonBooking.AssistantApi;

public static class AssistantEndpoints
{
    private const int MaxQuestionLength = 500;
    private const int MaxHistoryTurns = 50;

    public static void MapAssistantEndpoints(this WebApplication app)
    {
        app.MapPost("/assistant/ask", async (AskRequest request, HttpContext httpContext, AssistantService assistant, CancellationToken ct) =>
        {
            var question = request.Question?.Trim();
            if (string.IsNullOrEmpty(question))
            {
                return Results.BadRequest("Question is required.");
            }

            if (question.Length > MaxQuestionLength)
            {
                return Results.BadRequest($"Question must be {MaxQuestionLength} characters or fewer.");
            }

            if (request.History is { Count: > MaxHistoryTurns })
            {
                return Results.BadRequest($"History must have {MaxHistoryTurns} messages or fewer.");
            }

            var authorization = httpContext.Request.Headers.Authorization.ToString();
            return Results.Ok(await assistant.AskAsync(
                question,
                request.History,
                string.IsNullOrWhiteSpace(authorization) ? null : authorization,
                ct));
        });

        app.MapPost("/assistant/reindex", async (KnowledgeIndexer indexer, CancellationToken ct) =>
        {
            var result = await indexer.ReindexAsync("manual", ct);
            return result.Ran ? Results.Ok(result) : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }
}
