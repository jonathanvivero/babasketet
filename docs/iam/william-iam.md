# Especificación Técnica de IAM — `Zitadel` + `WillIAM.UI` (Passkeys + Disco Rotatorio)

> **Decisión arquitectónica**: Ver [ADR-001](../architecture/adr-001-iam-strategy.md) (**Aceptado**). Se utiliza **Zitadel** como motor IAM portable sobre PostgreSQL independiente (`zitadel_db`), acompañado de una **Login UI propia (`WillIAM.UI`)** en SvelteKit conectada a la **Session & OIDC API v2** de Zitadel.

---

## 1. Propósito y Portabilidad

El subsistema IAM está pensado para:
1. Dar servicio de autenticación, **Passkeys (WebAuthn/FIDO2)**, logins sociales y gestión de sesiones a **Babasketet**.
2. **Portarse directamente a futuros proyectos** creando simplemente nuevos *Projects* y *Applications* (OIDC/PKCE) desde la consola de administración de Zitadel, sin duplicar servidores ni bases de datos de usuarios.

### Principios de Diseño
- **Base de datos propia**: `zitadel_db` (PostgreSQL). Cero conocimiento del esquema de `babasketet_db`.
- **Login UI desacoplada (`WillIAM.UI`)**: SvelteKit actúa como capa de presentación (*Bring Your Own Login UI*) que habla por servidor con la API v2 de Zitadel (`POST /v2/sessions`, `POST /v2/oidc/auth_requests/{id}`, `POST /v2/users/{id}/passkeys`).
- **Emisión estándar OIDC/JWT**: `Babasketet.API` valida JWTs exclusivamente contra `https://auth.tu-dominio.com/.well-known/openid-configuration` y su `JWKS`.

---

## 2. Stack Tecnológico del Subsistema IAM

- **Motor IAM**: **Zitadel** (`ghcr.io/zitadel/zitadel`, binario Go + PostgreSQL `zitadel_db`, sin Redis obligatorio).
- **Login UI Propia (`src/WillIAM.UI/`)**:
  - **SvelteKit** (`@sveltejs/adapter-node`, TypeScript estricto) + `@simplewebauthn/browser` para el flujo nativo de **Passkeys (WebAuthn/FIDO2)** en el navegador.
  - Autenticación servidor-a-servidor contra Zitadel usando un **Service User (Machine User)** con **PAT** (Personal Access Token) almacenado solo en variables de entorno de servidor (nunca enviado al cliente).
- **Notificaciones**:
  - **Email**: Configuración SMTP nativa en Zitadel (Mailpit en local/CI; Brevo/Resend/SES en VPS).
  - **Teléfono (SMS / WhatsApp)**: Proveedor SMS nativo de Zitadel (Twilio) o webhook HTTP personalizado hacia un adaptador ligero (mock en dev/CI).

---

## 3. Los 9 Métodos de Autenticación (incluyendo Passkeys)

| # | Método | Soporte en Zitadel + `WillIAM.UI` | Notas de Implementación |
| :-: | :--- | :--- | :--- |
| 1 | **Passkey (WebAuthn / FIDO2)** *(Destacado)* | Nativo en Zitadel (`Passkey` / `WebAuthn` passwordless + `discoverable credentials`) | Registro e inicio de sesión biométrico (FaceID / TouchID / Windows Hello / Llave hardware) vía `@simplewebauthn/browser` + Zitadel Session API v2. |
| 2 | **Email + Password** | Nativo en Zitadel | Política de contraseñas y bloqueo por fuerza bruta gestionados por Zitadel. |
| 3 | **Email + MagicLink / OTP** | Nativo en Zitadel (OTP por email; enlace clicable en plantilla de email de Zitadel hacia `WillIAM.UI` que autocompleta el código) | En dev/CI se captura mediante **Mailpit**. |
| 4 | **Teléfono (país + número) + MagicLink / OTP** | Normalización E.164 (`+34`, `+351`, etc.) + SMS OTP/enlace de Zitadel vía Twilio o Webhook | En dev/CI se captura mediante mock SMS. |
| 5 | **SSO Google** | Identity Provider nativo `Google` en Zitadel | Redirect URI de Zitadel + auto-registro / auto-vinculación activable. |
| 6 | **SSO Meta (Instagram/Facebook)** | Identity Provider OAuth2/OIDC `Meta / Facebook` en Zitadel | Usa Facebook Login (Meta for Developers) para cuentas del ecosistema Meta. |
| 7 | **SSO Apple** | Identity Provider nativo `Apple` en Zitadel | Requiere Service ID + clave `.p8` de Apple Developer. |
| 8 | **SSO Microsoft** | Identity Provider nativo `Azure AD / Entra ID` en Zitadel | Soporta cuentas personales y corporativas. |
| 9 | **SSO LinkedIn** | Identity Provider OIDC `LinkedIn` en Zitadel | Flujo OIDC estándar de LinkedIn. |

### Passkeys: Experiencia de Usuario Recomendada
- **Registro progresivo**: Cuando un usuario se da de alta con Email, Teléfono o SSO, `WillIAM.UI` le ofrece inmediatamente *"Activar Passkey en este dispositivo"* para futuros accesos en 1 toque.
- **Login sin escribir usuario (*Discoverable Passkey / Conditional UI*)**: Al elegir **Passkey** en el "norte" del disco rotatorio, el navegador invoca `navigator.credentials.get()` y autentica directamente con biometría sin teclear email ni contraseña.

---

## 4. UI de Login / Signup — Teléfono Antiguo (Disco Rotatorio)

`WillIAM.UI` implementa el selector circular estilo **teléfono antiguo (disco rotatorio)**:
- Los **9 iconos redondos** se disponen equidistantemente en círculo (`40°` de separación entre métodos).
- El usuario gira el disco arrastrando en círculo (touch/mouse), con rueda o haciendo clic directo sobre un icono.
- El método situado en el **"norte" (12 en punto)** es el seleccionado:
  - Si es **Passkey**: muestra botón directo *"Entrar / Registrar con Passkey"*.
  - Si es **Email + Password**, **Email + MagicLink** o **Teléfono + MagicLink**: despliega los campos mínimos debajo del disco.
  - Si es un **SSO** (Google, Meta, Apple, Microsoft, LinkedIn): dispara `POST /v2/idp_intents` de Zitadel para redirigir al proveedor elegido.
- **Portabilidad visual**: Para futuros proyectos conectados al mismo Zitadel, `WillIAM.UI` puede mostrar el tema `rotary-dial` para Babasketet o un tema estándar para otros clientes según el `projectId` / `clientId` de la `authRequest`.

---

## 5. Flujo de Integración con `Babasketet` (Anónimo → Registrado)

```mermaid
sequenceDiagram
    participant U as Usuario (PWA Flutter)
    participant L as WillIAM.UI (SvelteKit)
    participant Z as Zitadel (Motor IAM)
    participant A as Babasketet.API (.NET 10)

    Note over U,A: El usuario ya usa Babasketet de forma anónima con 1 casa (deviceToken)
    U->>Z: Inicia OIDC Auth Code + PKCE (/oauth/v2/authorize)
    Z-->>L: Redirige a WillIAM.UI con authRequestId
    Note over U,L: Usuario gira el disco rotatorio (ej. Passkey o Google)
    L->>Z: Crea/verifica sesión (API v2: Passkey / Password / IDP Intent)
    L->>Z: Finaliza authRequest -> devuelve callbackUrl con code
    L-->>U: Redirige a Babasketet.WEB con authorization_code
    U->>Z: Intercambia code + code_verifier -> access_token (JWT) + refresh_token
    U->>A: POST /api/v1/houses/claim (Auth: JWT Zitadel + X-Device-Token)
    A->>Z: Valida firma JWT contra /.well-known/openid-configuration y JWKS
    A->>A: Migra house_members(deviceId) -> userId (sub)
    A-->>U: 200 OK (casa vinculada; límite ampliado a 4 casas)
```

---

## 6. Variables de Entorno Clave

### `Zitadel`
```env
ZITADEL_MASTERKEY=<32-bytes-exactos>
ZITADEL_EXTERNALDOMAIN=auth.tu-dominio.com
ZITADEL_EXTERNALPORT=443
ZITADEL_EXTERNALSECURE=true
ZITADEL_DATABASE_POSTGRES_HOST=postgres
ZITADEL_DATABASE_POSTGRES_PORT=5432
ZITADEL_DATABASE_POSTGRES_DATABASE=zitadel_db
ZITADEL_DATABASE_POSTGRES_USER_USERNAME=zitadel_user
ZITADEL_DATABASE_POSTGRES_USER_PASSWORD=***
ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_REQUIRED=true
ZITADEL_DEFAULTINSTANCE_FEATURES_LOGINV2_BASEURI=https://login.tu-dominio.com/
```

### `WillIAM.UI` (SvelteKit)
```env
ZITADEL_API_URL=http://zitadel:8080
ZITADEL_SERVICE_USER_TOKEN=<pat-service-user-solo-servidor>
PUBLIC_RP_ID=tu-dominio.com
```

### `Babasketet.API` (.NET 10)
```env
OIDC_ISSUER=https://auth.tu-dominio.com
OIDC_AUDIENCE=<zitadel-project-id>
OIDC_JWKS_URI=https://auth.tu-dominio.com/oauth/v2/keys
```
