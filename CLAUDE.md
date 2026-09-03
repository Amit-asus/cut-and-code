# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

SalonBooking is a .NET 10 / Aspire 13.5 distributed application — a microservices skeleton for a salon booking system. It is early-stage: the AppHost does not yet register any resources, no service references `SalonBooking.ServiceDefaults`, and all five projects are still on template/scaffold code (weather-forecast sample endpoints, no real domain logic). Not yet a git repository.

## Running the app — use the Aspire CLI, not `dotnet run`/`dotnet build`

This repo has project-local Aspire agent skills at `.agents/skills/` (aspire, aspire-init, aspire-orchestration, aspire-deployment, aspire-monitoring) that take precedence over generic `dotnet` workflows and encode mandatory safety rules:

- **Always** `aspire start` (background) or `aspire run` (foreground, human) to launch the app — **never** `dotnet run` on the AppHost (`apphost.cs`).
- **Always** `aspire wait <resource>` before interacting with a resource — never manual HTTP polling.
- **Always** `aspire stop` when done, to release file locks/ports.
- If a build fails with `MSB3491` or `CS2012` ("could not write to output file" / "cannot open for writing"), Aspire is running and holding locks on `bin/`/`obj/` — this is **not** a real build failure. Run `aspire stop` first, then rebuild.
- `aspire describe` / `aspire ps` (add `--include-hidden` for proxies/helpers) to inspect resource state; `aspire logs <resource>` / `aspire otel logs` for diagnostics.
- Append `--non-interactive` to any Aspire CLI command run by an agent.

The Aspire CLI is **not currently installed/on PATH** in this dev environment. Until it is, `dotnet build`/`dotnet run` are the only available fallback for compiling and quick checks — but be aware they bypass the lock-safety the CLI provides, so avoid running them concurrently with a live `dotnet run` process on the same project.

Full routing detail lives in `.agents/skills/aspire/SKILL.md` and its sibling skills; consult it before running unfamiliar `aspire` subcommands or editing the AppHost.

## Build

```bash
dotnet build SalonBooking.slnx        # build everything
dotnet build src/Services/Booking.Api/Booking.Api.csproj   # build one project
dotnet build apphost.cs               # build just the AppHost (file-based app)
```

No test projects exist yet in the solution.

## Solution structure

`SalonBooking.slnx` (new XML solution format) lists six projects:

```
apphost.cs                                    # Aspire AppHost (file-based C# app, #:sdk directive — not a .csproj)
src/Gateway/                                   # API gateway/BFF (currently template code only)
src/SalonBooking.ServiceDefaults/              # Shared library: OTel, service discovery, resilience, health checks
src/Services/Booking.Api/
src/Services/Identity.Api/
src/Services/Notification.Api/
src/Services/Salon.Api/
```

All five runnable projects target `net10.0`, use `Microsoft.NET.Sdk.Web`, and are currently unconnected to each other and to the AppHost — wiring them (AppHost resource registration, `AddServiceDefaults()`/`MapDefaultEndpoints()` calls, Gateway routing to the other services) is outstanding scaffolding work, not yet done.

## AppHost (`apphost.cs`)

This is a **file-based C# app** (C# 13/.NET 10 single-file program using `#:sdk`/`#:package` directives), not a traditional `.csproj`-based AppHost project. Key implication: IDE language servers (OmniSharp / C# Dev Kit) resolve file-based apps through a lighter design-time model that does not always pick up SDK-implicit package references the way `dotnet build` does. If `DistributedApplication` or other Aspire hosting types show as unresolved in the editor despite the CLI build succeeding, add the explicit package directive (already done — see the `#:package Aspire.Hosting.AppHost@<version>` line) and restart the language server (`.NET: Restart Language Server` for C# Dev Kit, or `Developer: Restart OmniSharp`), matching the version to the `#:sdk Aspire.AppHost.Sdk@<version>` line above it.

## ServiceDefaults

`src/SalonBooking.ServiceDefaults/Extensions.cs` is the shared Aspire service-defaults library, meant to be referenced by every service project (none currently do). It provides `AddServiceDefaults()` (OpenTelemetry tracing/metrics/logging, service discovery, standard HTTP resilience via `AddStandardResilienceHandler`) and `MapDefaultEndpoints()` (`/health` and `/alive`, development-only).

## Known issue: NuGet advisory

`Microsoft.OpenApi 2.0.0` (pulled in transitively via `Microsoft.AspNetCore.OpenApi` in all four API projects) has a known high-severity vulnerability ([GHSA-v5pm-xwqc-g5wc](https://github.com/advisories/GHSA-v5pm-xwqc-g5wc)), flagged by `dotnet build` as `NU1903`.
