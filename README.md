# Babasketet

**Babasketet** es una Progressive Web App (PWA) multi-dispositivo para gestionar la lista de la compra compartida en el hogar (3 o más dispositivos sincronizados), pensada para ejecutarse íntegramente en **Docker / Docker Compose** sobre un VPS propio y preparada para evolucionar posteriormente a app móvil nativa (Android e iOS).

---

## Stack Tecnológico

| Componente | Proyecto | Tecnología | Base de Datos |
| :--- | :--- | :--- | :--- |
| **Backend (API REST + Real-time)** | `Babasketet.API` | .NET 10 (ASP.NET Core + EF Core + SignalR) | PostgreSQL (`babasketet_db`) |
| **Frontend (PWA / Móvil futuro)** | `Babasketet.WEB` | Flutter (Web PWA; preparado para Android/iOS) | Storage local (IndexedDB / Hive) |
| **IAM (Identidad y Acceso)** | `Zitadel` + `WillIAM.UI` | **Zitadel** (OIDC + Passkeys/WebAuthn + Social) + Login UI propia (SvelteKit) *(ver [ADR-001](docs/architecture/adr-001-iam-strategy.md))* | PostgreSQL (`zitadel_db`) |
| **Orquestación & Despliegue** | Infraestructura | Docker + Docker Compose + Reverse Proxy (Caddy/Traefik) | PostgreSQL 17 |
| **Testing** | QA / CI | xUnit + Testcontainers · Flutter Test · Vitest · **Playwright** | Efímeras en contenedor |

---

## Metodología: Organic-Driven Development (ODD)

Este proyecto sigue **Organic-Driven Development (ODD)**, la evolución de Spec-Driven Development del ecosistema **Gentle-AI**: la ceremonia documental y de planificación se adapta orgánicamente a la complejidad real de cada tarea, evitando burocracia en cambios simples y garantizando trazabilidad y verificación estricta en features sustanciales.

- Constitución y reglas para agentes: [AGENTS.md](AGENTS.md)
- Guía del flujo de trabajo ODD: [docs/methodology/organic-driven-development.md](docs/methodology/organic-driven-development.md)

---

## Mapa de Documentación

### 1. Metodología y Agentes
- [AGENTS.md](AGENTS.md) — Constitución del repositorio, reglas de ingeniería y protocolo ODD para agentes de IA.
- [docs/methodology/organic-driven-development.md](docs/methodology/organic-driven-development.md) — Flujo adaptativo Gentle-AI (Tier 1 / Tier 2 / Tier 3), plantillas de seguimiento y puertas de verificación.

### 2. Arquitectura e Infraestructura
- [docs/architecture/architecture.md](docs/architecture/architecture.md) — Arquitectura general, topología Docker Compose para VPS, sincronización multi-dispositivo y seguridad.
- [docs/architecture/adr-001-iam-strategy.md](docs/architecture/adr-001-iam-strategy.md) — Análisis comparativo: construir **WillIAM** (SvelteKit + BetterAuth) vs soluciones IAM existentes (Logto, Authentik, Zitadel, Pocket ID, Keycloak, Casdoor).

### 3. Especificaciones Técnicas por Módulo
- [docs/backend/babasketet-api.md](docs/backend/babasketet-api.md) — Diseño de `Babasketet.API` (.NET 10), modelo relacional PostgreSQL (incluyendo histórico de precios SCD-2), endpoints REST y SignalR.
- [docs/frontend/babasketet-web.md](docs/frontend/babasketet-web.md) — Diseño de `Babasketet.WEB` (Flutter Web PWA), gestos táctiles, selector de login de disco rotatorio, modo offline y preparación móvil.
- [docs/iam/william-iam.md](docs/iam/william-iam.md) — Diseño del subsistema IAM con **Zitadel** (`zitadel_db`) + Login UI propia (`WillIAM.UI`), **Passkeys (WebAuthn)**, 9 métodos en disco rotatorio y portabilidad OIDC.

### 4. Dominio y Features
- [docs/specs/domain-catalogs.md](docs/specs/domain-catalogs.md) — Catálogos semilla (24 supermercados ES/PT, 18 tipos de producto con colores y bordes, 11 formatos, límites y reglas).
- [docs/features/features-roadmap.md](docs/features/features-roadmap.md) — Especificaciones funcionales (F1–F7), criterios de aceptación (Given/When/Then) y casos borde.

### 5. Calidad y Testing
- [docs/testing/unit-and-integration-tests.md](docs/testing/unit-and-integration-tests.md) — Plan y catálogo de tests unitarios y de integración (`Babasketet.API`, `Babasketet.WEB`, `WillIAM`).
- [docs/testing/e2e-playwright.md](docs/testing/e2e-playwright.md) — Plan de automatización E2E con Playwright (Flutter Web semantics, pruebas multi-dispositivo y gestos táctiles).
