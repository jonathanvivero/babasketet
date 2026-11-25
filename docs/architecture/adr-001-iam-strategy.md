# ADR-001: Estrategia de IAM — Adopción de Zitadel + Custom Login UI + Passkeys

- **Estado**: **Aceptado** (Decisión confirmada: **Zitadel** como motor IAM + UI propia con disco rotatorio + soporte nativo de **Passkeys**)
- **Fecha**: 2026-11-23
- **Contexto**: Babasketet requiere un sistema IAM portable a otros proyectos propios, desplegable con Docker en un VPS, con PostgreSQL separado y soporte para:
  - **Passkeys (WebAuthn / FIDO2)** *(prioritario)*
  - Email + Password
  - Email + Magic Link / OTP
  - Teléfono (país + número) + Magic Link / OTP
  - SSO: Google, Meta (Facebook/Instagram), Apple, Microsoft, LinkedIn
  - **UI personalizada de selección de método estilo teléfono antiguo (disco rotatorio)**

---

## 1. Análisis de Alternativas Self-Hosted Existentes

Antes de invertir esfuerzo en construir y mantener un IAM propio (`WillIAM`), se evalúan las principales soluciones Open Source ya programadas y maduras para Docker/VPS:

| Solución | Stack / Licencia | RAM aprox. en VPS | OIDC / Portable | 5 SSO + Email/Pass | Magic Link Email & SMS | UI Disco Rotatorio | Veredicto |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Logto** | Node/TS + PostgreSQL (MPL-2.0) | ~300–500 MB | Sí (OIDC nativo, multi-app) | Sí (conectores listos) | Email/SMS OTP nativo (Magic Link vía custom) | Parcial (Bring Your Own UI o CSS custom) | **Mejor alternativa "lista para usar"** si quieres ahorrar tiempo |
| **Authentik** | Python + Go + PG + Redis (MIT/Open) | ~800 MB–1.5 GB | Sí (OIDC/SAML/LDAP) | Sí (muy completo) | Requiere configurar *Flows* personalizados | Difícil (requiere Web Components custom en flows) | Muy potente, pero pesado para un VPS pequeño y rígido para UI creativa |
| **Zitadel** | Go + PostgreSQL (Apache 2.0) | ~200–400 MB | Sí (OIDC nativo, multi-tenant) | Sí | OTP/Passwordless; SMS vía webhook | **Sí (Login UI desacoplada vía API V2)** | Excelente arquitectura ligera y moderna; curva de aprendizaje media |
| **Keycloak** | Java / Quarkus + PG (Apache 2.0) | ~600 MB–1.2 GB | Sí (estándar de facto) | Sí (todos) | Magic Link vía plugin comunitario | Compleja (temas Keycloakify/FreeMarker) | Robusto, pero pesado en RAM y costoso de personalizar en UI |
| **Casdoor** | Go + React + PG (Apache 2.0) | ~150–300 MB | Sí (OIDC/OAuth2/SAML) | Sí (decenas de SSO) | SMS/Email verificación nativa | Limitada (fork de frontend React) | Muy ligero, pero menos pulido en UX y documentación |
| **Pocket ID** | Go + SQLite/PG (BSD-2) | ~50–100 MB | Sí (OIDC) | **No** (solo Passkeys) | No | No | Descartado: no cubre los 8 métodos pedidos |
| **WillIAM (Propio)** | **SvelteKit + BetterAuth + PG** | **~100–200 MB** | **Sí** (plugin `oidcProvider` de BetterAuth) | **Sí** (nativo en BetterAuth) | **Sí** (plugins `magicLink` + `phoneNumber` / custom) | **100% libre y nativa** | **Máximo control, ligereza y extensibilidad** |

---

## 2. Evaluación Profunda: ¿Qué conviene más?

### Opción A — Solución existente + Frontend custom ligero: **Zitadel** o **Logto**
- **Por qué ahorra trabajo**:
  - Ya traen panel de administración de usuarios, auditoría, rotación automática de claves JWKS, gestión de clientes OIDC para tus futuros proyectos, MFA, bloqueo por fuerza bruta y cumplimiento estricto de OAuth2/OIDC sin que tengas que mantener código de seguridad crítico.
  - **Logto** es especialmente cómodo si vienes del ecosistema JS/TS + PostgreSQL: levanta con Docker Compose, tiene conectores preconfigurados para Google, Facebook, Apple, Microsoft, LinkedIn, Email y SMS, y permite conectar una UI externa o personalizar la experiencia.
  - **Zitadel** consume muy poca RAM (binario Go + Postgres, sin Redis obligatorio) y permite construir tu propia pantalla de login (por ejemplo, la pantalla del teléfono antiguo en Flutter o SvelteKit) hablando con su **Session/Login API v2**.
- **Inconveniente**:
  - Personalizar un flujo tan específico como *"Teléfono + Magic Link"* (enlace mágico por SMS/WhatsApp en lugar de código OTP de 6 dígitos) y la UI de disco rotatorio requiere pelearse con sus APIs de flujo o aceptar OTP numérico en vez de enlace clicable.
  - Menos control si más adelante quieres añadir lógica de negocio propia al IAM (roles a medida, planes Premium de Babasketet, etc.).

### Opción B — Desarrollar **`WillIAM`** con **SvelteKit + BetterAuth** (tu propuesta original)
- **Por qué encaja muy bien aquí**:
  - **BetterAuth** ya resuelve el 80% del trabajo duro que antes obligaba a usar Keycloak:
    - Proveedores OAuth integrados (`google`, `facebook`, `apple`, `microsoft`, `linkedin`).
    - Email + Password con hashing seguro (scrypt/argon2) y verificación.
    - Plugin oficial `magicLink` (Email) y plugin `phoneNumber` (extensible para enviar Magic Link por SMS/WhatsApp).
    - Plugin oficial `oidcProvider` / `jwt`: convierte a `WillIAM` en un **Identity Provider OIDC reutilizable** por `Babasketet.API` y por cualquier proyecto futuro que hagas.
    - Plugin `admin`: panel de gestión de usuarios, roles y baneos incluido.
  - **UI 100% a medida**: Implementar el selector de login estilo **teléfono antiguo de disco rotatorio** en SvelteKit (o en Flutter conectando a la API de WillIAM) es directo y limpio, sin luchar contra plantillas de Keycloak o Authentik.
  - **Consumo mínimo en VPS**: Un contenedor Node/Bun con SvelteKit + BetterAuth consume ~100–150 MB de RAM y cero dependencias extra (no requiere Redis).
- **Riesgo / Trade-off**:
  - Debes mantener las actualizaciones de BetterAuth, escribir el panel admin básico (si lo necesitas) y asegurar bien los flujos de seguridad (CSRF, rate limiting, rotación de claves, redirect URIs).

### Opción C — Enfoque Híbrido (Recomendación Pragmática)
1. **Si tu prioridad es ahorrar tiempo en el MVP de Babasketet**: Usa **Logto** (o **Zitadel**) en Docker Compose y coloca el **disco rotatorio** en la propia app `Babasketet.WEB` como selector previo que redirige directamente al conector elegido (`direct_sign_in` / `idp_hint`). Así obtienes un IAM de nivel producción en 1 día y mantienes intacta la UX del teléfono antiguo.
2. **Si tu prioridad es aprender/construir un IAM propio ultraligero, extensible y 100% tuyo para futuros proyectos**: Construye **`WillIAM` con SvelteKit + BetterAuth** apoyándote en sus plugins oficiales (`oidcProvider`, `jwt`, `magicLink`, `phoneNumber`, `admin`, `socialProviders`) para no reinventar criptografía ni OAuth2.

---

## 3. Nota Importante sobre los Proveedores Externos (aplica a cualquier opción)

Independientemente de si eliges **WillIAM**, **Logto** o **Zitadel**:
- **SSO Meta (Instagram/Facebook)**: Instagram Basic Display API fue deprecada por Meta; el login social de Meta se implementa mediante **Facebook Login (Meta for Developers)**, que cubre cuentas vinculadas del ecosistema Meta.
- **SSO Apple**: Requiere cuenta activa en **Apple Developer Program** (99 USD/año) para generar Service ID y clave privada `.p8`.
- **Teléfono + Magic Link**: Requiere un proveedor de envío SMS o WhatsApp Business API (ej. Twilio, Vonage, Brevo, AWS SNS) con coste por mensaje, salvo en entorno local/dev donde se puede imprimir el enlace en consola / Mailpit / mock SMS.

---

## 4. Decisión Final Aceptada

Se adopta **Zitadel** como motor IAM self-hosted (sobre PostgreSQL independiente `zitadel_db`) combinado con una **Login UI propia ligera** (basada en SvelteKit comunicándose con la **Session & OIDC API v2** de Zitadel, bajo el nombre de proyecto **`WillIAM`**) por los siguientes motivos:
1. **Passkeys (WebAuthn/FIDO2) nativos y listos para producción**: registro, login sin contraseña, gestión multi-dispositivo y MFA biométrico sin programar criptografía propia.
2. **UI 100% desacoplada**: Zitadel separa el motor de identidad de la interfaz de login, permitiendo implementar el **selector de disco rotatorio** con total libertad visual y reutilizarlo (o usar temas distintos) en otros proyectos futuros.
3. **Logins sociales y OIDC estándar**: Conectores listos para Google, Meta, Apple, Microsoft y LinkedIn, además de multi-tenancy / múltiples proyectos OIDC con bajo consumo en VPS (~200–400 MB RAM, sin Redis obligatorio).
4. **Cero acoplamiento con `Babasketet.API`**: El backend .NET 10 valida únicamente tokens JWT mediante OIDC/JWKS estándar (`OIDC_ISSUER`, `OIDC_AUDIENCE`, `OIDC_JWKS_URI`).

El diseño detallado queda recogido en [docs/iam/william-iam.md](../iam/william-iam.md).
