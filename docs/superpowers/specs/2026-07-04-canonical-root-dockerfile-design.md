# Canonical Root Dockerfile Design

## Objective

Make `Dockerfile` at the repository root the single image definition for the SonicRelay API. Every build, test, image publication, Compose environment, and deployment path must consistently target `services/SonicRelay.Api/SonicRelay.Api.csproj`.

## Docker strategy

- Keep the root `Dockerfile` as the only Dockerfile.
- Set its default `APP_PROJECT` to `services/SonicRelay.Api/SonicRelay.Api.csproj`.
- Preserve the multi-stage SDK/runtime build and non-root runtime user.
- Remove `services/SonicRelay.Api/Dockerfile` to prevent the two definitions from drifting.
- Keep the repository root as the Docker build context because the API depends on projects under `src/`.

## Compose behavior

`infra/compose.yml` will build the API from repository context `../` with `dockerfile: Dockerfile`. The development and production overlays retain their existing responsibilities: development exposes and mounts local resources, while production replaces the build with the published image. The API-only deployment Compose file continues consuming the image produced by CI and requires no build definition.

## CI/CD behavior

The workflow will define `APP_PROJECT` as `services/SonicRelay.Api/SonicRelay.Api.csproj` and use that exact path for restore and build. It will fail clearly if the configured project is absent instead of discovering an arbitrary non-test project. The existing independently retryable `build`, `test`, `publish_image`, and `deploy` jobs remain. Image publication will build the canonical root Dockerfile and pass the same project path as a build argument.

## Documentation

README and VPS deployment documentation will identify the root Dockerfile as canonical, describe the exact API project used by CI, and remove references to legacy-path fallback discovery. Architecture and security documentation do not require changes unless validation identifies a contradictory statement.

## Validation

Run the smallest relevant checks available in the environment:

1. `dotnet restore services/SonicRelay.Api/SonicRelay.Api.csproj`
2. `dotnet build services/SonicRelay.Api/SonicRelay.Api.csproj --configuration Release --no-restore`
3. `dotnet test tests/SonicRelay.Api.IntegrationTests/SonicRelay.Api.IntegrationTests.csproj --configuration Release`
4. `docker build .`
5. `docker compose -f infra/compose.yml -f infra/compose.dev.yml --profile dev config`
6. `docker compose -f infra/compose.yml -f infra/compose.prod.yml --profile prod config`
7. `docker compose -f deploy/docker-compose.prod.yml config`

Configuration validation replaces application-level TDD for this change because no application behavior or production C# code is modified. Any unavailable SDK or Docker command will be reported exactly.

## Scope boundaries

Do not change dependencies, application code, runtime topology, deployment secrets, or unrelated documentation. Leave the untracked `.vs/` directory untouched.
