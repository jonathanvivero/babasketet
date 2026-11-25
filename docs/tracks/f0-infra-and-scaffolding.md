# Track: [F0] Infraestructura Docker Compose, PostgreSQL Aislado y Zitadel Base

- **Estado**: Verificado
- **Spec de referencia**: `docs/architecture/architecture.md`, `docs/architecture/adr-001-iam-strategy.md`
- **Servicios afectados**: `postgres`, `zitadel`, `mailpit`, `proxy`, `Babasketet.API`, `WillIAM.UI`, `Babasketet.WEB`, `playwright`

## Decisiones tomadas durante la ejecución
- Se adopta PostgreSQL 17 (`postgres:17-alpine`) con dos bases de datos aisladas (`babasketet_db` y `zitadel_db`) y roles separados con permisos restringidos (`babasketet_user` y `zitadel_user`).
- En desarrollo local, Caddy enruta por subdominios locales (`*.localhost`) o puertos directos (`web`, `api`, `zitadel`, `william-ui`), mientras `docker-compose.prod.yml` configura los dominios HTTPS del VPS.
- Se incluye `mailpit` en el perfil de desarrollo/test para capturar los emails de MagicLink/OTP de Zitadel sin SMTP externo.

## Incrementos (Vertical Slices)
- [x] Slice 1: Scripts de inicialización PostgreSQL (`babasketet_db` + `zitadel_db`) y `.env.example`
- [x] Slice 2: `docker-compose.yml` (base local/dev) + `docker-compose.prod.yml` (VPS) + `docker-compose.test.yml` (CI/Playwright)
- [x] Slice 3: Bootstrap automático de Zitadel (proyecto OIDC, app `babasketet-web`, Service User PAT para `WillIAM.UI`, Login V2 y Passkeys habilitados)
- [x] Slice 4: Verificación `docker compose config` y arranque de `postgres` + `mailpit` + `zitadel`

## Verificación
- [x] `docker compose config` sin errores
- [x] PostgreSQL healthy con ambas BD aisladas
- [x] Zitadel responde en `/debug/healthz` y `/.well-known/openid-configuration`
