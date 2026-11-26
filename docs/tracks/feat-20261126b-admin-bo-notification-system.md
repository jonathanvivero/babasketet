# Track: `feat/20261126b-admin-bo-notification-system` — Sistema de Notificaciones en `/admin-bo`

- **Estado**: Verificado y listo para merge a `dev`
- **Rama**: `feat/20261126b-admin-bo-notification-system` (desde `dev`)
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Diseño
- Entidad `AdminNotification` (`admin_notifications`) en `babasketet_db` (migración EF Core reversible `20261126000002_AddAdminNotifications`) con estados `New`, `Read`, `Archived`, `CreatedAt`, `ReadAt` (`null` por defecto, rellenado al leer o archivar) y `ArchivedAt` (`null` por defecto, rellenado al archivar).
- Al crear una notificación vía `AdminNotificationService`, se persiste como `New` y se envía por correo electrónico (mediante `EmailService` configurado en BD) a los emails de todos los administradores activos.
- Endpoints `/api/v1/admin/notifications` (`GET` paginado de 10 en 10 con filtro multi-estado, `POST` para alta, `PATCH /{id}/status` para marcar `New`, `Read`, `Archived`).
- UI en `/admin-bo`: panel de notificaciones ubicado en el **tercio derecho** del dashboard principal (pestaña dedicada en `< 900px`), listado paginado de 10 en 10, filtro por defecto en `Nuevas` (`Todas`, `Nuevas`, `Leídas`, `Archivadas`), acciones rápidas en fila y diálogo popup con vista completa, marcado automático como `Leída` al abrir y navegación `Anterior` / `Siguiente`.

## Incrementos
- [x] Slice 1: Entidad `AdminNotification` + migración EF Core reversible `20261126000002_AddAdminNotifications`
- [x] Slice 2: `AdminNotificationService` (envío de correo a todos los admins + paginación + cambios de estado) y endpoints `/api/v1/admin/notifications*`
- [x] Slice 3: UI en `/admin-bo` (`AdminNotificationsPanel` en tercio derecho + popup con navegación anterior/siguiente)
- [x] Slice 4: Documentación y tests unitarios, integración (Testcontainers + migración reversible), widget test y Playwright E2E (`E2E-32`)

## Verificación
- [x] `dotnet test` (17/17 UnitTests + 13/13 IntegrationTests con Testcontainers PostgreSQL, incluyendo `IT_API_13` reversibilidad `Up`/`Down` y `IT_API_17`)
- [x] `flutter test` (6/6 en verde durante build Docker de `web`)
- [x] `Playwright` E2E (14/14 en verde, incluyendo `E2E-32` panel de notificaciones, filtros, popup con auto-lectura + anterior/siguiente y envío real de correo a Admins en Mailpit)
