# Track: [F1–F7] Backend `Babasketet.API` (.NET 10)

- **Estado**: Verificado
- **Spec de referencia**: `docs/backend/babasketet-api.md`, `docs/specs/domain-catalogs.md`, `docs/features/features-roadmap.md`
- **Servicios afectados**: `Babasketet.API`, `Babasketet.API.UnitTests`, `Babasketet.API.IntegrationTests`

## Decisiones tomadas durante la ejecución
- Se habilita `DualAuth` (PolicyScheme): `ZitadelBearer` (OIDC/JWKS) para usuarios registrados + `DeviceBearer` (JWT HMAC-SHA256 emitido por la API) para usuarios anónimos.
- Los seeds de las 24 tiendas ES/PT y 18 tipos de producto usan UUIDs deterministas (`Guid.Parse(...)`) para garantizar idempotencia entre migraciones, tests y producción.
- Testcontainers en Windows Docker Desktop dentro de contenedor .NET SDK usa `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`.

## Incrementos (Vertical Slices)
- [x] Slice 1: Proyecto `Babasketet.API.csproj` (.NET 10), entidades de Dominio, catálogos semilla y excepciones
- [x] Slice 2: `BabasketetDbContext` (PostgreSQL + SCD-2 `product_price_history` con índice único parcial) + `DatabaseInitializer`
- [x] Slice 3: Autenticación dual (`DeviceToken` + `Zitadel OIDC`), servicios de Casas, Tiendas, Tipos, Lista, Cesta (SCD-2), Compras y SignalR Hub
- [x] Slice 4: Endpoints REST `/api/v1`, `ProblemDetails` RFC 9457, `Dockerfile` multi-stage
- [x] Slice 5: `Babasketet.API.UnitTests` y `Babasketet.API.IntegrationTests` (Testcontainers PostgreSQL real) ejecutados en Docker

## Verificación
- [x] `dotnet build` en contenedor .NET 10 sin errores (`TreatWarningsAsErrors`)
- [x] `Babasketet.API.UnitTests`: 17/17 en verde
- [x] `Babasketet.API.IntegrationTests`: 7/7 en verde contra PostgreSQL 17 real (Testcontainers)
