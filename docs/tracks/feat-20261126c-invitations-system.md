# Track: `feat/20261126c-invitations-system` — Sistema de Invitaciones de Plataforma en `/admin-bo`

- **Estado**: Verificado y listo para merge a `dev`
- **Rama**: `feat/20261126c-invitations-system` (desde `dev`)
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Diseño
- Entidad `PlatformInvitation` (`platform_invitations`) en `babasketet_db` mediante migración EF Core reversible `20261126000003_AddPlatformInvitations` con relación opcional a `AppUser` (`redeemed_by_user_id`), `created_at`, `expires_at` (`null` = indefinida, por defecto `created_at + 7 días`), `cancelled_at` y `redeemed_at`.
- `PlatformInvitationService` expone gestión admin:
  - Listado con filtro de fechas (`all`, `year`, `6months`, `3months`, `month`, `week`, `24h`) y filtro de estado (por defecto `Active`; soporta `All`, `Active`, `Redeemed`, `Cancelled`, `Expired`).
  - Generación de código único (`BBK-XXXXXXXX`) y fecha de expiración configurable (por defecto +7 días o indefinida).
  - Cancelación, renovación (`+1/7/15/30` días sumados a `ExpiresAt` o indefinida, **solo si no ha caducado ni sido cancelada/canjeada**), reactivación (`+1/7/15/30` días desde el momento de la reactivación sobre invitaciones caducadas o canceladas) y envío real por SMTP a múltiples direcciones (una por línea) vía `EmailService`.
- Sección `"Invitaciones"` en `/admin-bo` con generación de invitación, copia de URL (`/invitation-redeem/{code}`), compartir por WhatsApp, TextArea multi-línea de correos + envío SMTP, filtros de fecha y estado, acciones de cancelar/renovar/reactivación y enlace al usuario canjeador en la pestaña Usuarios.

## Incrementos
- [x] Slice 1: Entidad `PlatformInvitation` + migración EF Core reversible `20261126000003_AddPlatformInvitations`
- [x] Slice 2: `PlatformInvitationService`, DTOs y endpoints `/api/v1/admin/invitations*`
- [x] Slice 3: Sección `"Invitaciones"` en `/admin-bo` (`AdminInvitationsSection`)
- [x] Slice 4: Documentación y tests unitarios, integración (Testcontainers + migración reversible), widget test y Playwright E2E (`E2E-33`)

## Verificación
- [x] `dotnet test` (18/18 UnitTests + 14/14 IntegrationTests con Testcontainers PostgreSQL, incluyendo `IT_API_13` reversibilidad `Up`/`Down` y `IT_API_18`)
- [x] `flutter test` (6/6 en verde durante build Docker de `web`)
- [x] `Playwright` E2E (15/15 en verde, incluyendo `E2E-33`)
