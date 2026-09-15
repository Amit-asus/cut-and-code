# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SalonBooking is a .NET 10 / Aspire 13.5 distributed application — a microservices skeleton for a salon booking system, now with a React/Vite frontend (`src/Web/`) consuming it. `Identity.Api` (auth: register/login/JWT/roles), `Salon.Api` (Salon + Service CRUD, image upload via Azure Blob Storage emulator), `Booking.Api` (bookings, double-booking prevention, a Redis-cached availability endpoint), and `Gateway` (YARP reverse proxy in front of all three, plus CORS for the frontend) are all built out and working end-to-end against live Postgres databases; `Notification.Api` is still on template/scaffold code (default `/weatherforecast` endpoint). The AppHost registers all five backend projects. This is a git repository with a remote (`origin/main`); see `README.md` for the user-facing overview, which mirrors this file's architecture section (note: `README.md` may lag behind this file — this file is the source of truth for current state).

**Known live bug (frontend/backend mismatch):** `src/Web/src/pages/AdminCreateSalonPage.jsx` and `SalonDetailPage.jsx` still reference `service.durationMinutes` (send it on create, display it as "`{n}` min"), but Salon.Api's `Service` model had `DurationMinutes` **removed** via migration `20260909092230_DropServiceDuration`. Net effect: every service now displays "undefined min" in the UI, and the field is silently dropped on create. Not yet fixed — flagged during a full-codebase survey on 2026-09-09, left for the user to handle. If duration is reintroduced, decide whether it lives per-`Service` (natural fit) or stays implicit — Booking.Api's `/bookings/availability` currently hardcodes 1-hour slots independent of any per-service duration, so both would need to agree if this changes.

**Mentor-directed "Phase 0.5" is in progress**: before full-fledged feature development, prove core infra works in isolation — cron job triggering, Service Bus messaging across services, CRUD on DB, CRUD on Redis. CRUD on DB and CRUD on Redis are both done (see Progress checklist below); Service Bus and cron remain, and Service Bus now has a locked-in plan (`remainingplan.md`) folded into the Notification.Api build-out. Once Phase 0.5 is complete, the plan shifts to real domain/business logic across all services.

**The user's stated goal is to learn .NET/Aspire/EF Core/JWT auth while building this, not just to get code written.** When making changes here (as opposed to just answering questions), prefer explaining concepts and letting the user type edits themselves over silently doing everything — match whatever level of hand-holding the current conversation establishes. The user knows Node.js/Express well; analogies to `bcrypt`, `jsonwebtoken`, Express routes/middleware, and Mongoose land well.

## Running the app — use the Aspire CLI, not `dotnet run`/`dotnet build`

This repo has project-local Aspire agent skills at `.agents/skills/` (aspire, aspire-init, aspire-orchestration, aspire-deployment, aspire-monitoring) that take precedence over generic `dotnet` workflows and encode mandatory safety rules:

- **Always** `aspire start` (background) or `aspire run` (foreground, human) to launch the app — **never** `dotnet run` on the AppHost (`apphost.cs`).
- **Always** `aspire wait <resource>` before interacting with a resource — never manual HTTP polling.
- **Always** `aspire stop` when done, to release file locks/ports.
- If a build fails with `MSB3491`/`MSB3027`/`CS2012` ("could not write to output file" / "cannot open for writing" / "process cannot access the file"), a running Aspire resource is holding locks on that project's `bin/`/`obj/` — this is **not** a real build failure. Fix: `aspire resource <name> restart` (or stop the whole AppHost and restart it), never `dotnet build` against a project Aspire currently has running.
- `aspire ps` lists resources with status; the dashboard (URL printed on `aspire run`/`aspire start`, or via `aspire ps`) gives a fuller view including per-resource logs and endpoint links. Use the `https://localhost:<port>` endpoint shown in the terminal/dashboard for actual HTTP requests (e.g. from Postman/curl) — the `*.dev.localhost` friendly hostnames shown in the dashboard do not reliably resolve outside Aspire's own tooling.
- Append `--non-interactive` to any Aspire CLI command run by an agent.

The Aspire CLI (`aspire`) is installed as a .NET global tool (`aspire.cli`) but has been unreliable to invoke from the Bash tool specifically (PATH not resolving there, works fine in the user's own PowerShell terminal). When running Aspire commands as an agent, ask the user to run `aspire`-prefixed commands themselves in their terminal and report back status/output, rather than assuming the Bash tool can invoke `aspire` directly. `dotnet build`/`dotnet run <file>.cs` and Docker commands (`docker ps`, `docker exec`) work fine from the Bash tool.

## Build

```bash
dotnet build SalonBooking.slnx                                 # build everything
dotnet build src/Services/Identity.Api/Identity.Api.csproj     # build one project
dotnet build apphost.cs                                        # build just the AppHost (file-based app)
```

`apphost.cs` is a **file-based C# app** (`#:sdk`/`#:package` directives, no `.csproj`) — run/build it by passing the `.cs` path directly (`dotnet run apphost.cs` / `dotnet build apphost.cs`), never via `dotnet run --project apphost.cs` (`--project` requires an actual project file and will fail with "Couldn't find a project to run").

No test projects exist yet in the solution.

## Solution structure

```
apphost.cs                                    # Aspire AppHost — postgres (identitydb/salondb/bookingdb), redis (cache), Azurite blob storage (salon-images); all 5 backend projects
src/Gateway/                                   # API gateway/BFF — YARP reverse proxy + CORS for the frontend, built out, see below
src/SalonBooking.ServiceDefaults/              # Shared library: OTel, service discovery, resilience, health checks — referenced by all 5 services
src/Services/Identity.Api/                     # Auth service — built out, see below
src/Services/Salon.Api/                        # Salon + Service CRUD + image upload — built out, see below
src/Services/Booking.Api/                      # Bookings + availability — built out, see below
src/Services/Notification.Api/                 # template only
src/Web/                                       # React + Vite frontend — built out, see below
```

**AppHost note:** `postgres` and `redis` (`cache`) resources both use `.WithLifetime(ContainerLifetime.Persistent)` — they survive `aspire run` restarts instead of resetting to a fresh empty container each time (the default). This requires `using Aspire.Hosting.ApplicationModel;` in `apphost.cs`. To force a truly fresh database (e.g. to re-test seeding from scratch), stop/remove the container manually via Docker (`docker stop <name>`, `docker rm <name>`) — restarting Aspire alone won't reset it anymore.

**AppHost note — `cache` (Redis) now belongs to `booking-api`, not `identity-api`:** the `cache` resource itself is still registered the same way, but its `.WithReference(cache)`/`.WaitFor(cache)` moved off `identity-api` and onto `booking-api` (Identity.Api's `Aspire.StackExchange.Redis` package ref and `AddRedisClient("cache")` call were removed entirely). This reflects Redis finding its real use as Booking.Api's availability cache — see the Booking.Api section below.

**AppHost — Azure Blob Storage (Azurite emulator), added for Salon.Api image uploads:** `builder.AddAzureStorage("storage").RunAsEmulator(...)` with `.WithDataVolume("storage-data")` and `.WithBlobPort(10000)` (pinned so the browser-facing blob URLs returned to the frontend stay stable across restarts, unlike every other service's dynamic port), then `storage.AddBlobContainer("salon-images")` — referenced by `salon-api` via `.WithReference(salonImages)`. Requires `#:package Aspire.Hosting.Azure.Storage@13.5.3` at the top of `apphost.cs`, and `Aspire.Azure.Storage.Blobs` in Salon.Api's `.csproj` (client-side, registered via `builder.AddAzureBlobContainerClient("salon-images")` in `Program.cs`). **Note:** unlike the Postgres databases and `cache`, `salonApi`'s reference to `salonImages` does **not** currently have a matching `.WaitFor(salonImages)` — an asymmetry versus the rest of the AppHost; hasn't caused an observed failure yet but worth adding if a startup race ever appears.

## Identity.Api — current state (most-built service)

Folder convention established here (carry forward for other services as they're built out):
```
Identity.Api/
  Models/       → EF Core entities (User.cs, with UserRole enum: Customer/Staff/Admin)
  Dtos/         → request/response shapes as `record` types (RegisterRequest.cs, LoginRequest.cs)
  Services/     → business logic classes, registered as DI singletons (PasswordHasherService.cs, TokenService.cs)
  Endpoints/    → route groups as extension methods on WebApplication (AuthEndpoints.cs → MapAuthEndpoints())
  Migrations/   → EF Core migrations (InitialCreate: Users table, unique index on Email)
  IdentityDbContext.cs
  Program.cs
```

**Built and verified working end-to-end (tested via curl against a live `aspire run` instance + direct psql queries into the Postgres container):**
- `POST /register` — creates a `User` (always `Role = Customer`; no path yet to create Staff/Admin accounts), hashes password via `PasswordHasherService` (wraps `PasswordHasher<User>` from `Microsoft.Extensions.Identity.Core` — deliberately *not* the full `Microsoft.AspNetCore.Identity` framework, since this project uses a hand-rolled `User` entity + JWT rather than ASP.NET Core's cookie-based membership system), rejects duplicate emails with 409.
- `POST /login` — verifies credentials, returns a JWT via `TokenService` (HMAC-SHA256, 1hr expiry, claims: `sub`=user id, `email`, `role`). Deliberately returns the same generic 401 for "no such user" and "wrong password" to avoid user-enumeration.
- `GET /me` — protected via `.RequireAuthorization()`, reads `sub`/`email`/`role` claims off `ClaimsPrincipal` and returns them. JWT Bearer auth wired in `Extensions/JwtAuthExtensions.cs` (`AddJwtAuth()` called from `Program.cs`). **Important gotcha already hit and fixed:** `options.MapInboundClaims = false` is required in the `AddJwtBearer` options — without it, ASP.NET Core silently remaps inbound claim names (e.g. `sub` → a long legacy URI), so `FindFirstValue(JwtRegisteredClaimNames.Sub)` on the receiving end returns `null` even though the token and auth are both valid. If claims come back null again anywhere, check this setting first.
- Database: Postgres via Aspire (`postgres.AddDatabase("identitydb")` in apphost.cs + `AddNpgsqlDbContext<IdentityDbContext>("identitydb")` in Program.cs — the string `"identitydb"` must match between the two). Migrations are applied automatically on startup (`db.Database.Migrate()` in `Program.cs`), not via a separate manual `dotnet ef database update` step — this is because `dotnet ef` commands run outside Aspire have no access to the Aspire-injected connection string (fails with "ConnectionString property has not been initialized"); running migrations in-process is the workaround in use here.
- JWT signing key stored via `dotnet user-secrets` (key `Jwt:SigningKey`), not in any committed file — set up once per dev machine with `dotnet user-secrets init` + `dotnet user-secrets set "Jwt:SigningKey" "..."` from the `Identity.Api` project directory.
- `POST /admin/users` — Admin-only (`.RequireAuthorization(policy => policy.RequireRole("Admin"))`), creates a user with any role (Customer/Staff/Admin) via `CreateUserRequest` DTO. Tested: Admin → 201, Customer → 403, no token → 401.
- First-Admin bootstrap: on every startup, `Program.cs` checks if any `User` with `Role == Admin` exists; if not, seeds one from `Seed:AdminEmail`/`Seed:AdminPassword` config. These are set as **plain-text env vars in `apphost.cs`** via `.WithEnvironment("Seed__AdminEmail", ...)` / `.WithEnvironment("Seed__AdminPassword", ...)` on the `identity-api` resource — a deliberate choice for this learning project (simpler to see/reason about than user-secrets); note this means the admin password is committed in `apphost.cs`, acceptable here but would need to move to real secrets management beyond a learning project. Double-underscore (`Seed__AdminEmail`) is .NET's env-var convention for the nested config key `Seed:AdminEmail` — same value, different separator because env vars can't contain `:`. Idempotent (checks `Any(u => u.Role == Admin)` first) — safe to run on every startup, only ever seeds once per database.

**Not yet built:**
- Salon-to-manager relationship (which staff/admin manages which salon) — deliberately deferred; this is intended to live in `Salon.Api`'s own data as a separate mapping table keyed by the user's Guid, not as a field on `Identity.Api`'s `User`, since each service should own its own slice of user-related data
- Other services validating tokens issued by Identity.Api (would need matching JWT bearer config using the same signing key/issuer/audience)
- A Redis CRUD proof (`Endpoints/CacheEndpoints.cs`, `IConnectionMultiplexer` via `AddRedisClient("cache")`) was drafted here early on — never finished, and since fully unwound (package ref and `AddRedisClient` call removed; the `cache` resource reference in `apphost.cs` moved to Booking.Api instead, see AppHost notes above). Redis ended up used for real in Booking.Api's availability cache rather than as a standalone Identity.Api proof — see the Booking.Api section.

## Salon.Api — current state

Same folder convention as Identity.Api, plus `Extensions/` for setup helpers:
```
Salon.Api/
  Models/       → Salon.cs (has-many Services via List<Service> nav property), Service.cs (belongs-to Salon via SalonId FK + Salon? nav property)
  Dtos/         → CreateSalonRequest.cs, UpdateSalonRequest.cs (Update deliberately excludes OwnerId — no silent ownership transfer via edit)
  Endpoints/    → SalonEndpoints.cs → MapSalonEndpoints() — full CRUD on /salons
  Extensions/   → DatabaseExtensions.cs → MigrateDatabase() (same "migrate on startup" pattern as Identity.Api, extracted to keep Program.cs slim)
  Migrations/   → EF Core migrations (InitialCreate: Salons + Services tables, FK with Cascade delete)
  SalonDbContext.cs
  Program.cs
```

**Important — all Salon.Api's own types are wrapped in `namespace SalonBooking.SalonApi;`** (Models, DbContext, Endpoints, Extensions, Dtos), with `using SalonBooking.SalonApi;` in `Program.cs`. This is **required**, not stylistic: EF Core's `dotnet ef migrations add` auto-generates migration files under `namespace Salon.Api.Migrations` (derived from the project name `Salon.Api`), which in C# creates a real namespace called `Salon`. Since our entity was also named `Salon` and originally sat in the global namespace, this collided (`CS0101: already contains a definition for 'Salon'`). Wrapping our code in an explicit namespace resolves it. **Watch for this same class-of-bug in any future service whose entity name matches (part of) its project name** (e.g. don't name a class `Identity` inside `Identity.Api` without a namespace either).

**Built and verified working end-to-end (tested via curl against a live `aspire run` instance + direct psql queries):**
- Full CRUD on `/salons`: `POST` (201), `GET` all (200) and by id (200/404), `PUT` (200/404), `DELETE` (204/404) — all confirmed against real Postgres (`salondb`). `PUT` only updates `Name`/`Address` (matches `UpdateSalonRequest`, deliberately excludes `OwnerId`).
- EF Core inferred the `Salon` ↔ `Service` one-to-many relationship automatically from the `List<Service> Services` / `Guid SalonId` / `Salon? Salon` shape alone — no `OnModelCreating` override needed (unlike `IdentityDbContext`'s unique-email-index, which does need one). Generated a real FK constraint with `onDelete: Cascade` (deleting a Salon deletes its Services) since `Service.SalonId` is non-nullable.
- Database: separate `salondb` database in the same shared Postgres container as `identitydb` (`postgres.AddDatabase("salondb")` in apphost.cs + `AddNpgsqlDbContext<SalonDbContext>("salondb")` in Program.cs).
- **Service sub-resource CRUD**, added since the initial build: `POST /salons/{salonId}/services`, `PUT /salons/{salonId}/services/{serviceId}`, `DELETE /salons/{salonId}/services/{serviceId}` — all Staff/Admin-only, via `CreateServiceRequest(Name, Price)` / `UpdateServiceRequest(Name, Price)`. **`Service` no longer has `DurationMinutes`** — removed via migration `DropServiceDuration` (see the known-bug note at the top of this file for the frontend fallout).
- **Salon image upload**: `Salon` gained an `ImageUrl` (string?) field (migration `AddSalonImageUrl`). `POST /salons/{id}/image` (Staff/Admin-only, `.DisableAntiforgery()`, multipart form file field `file`) uploads to the `salon-images` Azurite blob container via `BlobContainerClient` (registered through `AddAzureBlobContainerClient("salon-images")`), then sets `salon.ImageUrl` to the resulting blob URL and saves.

- JWT auth wired via `Extensions/JwtAuthExtensions.cs` (`AddJwtAuth()`, identical pattern to Identity.Api). `POST`/`PUT`/`DELETE` on `/salons` require `.RequireAuthorization(policy => policy.RequireRole("Staff", "Admin"))`; both `GET` endpoints stay public (no auth needed to browse). Tested end-to-end: no token → 401, Customer token → 403, Admin token → 201/200/204.
- **Shared JWT signing key**: moved out of Identity.Api's user-secrets entirely and into `apphost.cs` as a single `jwtSigningKey` variable, passed to both `identity-api` and `salon-api` via `.WithEnvironment("Jwt__SigningKey", jwtSigningKey)` — one source of truth for the secret both services need to agree on (Identity.Api signs with it, Salon.Api validates with it). This is the pattern to reuse for any future service that needs to validate Identity.Api's tokens.

**Gotcha hit and fixed — `Microsoft.IdentityModel.*` package version mismatch causing a false "invalid token" 401:** Adding `<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.15.0" />` directly to Salon.Api's `.csproj` (alongside `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.0, which transitively wants `8.0.1`) caused NuGet to resolve the `Microsoft.IdentityModel.*` package family to **mixed versions** (`Abstractions`/`Logging` at 8.15.0, but `Tokens`/`JsonWebTokens`/`Protocols` at 8.0.1) — these packages ship together and must stay in lockstep. Symptom: `identity-api` issued perfectly valid tokens (verified working against its own `/me`), but `salon-api` rejected every one with `401` / `WWW-Authenticate: Bearer error="invalid_token"`, even with the correct shared signing key confirmed present in the Aspire dashboard for both services. Root cause was only found by temporarily adding `JwtBearerEvents.OnAuthenticationFailed` logging (see git history / this session for the pattern if this class of bug recurs), which surfaced the real exception: `IDX14102: Unable to decode the header ... as Base64Url encoded string` — a symptom of the internal version mismatch, not a real malformed token. **Fix: don't add an explicit `PackageReference` for any `Microsoft.IdentityModel.*` package in a service that only *validates* JWTs (i.e. has `Microsoft.AspNetCore.Authentication.JwtBearer` but not `System.IdentityModel.Tokens.Jwt`) — let it resolve transitively.** If validating a token against Identity.Api-issued tokens *ever* mysteriously 401s again despite correct config, check `obj/**/project.assets.json` for mixed `Microsoft.IdentityModel.*` versions first, before re-checking signing keys/config.

**Not yet built:**
- Fine-grained "does this Staff member actually own this specific salon" ownership check (beyond just role-based auth)
- `POST /salons`'s `CreateSalonRequest(Name, Address, OwnerId)` still **trusts `OwnerId` from the request body**, unlike Booking.Api's pattern of always deriving the acting user from the JWT `sub` claim — nothing stops a Staff/Admin caller from creating a salon "owned by" an arbitrary other user's Guid. The frontend's `AdminCreateSalonPage.jsx` happens to always pass the logged-in user's own id, so this is self-consistent in practice today, but it's an unenforced trust boundary on the backend.
- Redis / Service Bus / cron — see Progress checklist

## Gateway — current state

A YARP (`Yarp.ReverseProxy`) reverse proxy — the single front door clients hit instead of knowing individual backend service ports (which change every `aspire run` restart). Also the frontend's CORS boundary (see below).

**Packages:** `Yarp.ReverseProxy` (2.3.0) + `Microsoft.Extensions.ServiceDiscovery.Yarp` (10.0.0) — the second package is **required**, not optional: `AddServiceDefaults()`'s `ConfigureHttpClientDefaults(...AddServiceDiscovery())` only wires Aspire service discovery into `HttpClient`s created via `IHttpClientFactory`; YARP resolves cluster destinations through its own separate `IDestinationResolver` pipeline, which needs the explicit `.AddServiceDiscoveryDestinationResolver()` call (chained onto `AddReverseProxy().LoadFromConfig(...)` in `Program.cs`) to understand Aspire resource-name pseudo-hosts at all. Without it, addresses like `https://identity-api` in `appsettings.json` would fail as literal unresolvable hostnames.

**Routing config lives in `appsettings.json`** (`ReverseProxy:Routes` / `ReverseProxy:Clusters`), not code:
- `/identity/{**catch-all}` → `identity-cluster` → `https://identity-api`, with a `PathRemovePrefix: /identity` transform (Identity.Api's own routes don't know about the `/identity` prefix — e.g. `/identity/login` arrives at Identity.Api as just `/login`)
- `/salons/{**catch-all}` → `salon-cluster` → `https://salon-api`, **no** prefix removal (Salon.Api's real routes already start with `/salons`)
- `/bookings/{**catch-all}` → `booking-cluster` → `https://booking-api`, **no** prefix removal (same reasoning as `/salons`) — this wildcard also covers `GET /bookings/availability?serviceId=...&date=...` with no dedicated route needed; the query string passes through untouched.
- The `https://identity-api` / `https://salon-api` / `https://booking-api` addresses are Aspire resource-name pseudo-hosts (matching the names given in `apphost.cs`'s `AddProject(...)` calls), resolved to the real dynamic address at request time by the service-discovery destination resolver above — never hardcode a port here.

**CORS**, added to support `src/Web`'s Vite dev server: `Program.cs` registers an `AddCors` policy `"WebClient"` allowing origin `http://localhost:5174` (any header/method, no credentials), applied via `app.UseCors("WebClient")` before `MapReverseProxy()`. If the Vite dev server's port ever changes (Vite auto-increments if 5174 is taken), this policy needs updating to match, or the frontend's `fetch` calls will be silently blocked by the browser with no server-side error to debug from.

**Built and verified working end-to-end (tested via curl against the Gateway's own port, not the backend services directly):**
- `GET /salons` through the Gateway → forwards to Salon.Api, returns real data (200)
- `POST /identity/login` through the Gateway → forwards to Identity.Api with prefix stripped, returns a valid JWT (200)
- `POST /salons` through the Gateway, with a Bearer token → forwards correctly including the `Authorization` header, role-based auth still enforced (201 as Admin)
- `GET /bookings` and `POST /bookings` through the Gateway, with a Bearer token → forwards correctly to Booking.Api, both returned real data (200/201)
- Gateway's own `/health` works (via `AddServiceDefaults()`/`MapDefaultEndpoints()`, same as every other service)

**Not yet built:**
- A route for Notification.Api (no real endpoints there yet to route to)
- Any Gateway-level concern beyond pure routing (e.g. rate limiting, aggregation across services) — out of scope for now, pure pass-through proxy only

## Booking.Api — current state

Folder convention matches Identity.Api/Salon.Api (`Models/`, `Dtos/`, `Endpoints/`, `Extensions/`), namespace `SalonBooking.BookingApi` (same collision-avoidance reasoning as Salon.Api — a project named `Booking.Api` would generate a `namespace Booking.Api.Migrations`, colliding with a `class Booking` in the global namespace).

**Entity:** `Booking` — `CustomerId`/`SalonId`/`ServiceId` are plain `Guid`s pointing into Identity.Api's and Salon.Api's separate databases (no real FK, same cross-service-reference pattern used elsewhere). `Status` is an enum (`Confirmed`/`Cancelled`); cancel is a soft-delete (`DELETE /bookings/{id}` sets `Status = Cancelled`, never removes the row) so history is preserved and a cancelled booking's slot becomes bookable again.

**Built and verified working end-to-end:**
- `POST /bookings` — creates a booking for the *calling* user (customer ID comes from the JWT's `sub` claim, never trusted from the request body — you can't book on someone else's behalf). Rejects `EndTime <= StartTime` (400). **Double-booking prevention**: rejects (409) if the same `ServiceId` already has a `Confirmed` booking whose time range overlaps the requested one. Overlap test: `existing.StartTime < new.EndTime && existing.EndTime > new.StartTime` — correctly allows back-to-back bookings (one ending exactly when the next starts) since it's `<`/`>`, not `<=`/`>=`. Tested: first booking 201, a genuinely overlapping second booking on the same service 409, a back-to-back booking 201, cancelling then re-booking the same slot 201 (cancelled bookings don't count toward the conflict check, since the query filters on `Status == Confirmed`).
- `GET /bookings` — returns only the calling user's own bookings (filtered by `sub` claim), not everyone's.
- `DELETE /bookings/{id:guid}` — the `:guid` route constraint is deliberate: without it, this route would swallow `/bookings/availability` (matching "availability" as if it were an `{id}`). 404 if not found, 403 (`Results.Forbid()`) if the booking belongs to a different customer, otherwise soft-cancels (204).
- **`GET /bookings/availability`** (query params `serviceId`, `date`) — added since the initial build, backs the frontend's slot picker. Generates fixed **1-hour slots, 9am–9pm, hardcoded to `"India Standard Time"`** (`Enumerable.Range(9, 12)`), converts the requested local date to UTC before querying (an explicit fix — bookings are stored/compared in UTC, and naively comparing local-time slots against UTC-stored bookings was wrong), then flags each slot `Booked = true` if any existing `Confirmed` booking for that `ServiceId` overlaps it. Response: `[{ startTime, endTime, booked }, ...]` (camelCased). Any authenticated user (not role-restricted — doesn't reveal whose booking holds a slot, only that it's taken). **The slot duration (1 hour) is hardcoded here, independent of any per-service duration field** — relevant if `Service.DurationMinutes` (removed, see the known-bug note at the top of this file) is ever reintroduced; this endpoint would need to consume it instead of the fixed 1-hour assumption.
- **Redis-backed availability cache** (closes the Phase 0.5 "CRUD on Redis" item) — `IConnectionMultiplexer` via `AddRedisClient("cache")` (package `Aspire.StackExchange.Redis`, resource wired in `apphost.cs` onto `booking-api`, see AppHost notes above). Cache key `availability:{serviceId}:{date:yyyy-MM-dd}`, value is the exact JSON the endpoint would otherwise return (serialized with `JsonSerializerDefaults.Web` so a cache hit and a cache miss are byte-for-byte identical), TTL 1 day. `GET /bookings/availability` checks the cache first and returns it verbatim as raw JSON (`Results.Text(..., "application/json")`) on a hit, otherwise computes from Postgres as before and writes the result back to the cache before returning. `POST /bookings` and `DELETE /bookings/{id:guid}` both call a shared `InvalidateAvailabilityAsync` helper after a successful write, deleting that service+date's cache key so the next read recomputes. **Every Redis call (`StringGetAsync`/`StringSetAsync`/`KeyDeleteAsync`) is wrapped in `try/catch (RedisException)` and swallowed** — deliberate: Postgres is the source of truth and a booking write has already succeeded by the time invalidation runs, so a Redis outage should degrade the feature to "slower" (always recomputes) rather than "broken" (an unrelated caching concern should never fail a booking). Worst case of the caught exception: a stale slot shows as available for up to the 1-day TTL, but the real double-booking check in `POST /bookings` (against Postgres, not the cache) still prevents an actual conflict.
- All four endpoints require `.RequireAuthorization()` (any authenticated role — Customer/Staff/Admin can all book), reusing the same `JwtAuthExtensions.cs` pattern and shared `Jwt__SigningKey` as Identity.Api/Salon.Api.
- Database: `bookingdb`, same shared Postgres container as `identitydb`/`salondb`.
- **Enum serialization note:** unlike Salon.Api (which registers `JsonStringEnumConverter` via `ConfigureHttpJsonOptions`), Booking.Api does **not** — `BookingStatus` serializes as a raw integer (`0`/`1`) in JSON responses, not the string `"Confirmed"`/`"Cancelled"`. The frontend's `MyBookingsPage.jsx` relies on this today (`{0: "Confirmed", 1: "Cancelled"}` lookup). If Booking.Api is ever changed to match Salon.Api's string-enum convention "for consistency," that frontend mapping will silently break.

**Known gap, deliberately deferred:** the overlap check is an **application-level, read-then-write check — not race-condition-safe.** Two concurrent requests for the same overlapping slot could both pass the conflict check before either INSERT completes, both succeeding (real double-booking under concurrency). The more correct fix is a Postgres `tstzrange` column + a `EXCLUDE USING gist` constraint (DB-enforced, race-safe, uses Postgres's native `&&` overlap operator) instead of two plain `DateTime` columns — raised explicitly during this build, deliberately deferred to keep the first pass simple and EF-Core-idiomatic. Revisit this before Booking.Api is used under any real concurrent load.

**Gotcha hit and fixed — `dotnet ef migrations add` fails design-time build when `AddJwtAuth()` throws:** unlike Salon.Api (where the migration was generated *before* JWT auth was added), Booking.Api had `AddJwtAuth()` wired from the start. `dotnet ef migrations add` builds and runs the app's startup code to inspect the `DbContext`, so it hit `AddJwtAuth()`'s `?? throw new InvalidOperationException("Jwt:SigningKey is not configured.")` — no `Jwt__SigningKey` env var exists outside `aspire run`. **Fix: prefix the `dotnet ef` command with the env var inline for that one command**, e.g. (bash) `Jwt__SigningKey="<value>" dotnet ef migrations add <Name>` (PowerShell: `$env:Jwt__SigningKey="<value>"; dotnet ef migrations add <Name>`) — the dummy value only needs to satisfy the null-check, it's never actually used to sign/validate anything during migration generation. Apply this to any future service where JWT auth is wired before the first migration is generated.

**Not yet built:**
- Any check that `SalonId`/`ServiceId` in a booking request actually correspond to real Salon.Api records (currently trusted, not validated — would need either a service-to-service HTTP call to Salon.Api or accepting eventual consistency)

## Web (frontend) — current state

A React 19 + Vite (v8) app at `src/Web/`, built as a discovery/proof tool for the backend (found real gaps during its build — see the known-bug note at the top of this file and other notes throughout). Plain hand-rolled CSS (`src/index.css`), no CSS framework. Native `Date`/`toLocaleString`/`toISOString` for all date handling, no date library. Routing via `react-router-dom` 7. Linted with `oxlint`, not eslint.

**Structure:**
```
Web/
  src/
    api/client.js         → thin fetch wrapper; every function maps to one Gateway endpoint (see below), auto-attaches Bearer token from localStorage
    context/AuthContext.jsx → user/login/logout via React Context; on mount, hydrates from a stored token via GET /identity/me
    components/
      Layout.jsx           → header/nav/footer shell, role-aware nav links
      ProtectedRoute.jsx    → redirects to /login if unauthenticated, or to /salons if role not permitted (takes a `roles` prop)
    pages/
      LoginPage.jsx, RegisterPage.jsx
      SalonsPage.jsx        → directory grid, shows salon.imageUrl if present
      SalonDetailPage.jsx   → services list, inline edit/delete (Staff/Admin), booking form with date picker + availability slot grid
      MyBookingsPage.jsx    → caller's own bookings, cancel button
      AdminCreateSalonPage.jsx → 3-step wizard: create salon → upload photo → add services (Staff/Admin only)
  .env / .env.example    → VITE_API_URL (the Gateway's current https://localhost:<port> — update this by hand every aspire run restart, same as the Postman environment variable used earlier in this project's history)
```

**Every `api/client.js` function and the Gateway endpoint it calls:**
| Function | Method | Path |
|---|---|---|
| `register`, `login` | POST | `/identity/register`, `/identity/login` |
| `me` | GET | `/identity/me` |
| `listSalons`, `getSalon(id)` | GET | `/salons`, `/salons/{id}` |
| `createSalon`, `updateSalon(id)`, `deleteSalon(id)` | POST/PUT/DELETE | `/salons`, `/salons/{id}` |
| `uploadSalonImage(id, file)` | POST | `/salons/{id}/image` (multipart, field `file`) |
| `createService/updateService/deleteService(salonId, ...)` | POST/PUT/DELETE | `/salons/{salonId}/services[/{serviceId}]` |
| `listBookings`, `createBooking`, `cancelBooking(id)` | GET/POST/DELETE | `/bookings`, `/bookings/{id}` |
| `getAvailability(serviceId, date)` | GET | `/bookings/availability?serviceId=...&date=...` |

**Routes (App.jsx):** `/login`, `/register`, `/salons`, `/salons/:id` (all public), `/bookings` (protected, roles Customer/Staff), `/admin/salons/new` (protected, roles Staff/Admin), `/` → SalonsPage.

**Before it'll work at all: accept the Gateway's self-signed dev cert once per browser** — visit `https://localhost:<gateway-port>/health` directly and click through the certificate warning. Without this, `fetch()` calls from the React app fail silently (no CORS-looking error, just a network failure) since the browser blocks the untrusted cert before CORS is even evaluated.

**Not yet built / known issues:**
- The `durationMinutes` bug (see top of file)
- No enrichment of booking data with salon/service names — `MyBookingsPage.jsx` can only show raw times/status, not "Haircut at Glow Salon," since `Booking` only stores `SalonId`/`ServiceId` as opaque Guids

## Progress & Next Steps

Keep this checklist current — update it (check off items, add new ones) at the end of each work session or meaningful milestone, don't let it go stale.

Done:
- [x] AppHost wired: Postgres (`identitydb`) + all 5 project resources registered; Gateway references the 4 backend services
- [x] All services reference `SalonBooking.ServiceDefaults` (health checks, OTel, service discovery)
- [x] Identity.Api: `User` entity + EF Core migration (Postgres, unique email index)
- [x] Identity.Api: `POST /register` (password hashing, duplicate-email check)
- [x] Identity.Api: `POST /login` (credential check, JWT issuing)
- [x] Identity.Api: JWT validation wired (`AddJwtAuth()`) + protected `GET /me` endpoint, tested end-to-end (register → login → /me with and without token)
- [x] Identity.Api: first-Admin seeding on startup + Admin-only `POST /admin/users` for creating Staff/Admin accounts, tested end-to-end (Admin → 201, Customer → 403, no token → 401)
- [x] Postgres + Redis made persistent (`ContainerLifetime.Persistent`) so local data survives `aspire run` restarts
- [x] Salon.Api: `Salon`/`Service` entities, migration, full CRUD on `/salons`, tested end-to-end — completes the mentor's Phase 0.5 "CRUD on DB" item with real (non-throwaway) code
- [x] Salon.Api: JWT auth wired, `/salons` write endpoints restricted to Staff/Admin, tested end-to-end (no token → 401, Customer → 403, Admin → success) — closes the "no auth on Salon.Api" gap flagged earlier
- [x] Gateway: YARP reverse proxy routing to Identity.Api and Salon.Api via Aspire service discovery, tested end-to-end (public GET, prefix-stripped POST, and an authenticated POST all correctly forwarded)
- [x] Booking.Api: `Booking` entity/migration, `POST`/`GET`/`DELETE /bookings`, JWT auth, and double-booking prevention (time-overlap check per Service), tested end-to-end (create, reject overlap, allow back-to-back, list own bookings, cancel, re-book a cancelled slot) — the core business feature of the whole app is now live, though the overlap check has a known race-condition gap (see Booking.Api section)
- [x] Gateway: `/bookings` route added and tested end-to-end (GET + POST through the Gateway, JWT pass-through intact) — Gateway now fronts all three built services (Identity.Api, Salon.Api, Booking.Api)
- [x] Salon.Api: Service sub-resource CRUD (`POST`/`PUT`/`DELETE /salons/{salonId}/services[/{serviceId}]`) and image upload (`POST /salons/{id}/image` via Azurite blob storage emulator, new AppHost `storage`/`salon-images` resources)
- [x] Booking.Api: `GET /bookings/availability` — computes fixed 1-hour slots (9am–9pm IST) per service/date, flags booked ones against `Confirmed` bookings
- [x] Gateway: CORS wired (`WebClient` policy for `http://localhost:5174`) to support the new frontend
- [x] `src/Web/`: full React 19 + Vite + react-router frontend built — Login/Register, Salons directory + detail (with inline Staff/Admin edit/delete), booking flow with availability slot picker, My Bookings, Admin salon-creation wizard with image upload. Built specifically as a discovery tool and immediately surfaced 1 real bug (`durationMinutes` mismatch, see top of file) and 2 unenforced trust-boundary gaps (Salon.Api `OwnerId`, no salon/service name enrichment on bookings) — validates the "build the frontend to find backend gaps" approach
- [x] Booking.Api: Redis-backed availability cache (`GET /bookings/availability` cached 1 day, invalidated on booking create/cancel) — closes the Phase 0.5 "CRUD on Redis" item for real, see Booking.Api section for the implementation. Identity.Api's earlier unfinished Redis proof (`CacheEndpoints.cs`) was fully unwound as part of this — the `cache` AppHost resource moved from `identity-api` to `booking-api`.

**Mentor's Phase 0.5 checklist** (prove infra works before full-fledged development):
- [x] CRUD on DB — done via Salon.Api's `/salons` (see above)
- [x] CRUD on Redis — done via Booking.Api's availability cache (see Booking.Api section) — reads/writes/deletes a real Redis instance (Aspire-hosted, persistent), not a throwaway proof
- [ ] Service Bus working across services — not started yet, but now has a locked-in plan (see `remainingplan.md` at the repo root, written 2026-09-15): Azure Service Bus via Aspire's local emulator (`.RunAsEmulator()`) with a booking-created queue wired to `booking-api` + `notification-api`; Booking.Api publishes via a new `Services/BookingEventPublisher.cs` after a successful create; Identity.Api gets an unauthenticated internal endpoint `GET /internal/users/{id}` (`{ id, email }`) and Salon.Api gets `GET /internal/salons/{salonId}/services/{serviceId}` (`{ salonName, serviceName }`) for Notification.Api to enrich the event; Notification.Api becomes a real `BackgroundService` Service Bus consumer that calls both internal endpoints and sends a real email via SendGrid (API key via `dotnet user-secrets`, matching the `Jwt:SigningKey` pattern, or an apphost env var like the admin seed password). End-to-end test target: create a booking → message visible in the Aspire dashboard → Notification.Api consumes it → real email lands.
- [ ] Cron job triggering — not started. Plan: simple heartbeat/log job to start (exact mechanism — `BackgroundService` vs. other — not yet decided)

Next up after Phase 0.5 (roughly in order):
- [ ] Notification system per the locked-in plan above (Service Bus + internal enrichment endpoints + SendGrid) — this is the next concrete milestone, see `remainingplan.md`
- [ ] Fix the `durationMinutes` frontend/backend mismatch (see known-bug note at top of file) — left to the user, not yet done
- [ ] Salon-to-manager relationship (see Salon.Api section above)
- [ ] Race-condition-safe double-booking prevention in Booking.Api (Postgres `tstzrange` + `EXCLUDE USING gist`, see Booking.Api section)
- [ ] Validate `SalonId`/`ServiceId` on booking creation actually exist in Salon.Api (currently trusted, not checked) — note the planned `/internal/...` endpoints above would also serve this validation, not just notification enrichment
- [ ] `POST /salons`'s trusted `OwnerId` (see Salon.Api section) — align with Booking.Api's JWT-derived-identity pattern

## Known issue: NuGet advisory

`Microsoft.OpenApi 2.0.0` (pulled in transitively via `Microsoft.AspNetCore.OpenApi` in all four API projects) has a known high-severity vulnerability ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)), flagged by `dotnet build` as `NU1903`.
