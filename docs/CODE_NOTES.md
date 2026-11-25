# Code Notes

Design decisions and "why" explanations for the SalonBooking codebase, kept here instead of inline in the source.

---

## AppHost (`apphost.cs`)

- **Blob storage (Azurite)** — `AddAzureStorage("storage").RunAsEmulator()` runs Azurite, a local Azure Storage emulator, in a container instead of a real Azure account. Same idea as `AddPostgres`/`AddRedis`, just for file storage.
- **`WithDataVolume("storage-data")`** — persists the emulator's files across restarts (like `ContainerLifetime.Persistent` on postgres/cache, but for the data itself rather than the container).
- **`WithBlobPort(10000)`** — pins the blob endpoint to a fixed host port. Other services find each other through Aspire service discovery, so random ports are fine for them. But salon-api stores the *browser-facing* blob URL in `Salon.ImageUrl` (the browser loads images directly from blob storage, not through salon-api), so that URL must stay valid across restarts or every uploaded image breaks.
- **`salon-images` container** — one container ("bucket"); salon-api receives a client scoped directly to it.
- **Service Bus emulator** — the async "mailbox" between booking-api (publisher) and notification-api (consumer). booking-api drops a message on the `booking-created` queue after a successful booking and moves on; notification-api picks it up whenever it's ready.
- **`salonApi.WithReference(salonImages)`** — gives salon-api the blob container connection info, injected the same way as the `salondb` connection string.
- **notification-api references identity-api and salon-api** — it calls their internal endpoints directly (not through the Gateway) to enrich a booking-created event before emailing. The references make `https://identity-api` / `https://salon-api` resolve via Aspire service discovery, same as the Gateway's YARP cluster addresses.

## Identity.Api

### `Endpoints/AuthEndpoints.cs`
- **`POST /register`** — public sign-up. Always creates a Customer; a caller can never self-assign Staff/Admin.
- **`POST /login`** — verifies credentials and issues a JWT. Returns the same generic 401 for "no such user" and "wrong password" to prevent user enumeration.
- **`GET /me`** — returns the caller's identity from their token's claims. Requires any valid JWT.
- **`GET /profile`** — returns the caller's profile from the database (unlike `/me`, which only echoes token claims), so it includes fields that never go into the JWT, such as `CreatedAt`.
- **`POST /admin/users`** — Admin-only; creates a user with any role. Requires the `Admin` role, not just any valid token.
- **`GET /internal/users/{id}`** — service-to-service only (not routed through the Gateway), so no auth. Lets Notification.Api resolve a customer's email without its own copy of Identity.Api's user data.

## Salon.Api

### `Endpoints/SalonEndpoints.cs`
- **Read one salon** — `FindAsync` doesn't load navigation properties; `Include` is required or `salon.Services` always comes back empty.
- **Image upload — public access** — blob containers are private by default, so without this the `ImageUrl` returned to the browser would 403 when used as an `<img src>` (no SAS token). `PublicAccessType.Blob` allows anonymous read of blobs, but not container listing.
- **Why `SetAccessPolicyAsync` on every request** — `CreateIfNotExistsAsync` only applies the access type when it *creates* the container; it's a no-op once the container exists. `SetAccessPolicyAsync` applies unconditionally, so a container created earlier still gets switched to public.
- **Blob naming `{salonId}/{guid}-{fileName}`** — a unique name per upload so re-uploads never collide with or overwrite each other (and don't get served from a stale browser cache).
- **Create/Update service return a plain shape** — `service.Salon` and `salon.Services` point at each other (EF navigation fixup), which crashes JSON serialization with "possible object cycle" if the raw entity is returned.
- **`GET /internal/salons/{salonId}/services/{serviceId}`** — service-to-service only, no auth. Lets Notification.Api resolve salon/service names without its own copy of Salon.Api's data.

### `Models/Service.cs`
- The `Salon` navigation property is kept for EF (queries/includes) but excluded from JSON (`[JsonIgnore]`); otherwise `Salon.Services[].Salon` points back to the parent and serialization loops forever.

## Booking.Api

### `Endpoints/BookingEndpoints.cs`
- **Cache JSON options** — match ASP.NET Core's default `Results.Ok(...)` serialization (camelCase), so a cache hit and a cache miss return identical JSON.
- **Best-effort cache invalidation** — by the time invalidation runs, the booking is already saved in Postgres (the source of truth). If Redis is briefly unavailable, the error is swallowed rather than failing the booking. Worst case: the slot picker shows a stale slot until the cache TTL expires — but the `POST /bookings` conflict check still prevents an actual double-booking. A caching concern should never block the core feature.
- **`POST /bookings`** — creates a booking; rejects if the same Service already has an overlapping Confirmed booking.
- **`GET /bookings/availability`** — fixed 1-hour slots, 9am–9pm IST, each flagged booked/available. Any authenticated user can call it (needed to render the slot picker); it reveals only *whether* a slot is taken, never who holds it.
  - Cache read is also best-effort: if Redis is unreachable, fall through and compute from Postgres. A cache outage should make things slower, never broken.
  - "9am–9pm" means salon-local (IST) hours. The code builds midnight in IST and converts to UTC, because bookings are stored and compared in UTC. Previously slots were literally 9:00–20:00 UTC, which the frontend displayed shifted (e.g. 2:30 PM–1:30 AM for an IST viewer).
- **`GET /bookings`** — the current user's own bookings, newest first, paged.
- **`DELETE /bookings/{id:guid}`** — soft-delete (sets `Status = Cancelled`, keeps the row). Only the owning customer can cancel. The `:guid` constraint stops this route from swallowing `/bookings/availability` — without it, "availability" would match `{id}` and `GET /bookings/availability` would return 405.

### `Services/BookingEventPublisher.cs`
- Publishes a "booking created" message to Service Bus after a booking is saved. Fire-and-forget from the caller's point of view: the booking already succeeded in Postgres, so a Service Bus outage degrades to "no notification sent" rather than failing the booking — same philosophy as the Redis cache invalidation.

## Notification.Api

### `Program.cs`
- **Named `HttpClient` for enrichment** — `AddServiceDefaults()` wires Aspire service discovery into `HttpClientFactory` clients, so `https://identity-api` / `https://salon-api` resolve without hardcoded ports.
- **Typed client for Gemini with longer timeouts** — ServiceDefaults' standard resilience handler caps each attempt at 10s: fine for internal services, too short for an LLM. This client swaps it for a copy with longer timeouts. `RemoveAllResilienceHandlers` is experimental (`EXTEXP0001`), opted into here only.
- Polly validation requires the circuit breaker's sampling window to be at least 2× the attempt timeout.

### `Services/BookingNotificationConsumer.cs`
- A long-running `BackgroundService` that listens on the `booking-created` queue for the app's lifetime, independent of any HTTP request.
- Uses `IServiceScopeFactory` instead of injecting `BookingEnrichmentService`/`EmailSender` directly: this class is a singleton while HttpClient-backed services are typically scoped, so each message gets its own scope (same lifetime rules as a per-request scope).
- A message is completed (removed from the queue) only after it's fully processed. If processing throws, `ProcessErrorAsync` logs it and the message stays in the queue for retry instead of vanishing.
- `ExecuteAsync` waits on the stopping token to keep the service alive; `StopAsync` (called on shutdown) stops the processor. `OperationCanceledException` there is expected on shutdown.

### `Services/BookingEnrichmentService.cs`
- Calls Identity.Api's and Salon.Api's internal endpoints to turn the Guids on a booking-created message into readable values (email, salon name, service name) for the email.

### `Services/EmailSender.cs`
- Sends the confirmation email via SendGrid. `SendGrid:ApiKey` comes from `dotnet user-secrets` (never committed, same pattern as `Jwt:SigningKey`); `FromEmail`/`FromName` aren't secret and live in `appsettings.json`.
- Bookings are stored in UTC; the email shows the customer's local (IST) time, the same zone the availability endpoint uses.
- AI-written text is treated as plain text and HTML-encoded, rather than trusting model-generated HTML.

### `Services/AiEmailWriter.cs`
- Asks Gemini to write a friendly confirmation email. Best-effort: returns `null` on any failure (no key, timeout, rate limit, bad JSON) so `EmailSender` falls back to the plain template — an AI outage never stops a customer getting their confirmation.
- Models are tried in order: free-tier models often return 503 "high demand", so a busy primary model falls through to a lighter one before giving up.
- The API key goes in a header rather than the URL so it never appears in logged request URLs.
- Gemini wraps the answer: `candidates[0].content.parts[0].text` holds the JSON string.

### `Models/BookingCreatedMessage.cs`
- Mirrors the payload Booking.Api's `BookingEventPublisher` sends. Property names must match (case-insensitively — System.Text.Json deserializes case-insensitively by default) since there's no shared contract project.

## Assistant.Api (RAG support assistant)

How it works in one line: turn every help article section and salon/service listing into an embedding (a vector of numbers that captures meaning), store them in Postgres with pgvector, and for each question find the nearest vectors and let Gemini answer *only* from those texts.

### `Models/KnowledgeChunk.cs`
- `SourceKey` (`doc:cancellation-policy#refunds`, `salon:{id}`, `service:{id}`) is the stable identity used to decide add/update/delete on reindex.
- `ContentHash` covers the text **and** the embedding model + dimensions, so switching models re-embeds everything automatically.
- `vector(768)`: Gemini's embedding model defaults to 3072 dims, but pgvector's HNSW index supports at most 2000, and 768 is cheaper with near-identical retrieval quality.

### `AssistantDbContext.cs`
- `HasPostgresExtension("vector")` makes the migration run `CREATE EXTENSION vector`.
- HNSW index with `vector_cosine_ops` — must match the operator used in queries (`CosineDistance` → `<=>`), or Postgres ignores the index.

### `Services/GeminiClient.cs`
- Documents are embedded with `taskType: RETRIEVAL_DOCUMENT` (plus a title), questions with `RETRIEVAL_QUERY` — Gemini tunes the vectors for asymmetric search this way. Both must use the same model and dimensions or distances are meaningless.
- `batchEmbedContents` takes up to 100 texts per call, so reindexing is a handful of requests, not one per chunk.
- Same resilience override as Notification.Api's `AiEmailWriter` (LLM calls outlast the default 10s attempt timeout).

### `Services/KnowledgeIndexer.cs`
- Singleton with a `SemaphoreSlim` so startup, cron and the admin endpoint can't run two reindexes at once; it creates its own DI scope for the scoped `DbContext`.
- Embeds first, writes second: if Gemini or Salon.Api fails, nothing in the database has changed yet.

### `Services/ReindexScheduler.cs`
- This is the project's cron job. `Cronos` parses a standard 5-field cron expression; the loop sleeps until the next occurrence. Startup reindex retries a few times because Salon.Api may not be ready yet on AWS (locally the AppHost's `WaitFor(salonApi)` covers it).

### `Services/AssistantService.cs`
- Each question: retrieve help chunks (RAG) as before, then run a **tool loop** with Gemini function calling (max 5 rounds): Gemini either answers in text or asks for a tool; the code runs the tool, sends the result back, and asks again.
- Retrieval no longer short-circuits when nothing is close enough — booking messages ("tomorrow at 3pm") don't match help articles, but the tools can still answer them. The "don't guess" guard is now the prompt plus the `NO_ANSWER` marker, which maps to the fallback reply. (JSON mode can't be combined with function calling, which is why the answer is plain text with `[n]` citations instead of `{answered, answer, sourceIds}`.)
- Gemini's reply content is appended to the conversation **exactly as received** (`JsonElement`), because newer models attach a thought signature to function calls that must be sent back unchanged. Later rounds prefer the model that started the loop for the same reason.
- Chat memory: the widget sends the last 10 turns; the server keeps the last 10, truncates each to 1000 chars and drops leading model turns (Gemini expects the conversation to start with the user). Small-talk shortcut only applies to the first message, so "ok"/"yes" mid-conversation reach the model.
- Sources are fenced in `<source>` tags and the prompt says they are data — salon names/addresses are typed by Staff, so they're untrusted input from the model's point of view.

### `Services/BookingTools.cs`
- Three tools: `search_salons` (Salon.Api `/internal/knowledge/salons`, the same call the indexer uses), `get_availability` and `propose_booking` (both Booking.Api `/bookings/availability`, forwarding the customer's own `Authorization` header — Assistant.Api never holds a token of its own).
- **The AI never books.** `propose_booking` only validates (salon owns service, slot exists, is free, is in the future) and stores a `BookingProposal` in the per-request `ToolContext`; the response carries it to the widget, and the customer's Confirm click calls the existing `POST /bookings`. So double-booking prevention, cache invalidation and the confirmation email all stay on the one existing path, and a misunderstood message can't create a booking.
- The proposal (names, price, times) is built by the server from Salon.Api/Booking.Api data, not from the model's arguments, so the model can't put a wrong price on the card.
- Tools take and return IST `HH:mm` times and `yyyy-MM-dd` dates (what customers say); UTC stays internal. Tool failures return an `error` object instead of throwing, so the model can explain the problem.

### `Web/src/components/ChatWidget.jsx`
- Rendered once in `Layout`, so it's on every page. Salon sources are in-app links (closing the panel on click); help-article sources have no page, so they expand to show the snippet instead.
- A reply with a `proposal` renders a booking card; Confirm calls `api.createBooking` (same as `SalonDetailPage`) and shows the error inline on failure (e.g. a 409 if someone took the slot in between).

## ServiceDefaults (`Extensions.cs`)

Standard Aspire template: adds service discovery, resilience, health checks and OpenTelemetry to every service that references it. Health check requests are excluded from tracing. `/health` requires all checks to pass (readiness); `/alive` requires only checks tagged `live` (liveness). These endpoints are mapped only in Development — exposing them in production has security implications (see https://aka.ms/aspire/healthchecks). Optional extras from the template (not enabled): restricting service discovery to `https`, gRPC client instrumentation, the Azure Monitor exporter.

## Web (frontend)

- **`index.html` inline script** — applies the saved/OS theme before first paint so dark-mode users don't see a white flash.
- **`index.css`** — the dark theme reuses the same CSS variable names with different values under `:root[data-theme="dark"]`, so every rule using `var(--…)` switches automatically. The header chip's entry animation uses `backwards` fill mode, not `both`, because a held end state would override the `:hover` transform.
- **`ThemeContext.jsx`** — if storage is blocked (private mode, etc.), it falls back to the OS preference; failing to save the theme is non-fatal.
- **`AvatarContext.jsx`** — the user picks their avatar on the Profile page (never guessed from name/email). It's stored per user id in this browser's localStorage only, so it doesn't follow them to other devices. A version counter is bumped on every change so consumers re-render.
- **`Avatar.jsx`** — renders only the inner content (image or initials); the parent owns the circle's size and styling.
- **`ConfirmModal.jsx`** — native `<dialog>` + `showModal()` gives focus trapping, Esc-to-close and a backdrop for free. The `cancel` event (Esc) is intercepted so React state stays the source of truth. A click whose target is the `<dialog>` itself landed on the backdrop, so it closes the modal.
- **`LoginPage.jsx`** — pages that send the user to login (e.g. "Log in to book") pass `state.from` so the user is returned there afterwards; only in-app paths are accepted.
- **`MyBookingsPage.jsx`** — cancelling the last booking on the last page steps back one page. Bookings only store `SalonId`/`ServiceId`, so names are looked up from Salon.Api with one request per distinct salon on the page (`GET /salons/{id}` includes its services).
- **`ProfilePage.jsx`** — fetches page 1 with size 1: only the total count and most recent booking are needed.
- **`utils/user.js`** — initials helper: `"amit.kumar@x.com"` → `"AK"`, `"jane@x.com"` → `"JA"`.

---

## Running on AWS (provider switches)

The same code runs on the laptop (Azure emulators under Aspire) and on AWS. Each service picks an implementation at start-up from configuration; with nothing set, it behaves exactly as before.

- **Salon.Api photos** — the upload endpoint depends on `IImageStorage`, not on a cloud SDK. `Storage:Provider = S3` registers `S3ImageStorage`; anything else registers `AzureBlobImageStorage` (Azurite). The choice is made in `Extensions/StorageExtensions.cs`, which also skips registering the Azure client on AWS, where there is no connection string for it.
- **S3 image URL** — `S3ImageStorage` returns `https://{bucket}.s3.{region}.amazonaws.com/{key}` unless `Storage:S3:PublicBaseUrl` is set (for a CloudFront address). Each part of the key is URL-escaped because file names can contain spaces. The bucket itself must allow public reads, or the browser gets 403 on every image — the code does not set that.
- **Booking.Api publisher** — the endpoint depends on `IBookingEventPublisher`. `Messaging:Provider = Sqs` registers `SqsBookingEventPublisher`; anything else registers the Service Bus `BookingEventPublisher`. Both build the message body with the shared `IBookingEventPublisher.ToJson`, so the two queues carry the identical payload.
- **SQS publisher catches every exception**, not only SQS ones — missing credentials and network failures surface as other exception types, and a notification problem must never fail a booking.
- **Notification.Api consumer** — `BookingNotificationHandler` holds the "enrich, then email" logic once; `BookingNotificationConsumer` (Service Bus) and `SqsBookingNotificationConsumer` (SQS) only differ in how they receive and acknowledge.
- **SQS consumer loop** — long-polls for up to 20 seconds per call, so an empty queue costs almost no requests. A message is deleted only after the handler succeeds; on failure it is left alone and SQS redelivers it after the queue's visibility timeout. An unparseable message is deleted, since retrying can never fix it. `response.Messages` is null (not empty) when nothing arrived in AWS SDK v4, hence the `?? []`.
- **`https+http://identity-api`** (Gateway clusters and `BookingEnrichmentService`) — service discovery uses the https endpoint when one exists and falls back to http. Under Aspire nothing changes; on AWS the services sit behind plain http inside the private network.
- **Gateway CORS** — allowed origins come from `Cors:AllowedOrigins`; the two localhost Vite ports are the fallback when it is not set.
- **`ServiceUrls` aliases (ServiceDefaults `AddServiceUrlAliases`)** — Elastic Beanstalk on Linux silently drops environment properties whose names contain a hyphen, so `services__identity-api__http__0` never reaches the app. Each `ServiceUrls:<name>` entry (env var `ServiceUrls__identity_api`) is copied to `Services:<name with _ turned into ->:http:0`, the key service discovery reads. With no `ServiceUrls` section nothing changes, so Aspire runs are unaffected. `infra/salonbooking.yml` sets these for the Gateway and Notification.Api.
