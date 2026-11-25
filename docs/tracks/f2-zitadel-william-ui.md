# Track: [F2] IAM Portable — `Zitadel` + `WillIAM.UI` (Disco Rotatorio + Passkeys)

- **Estado**: Verificado
- **Spec de referencia**: `docs/iam/william-iam.md`, `docs/features/features-roadmap.md#f2`, `docs/architecture/adr-001-iam-strategy.md`
- **Servicios afectados**: `zitadel`, `WillIAM.UI`, `mailpit`

## Decisiones tomadas durante la ejecución
- `Zitadel` corre con `Login V2` habilitado y apunta a `WillIAM.UI` (`/ui/v2/login/login?authRequest=`).
- `WillIAM.UI` lee automáticamente el `login-client.pat` generado en el volumen compartido `/zitadel-bootstrap/login-client.pat`.
- Un script de bootstrap (`infra/zitadel/bootstrap.mjs` ejecutado al inicio o como servicio `zitadel-init`) asegura que exista el proyecto `babasketet` y la aplicación OIDC PKCE `babasketet-web`, dejando el `client_id` disponible en `/zitadel-bootstrap/client-id.txt`.
- Para `Teléfono + MagicLink` en desarrollo/tests sin proveedor SMS de pago, `WillIAM.UI` expone en modo `SMS_PROVIDER=mock` un buzón de inspección `/api/mock-sms` que guarda el último enlace mágico/OTP enviado.

## Incrementos (Vertical Slices)
- [x] Slice 1: Proyecto SvelteKit + TypeScript estricto + Dockerfile (`src/WillIAM.UI/`)
- [x] Slice 2: Componente `RotaryDial.svelte` (9 métodos en círculo, drag polar + teclado + click, selección en el norte)
- [x] Slice 3: Cliente servidor Zitadel API v2 (Session v2, OIDC AuthRequest v2, Email+Pass, Passkeys WebAuthn `@simplewebauthn/browser`, Email MagicLink/OTP, Teléfono E.164 MagicLink/OTP, 5 IDP intents sociales)
- [x] Slice 4: Bootstrap automático de proyecto y app OIDC en Zitadel + tests Vitest

## Verificación
- [x] `npm run check` y `npm run test` (Vitest 5/5) en verde dentro de Docker
- [x] `docker compose build william-ui` y healthcheck `/health` OK
- [x] Flujo OIDC + Disco Rotatorio + Passkey + MagicLinks verificados con Playwright contra Zitadel vivo
