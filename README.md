# SalonBooking

A salon booking system built as a .NET 10 / .NET Aspire distributed application. Early-stage microservices skeleton — most services are still scaffold/template code, with `Identity.Api` the furthest along (JWT auth, EF Core + PostgreSQL).

## Architecture

```
apphost.cs                          # Aspire AppHost (file-based C# app)
src/
  Gateway/                          # API gateway/BFF
  SalonBooking.ServiceDefaults/     # Shared OTel, service discovery, resilience, health checks
  Services/
    Identity.Api/                   # Auth: register/login, JWT, EF Core + PostgreSQL
    Booking.Api/                    # Booking service (template only)
    Salon.Api/                      # Salon service (template only)
    Notification.Api/               # Notification service (template only)
```

All services target `net10.0`, reference `SalonBooking.ServiceDefaults` for OpenTelemetry/service discovery/resilience/health checks, and are registered in the AppHost. The Gateway references the four backend services for service discovery.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling) (recommended — see below)
- Docker (for the PostgreSQL container used by `identitydb`)

## Running

Prefer the **Aspire CLI** over `dotnet run`/`dotnet build` — it manages resource lifecycles and avoids file-lock issues between the AppHost and running services.

```bash
aspire run          # foreground
aspire start         # background
aspire stop          # release ports/file locks when done
```

If the Aspire CLI isn't installed, `dotnet run apphost.cs` is a fallback, but avoid running it concurrently with other builds on the same projects — you'll hit `MSB3491`/`CS2012` file-lock errors, which mean a process is still holding `bin`/`obj`, not that the build is actually broken.

## Build

```bash
dotnet build SalonBooking.slnx                                # everything
dotnet build src/Services/Identity.Api/Identity.Api.csproj    # one project
dotnet build apphost.cs                                       # AppHost only
```

No test projects exist yet.

## Known issues

- `Microsoft.OpenApi 2.0.0` (pulled in transitively by `Microsoft.AspNetCore.OpenApi`) has a known high-severity advisory ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)), flagged as `NU1903` during build.
- Booking.Api, Salon.Api, and Notification.Api still expose the default `/weatherforecast` template endpoint — real domain logic not yet implemented.
