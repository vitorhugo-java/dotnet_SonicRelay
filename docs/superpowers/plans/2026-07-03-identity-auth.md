# ASP.NET Core Identity Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide production PostgreSQL-backed ASP.NET Core Identity registration, login, refresh, logout, and current-user behavior for bearer-token clients.

**Architecture:** Use the framework's Identity API endpoints and opaque bearer token handler. Persist a GUID-keyed `ApplicationUser` and all Identity entities through the existing EF Core context; add only small custom endpoints for `/auth/me` and `/auth/logout`.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core 10, Npgsql, xUnit, `WebApplicationFactory`, EF Core InMemory.

---

### Task 1: Add failing authentication integration tests

**Files:**
- Create: `tests/SonicRelay.Api.IntegrationTests/SonicRelay.Api.IntegrationTests.csproj`
- Create: `tests/SonicRelay.Api.IntegrationTests/SonicRelayApiFactory.cs`
- Create: `tests/SonicRelay.Api.IntegrationTests/AuthEndpointsTests.cs`
- Modify: `SonicRelay.sln`

- [ ] Create an xUnit test host that removes the Npgsql `AppDbContext` registration, installs a uniquely named in-memory database, and exposes an `HttpClient`.
- [ ] Add HTTP tests for register, login, refresh, `/auth/me`, logout authorization, and another protected API route.
- [ ] Run `dotnet test tests/SonicRelay.Api.IntegrationTests/SonicRelay.Api.IntegrationTests.csproj` and verify the tests fail because the stubs do not issue tokens.

### Task 2: Integrate Identity and make tests pass

**Files:**
- Modify: `src/SonicRelay.Domain/Users/ApplicationUser.cs`
- Modify: `src/SonicRelay.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `src/SonicRelay.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- Modify: `services/SonicRelay.Api/Program.cs`
- Modify: `services/SonicRelay.Api/Endpoints/AuthEndpoints.cs`
- Modify: project package references as required

- [ ] Derive `ApplicationUser` from `IdentityUser<Guid>` and preserve profile fields.
- [ ] Derive `AppDbContext` from the GUID-keyed Identity context, call `base.OnModelCreating`, and map Identity tables.
- [ ] Configure `AddIdentityApiEndpoints<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>()`, password settings, and bearer token lifetimes from configuration.
- [ ] Mount `MapIdentityApi<ApplicationUser>()` under `/auth`; implement protected `/me` and bearer-friendly `/logout`.
- [ ] Run the focused tests until all authentication scenarios pass, then run the full solution tests.

### Task 3: Add the database migration and documentation

**Files:**
- Create: `src/SonicRelay.Infrastructure/Persistence/Migrations/*`
- Modify: `README.md`

- [ ] Generate the initial PostgreSQL migration from `AppDbContext` and inspect it for all Identity and domain tables.
- [ ] Update README status, endpoint behavior, request/response examples, opaque bearer token guidance, and logout semantics.
- [ ] Run `dotnet build SonicRelay.sln` and `dotnet test SonicRelay.sln` with no failures.

### Task 4: Final review and commit

**Files:** all files above

- [ ] Review the diff for secrets, unrelated changes, and accidental JWT/custom crypto code.
- [ ] Verify the final build and tests from a clean invocation.
- [ ] Stage the scoped changes and create one commit: `feat: implement Identity bearer authentication`.
