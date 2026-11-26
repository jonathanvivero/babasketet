# Track: `feat/20261126a-admin-bo-email-config` — Configuración de Correo Electrónico Saliente (SMTP) en `/admin-bo`

- **Estado**: Verificado
- **Rama**: `feat/20261126a-admin-bo-email-config` (desde `dev`)
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Diseño
- Nueva tabla `system_email_configs` en `babasketet_db` (migración EF Core reversible `20261126000001_AddSystemEmailConfig`) que persiste servidor SMTP, puerto, usuario, contraseña protegida con `IDataProtectionProvider`, modo de seguridad (`None`, `StartTls`, `SslTls`, `Auto`), remitente (`FromEmail`, `FromName`), estado habilitado y fecha de última actualización.
- Servicio reutilizable `IPlatformEmailService` (`MailKit`) consumible en futuras features (notificaciones e invitaciones) y expuesto bajo `/api/v1/admin/settings/email` (`GET`, `PUT`, `POST /test`).
- En `/admin-bo`, nueva pestaña **Configuraciones** con menú lateral/submenú que incluye el ítem **Correo Electrónico Saliente**, formulario completo y botón de envío de correo de prueba.

## Incrementos
- [x] Slice 1: Entidad `SystemEmailConfig`, migración EF Core reversible y servicio SMTP (`MailKit`) + protección de secreto
- [x] Slice 2: Endpoints `/api/v1/admin/settings/email` (`GET`, `PUT`, `POST /test`) con validación Fluent/Dominio
- [x] Slice 3: Sección **Configuraciones del sistema → Correo Electrónico Saliente** en `/admin-bo` (`Babasketet.WEB`)
- [x] Slice 4: Tests unitarios/integración (SMTP fake/Mailpit + migración reversible), widget test y E2E Playwright (`E2E-31`)

## Verificación
- [x] `dotnet test` (`Babasketet.API.UnitTests`: `17/17`, `Babasketet.API.IntegrationTests`: `12/12`) en verde
- [x] `flutter test` (`6/6`) en verde
- [x] `Playwright` E2E (`13/13` pruebas, `01`..`10` incluyendo `E2E-31` con envío real recibido en Mailpit) en verde
