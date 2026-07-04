# ASP.NET Core Identity Authentication Design

## Scope

Replace SonicRelay's authentication stubs with ASP.NET Core Identity API endpoints, EF Core persistence in PostgreSQL, Identity bearer access/refresh tokens, a protected current-user endpoint, and a protected logout endpoint suitable for native clients.

## Architecture

`ApplicationUser` derives from `IdentityUser<Guid>` and retains SonicRelay profile metadata. `AppDbContext` derives from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` so users, credentials, roles, claims, logins, and tokens share the existing PostgreSQL database. Identity is configured through `AddIdentityApiEndpoints<ApplicationUser>()` and the EF store.

The official Identity endpoint set is mounted under `/auth`, including `POST /auth/register`, `POST /auth/login`, and `POST /auth/refresh`. Login omits `useCookies` (or passes `false`) to receive Identity's opaque bearer access and refresh tokens. These are intentionally not JWTs and no custom cryptography is introduced.

`GET /auth/me` resolves the authenticated Identity user and returns stable profile fields. `POST /auth/logout` requires authentication and returns `204`; bearer clients must then delete both tokens locally. Identity's built-in bearer tokens are self-contained, so this endpoint does not maintain a custom revocation store.

## Authorization

Existing device, session, and signaling routes remain protected with `RequireAuthorization`. The Identity bearer scheme becomes the application's default authentication scheme, replacing the unconfigured JWT bearer handler.

## Persistence

The Identity schema is represented by an EF Core migration. Existing domain tables remain in the same context. Identity table names are explicit and PostgreSQL-friendly, while the application user table remains `application_users`.

## Testing

HTTP integration tests host the real API and replace PostgreSQL with EF Core's in-memory provider. They verify registration, duplicate/invalid registration behavior through Identity, login token issuance, refresh token rotation/issuance, authenticated `/auth/me`, unauthenticated rejection, logout protection, and protection of existing private endpoints.

## Documentation

README authentication status, endpoint contracts, examples, and bearer logout semantics are updated to match the implemented behavior.
