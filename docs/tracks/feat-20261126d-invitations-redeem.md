# Track: `feat/20261126d-invitations-redeem` — Canje y Solicitud de Renovación de Invitaciones (`/invitation-redeem/:code`)

- **Estado**: Verificado y listo para merge a `dev`
- **Rama**: `feat/20261126d-invitations-redeem` (desde `dev`)
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Diseño
- Endpoints públicos/autenticados en `Babasketet.API`:
  - `GET /api/v1/platform-invitations/{code}` (`AllowAnonymous`): devuelve si el código es válido, activo, expirado, cancelado o canjeado.
  - `POST /api/v1/platform-invitations/redeem` (requiere usuario registrado autenticado vía WillIAM/Zitadel): vincula `RedeemedByUserId` + `RedeemedAt` de forma atómica e idempotente para el mismo usuario.
  - `POST /api/v1/platform-invitations/request-renewal` (`AllowAnonymous`): para códigos válidos pero expirados, crea una `AdminNotification` vía `AdminNotificationService.CreateAndNotifyAdminsAsync` (panel de notificaciones + email SMTP a todos los administradores).
- Nueva ruta frontal `/invitation-redeem` y `/invitation-redeem/:code` (`InvitationRedeemPage`) + ruta `/onboarding` ("Crear Casa o unirte a una"):
  - Solicita / pre-carga el código de invitación.
  - Si es válido y activo: solicita registro/acceso vía `WillIAM` guardando el código pendiente; al volver del callback OIDC canjea automáticamente la invitación y redirige a `/onboarding`.
  - Si es válido pero caducado: indica la expiración y ofrece botón para solicitar renovación -> notifica por panel + correo a admins y vuelve al formulario de introducción de código con confirmación.

## Incrementos
- [x] Slice 1: DTOs, lógica en `PlatformInvitationService` y endpoints `/api/v1/platform-invitations/*`
- [x] Slice 2: `LocalStorageService`, `ApiClient`, `InvitationRedeemPage`, rutas `/invitation-redeem(/:code)` y `/onboarding` + auto-canje post-OIDC
- [x] Slice 3: Documentación y tests unitarios/integración/widget/Playwright E2E (`E2E-34`)

## Verificación
- [x] `dotnet test` (18/18 UnitTests + 15/15 IntegrationTests con Testcontainers PostgreSQL, incluyendo `IT_API_19`)
- [x] `flutter test` (7/7 en verde durante build Docker de `web`)
- [x] `Playwright` E2E (16/16 en verde, incluyendo `E2E-34`)
