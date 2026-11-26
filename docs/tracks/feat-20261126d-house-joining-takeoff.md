# Track: `feat/20261126d-house-joining-takeoff` — Pantalla de Decisión al Unirse a una Casa (`/join/:token`)

- **Estado**: Verificado y listo para merge a `dev`
- **Rama**: `feat/20261126d-house-joining-takeoff` (desde `dev`)
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Diseño
- Endpoint público `GET /api/v1/invitations/{token}` (`HouseService.InspectInvitationAsync`) que valida la vigencia del token sin unir automáticamente al usuario y devuelve el nombre de la casa (`InspectHouseInvitationResponse`) o `410 Gone` si caducó/es inválido.
- Actualización del handler frontal `/join/:token` (`JoinHouseTakeoffPage`):
  - Si el enlace es inválido o caducado → error claro (`410`) con vuelta a `/`.
  - Si el usuario ya está autenticado con WillIAM → se une automáticamente y entra en la casa.
  - Si no está autenticado → presenta explícitamente las 3 opciones:
    1. **Registrarme** (`join_takeoff_register_btn`) → inicia flujo WillIAM guardando token de casa para auto-unión al completar callback OIDC.
    2. **Introducir mis credenciales** (`join_takeoff_login_btn`) → inicia login en WillIAM guardando token de casa para auto-unión tras autenticarse.
    3. **Continuar como invitado (se puede registrar después)** (`join_takeoff_guest_btn`) → se une inmediatamente con `deviceToken` anónimo manteniendo el acceso en cuenta para registro posterior.

## Incrementos
- [x] Slice 1: `InspectHouseInvitationResponse`, `HouseService.InspectInvitationAsync` y `GET /api/v1/invitations/{token}` + test integración `IT_API_20`
- [x] Slice 2: `LocalStorageService`, `ApiClient`, `JoinHouseTakeoffPage` + auto-unión post-callback en `main.dart` + widget test
- [x] Slice 3: Actualizar `01-houses-and-sharing.spec.ts` (`E2E-02`), crear `14-house-joining-takeoff.spec.ts` (`E2E-35`) y sincronizar docs

## Verificación
- [x] `dotnet test` (18/18 UnitTests + 16/16 IntegrationTests con Testcontainers PostgreSQL, incluyendo `IT_API_20`)
- [x] `flutter test` en verde durante build Docker de `web`
- [x] `Playwright` E2E (17/17 en verde, incluyendo `E2E-02`, `E2E-25` y `E2E-35`)
