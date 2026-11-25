# AGENTS.md — Constitución de Babasketet (Organic-Driven Development)

Este documento define las reglas inmutables, el contexto técnico y el flujo operativo que **todo agente de IA** (y desarrollador) debe respetar al trabajar en el repositorio **Babasketet**.

---

## 1. Principios Inmutables del Proyecto

1. **Docker-First**: Todo servicio (`Babasketet.API`, `Babasketet.WEB`, `Zitadel`, `WillIAM.UI`, `PostgreSQL`, `Playwright` en CI) debe poder levantarse de forma reproducible mediante `docker compose`. Ninguna dependencia crítica debe requerir instalación manual en el host del VPS.
2. **Aislamiento de IAM (`Zitadel` + `WillIAM.UI`)**: El subsistema IAM es un producto independiente pensado para portarse a otros proyectos. **Nunca** debe compartir base de datos (`zitadel_db`), tablas, migraciones ni acoplamientos de dominio con `Babasketet.API` (`babasketet_db`). La comunicación entre ambos ocurre exclusivamente mediante estándares (OIDC/OAuth2/JWT + JWKS).
3. **Uso sin fricción (Anonymous-First)**: Un usuario **no está obligado** a registrarse para crear/unirse a una casa y gestionar compras. El registro es una mejora progresiva (persistencia de sesiones y ampliación de 1 a 4 casas).
4. **Integridad del Histórico de Precios**: El precio de un producto en un supermercado sigue un modelo temporal estricto (SCD Type 2). Nunca se sobrescribe un precio histórico; siempre se cierra el vigente (`valid_to = now()`) y se inserta uno nuevo (`valid_to IS NULL`) de forma atómica en una transacción.
5. **Mobile-First en Flutter Web**: Aunque el primer entregable es una PWA web, el código de `Babasketet.WEB` debe evitar dependencias exclusivas de `dart:html` fuera de adaptadores aislados para permitir compilar a Android e iOS sin reescrituras.
6. **Accesibilidad Testable**: Todos los controles interactivos de Flutter deben exponer `Semantics` estables y predecibles para que la suite de **Playwright** pueda interactuar con ellos de forma fiable.

---

## 2. Protocolo Organic-Driven Development (ODD — Gentle-AI)

En lugar de aplicar burocracia pesada a cada cambio, el agente **clasifica la tarea** antes de actuar y escala el proceso orgánicamente:

| Nivel | Tipo de Tarea | Flujo Obligatorio | Artefacto |
| :--- | :--- | :--- | :--- |
| **Tier 1 — Inline** | Bug trivial, ajuste de estilo, cambio acotado en 1–2 archivos con alcance obvio. | Leer contexto → Implementar → Ejecutar tests afectados → Verificar. | Ningún documento extra. |
| **Tier 2 — Explore** | Incertidumbre técnica, duda de diseño, integración externa o impacto transversal. | Explorar codebase/docs → Resumir hallazgos y opciones → Confirmar enfoque → Implementar y verificar. | Nota breve en PR o actualización puntual de doc existente. |
| **Tier 3 — Feature Track** | Nueva feature (F1–F7), cambio de esquema de BD, nuevo flujo IAM o refactor mayor. | Revisar spec en `docs/features/` → Crear/actualizar documento vivo de seguimiento (`docs/tracks/<feature>.md`) → Diseñar tareas atómicas → Implementar por incrementos → Verificar con unit + integration + Playwright → Actualizar docs afectadas. | Track vivo en `docs/tracks/` (se archiva al cerrar). |

### Reglas de Ejecución ODD
- **No inventar requisitos**: Si una regla de negocio no está en `docs/specs/` o `docs/features/`, preguntar al usuario antes de asumir.
- **Specs vivas**: Si durante la implementación cambia un contrato de API, tabla o comportamiento, actualizar el `.md` correspondiente en el mismo commit/PR.
- **Verificación real**: Ninguna tarea se marca como terminada sin ejecutar los tests pertinentes y confirmar que compila dentro del entorno definido.

---

## 3. Estructura Esperada del Monorepo (cuando inicie el código)

```text
babasketet/
├── AGENTS.md
├── README.md
├── docker-compose.yml              # Orquestación local / desarrollo
├── docker-compose.prod.yml         # Overrides de producción (VPS)
├── docs/                           # Documentación viva (ODD)
│   ├── methodology/
│   ├── architecture/
│   ├── backend/
│   ├── frontend/
│   ├── iam/
│   ├── specs/
│   ├── features/
│   ├── testing/
│   └── tracks/                     # Documentos vivos de features en curso (Tier 3)
├── src/
│   ├── Babasketet.API/             # Backend .NET 10
│   ├── Babasketet.WEB/             # Frontend Flutter Web PWA
│   └── WillIAM.UI/                 # Login UI propia (SvelteKit -> Zitadel Login API v2 + Disco Rotatorio + Passkeys)
└── tests/
    ├── Babasketet.API.UnitTests/
    ├── Babasketet.API.IntegrationTests/
    └── e2e-playwright/             # Suite Playwright E2E
```

---

## 4. Convenciones por Módulo

### `Babasketet.API` (.NET 10)
- Estilo: C# moderno (records, primary constructors, nullable reference types habilitados, `TreatWarningsAsErrors` recomendado).
- Arquitectura: Vertical Slices o Clean Architecture pragmática (`Domain`, `Application`, `Infrastructure`, `Api`).
- Persistencia: EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL`. Migraciones versionadas en código.
- Validación: FluentValidation / DataAnnotations en entrada; respuestas de error en formato RFC 9457 (`ProblemDetails`).
- Tiempo real: SignalR Hub autenticado por `houseId` para propagar cambios a los 3+ dispositivos conectados.

### `Babasketet.WEB` (Flutter)
- Gestión de estado: Riverpod (recomendado) o Bloc, con separación estricta entre capa de datos (repositorios HTTP/WS/Local) y UI.
- Rutas: `go_router` con soporte nativo para deep-links de invitación (`/join/:token`).
- Internacionalización: `es-ES` como idioma base (separadores decimales con coma en UI, ej. `0,00 €`), preparado para `pt-PT` / `en`.
- Semántica: Toda acción clave lleva `Semantics(identifier: '...', label: '...')` para Playwright.

### `Zitadel` + `WillIAM.UI` (IAM Portable)
- Motor IAM: Contenedor oficial de **Zitadel** con base de datos PostgreSQL exclusiva (`zitadel_db`).
- Login UI propia (`WillIAM.UI`): SvelteKit + TypeScript estricto (`strict: true`), conectada a Zitadel **Session / OIDC API v2** mediante Service User PAT (solo en servidor, nunca expuesto al navegador).
- Métodos obligatorios: **Passkeys (WebAuthn/FIDO2)**, Email+Password, Email/Teléfono MagicLink/OTP y 5 proveedores sociales (Google, Meta, Apple, Microsoft, LinkedIn).
- `Babasketet.API` valida JWTs exclusivamente contra `/.well-known/openid-configuration` y `JWKS` de Zitadel sin acoplarse a `zitadel_db`.

### Testing & QA
- **Unitarios**: Rápidos, deterministas, sin I/O real.
- **Integración**: PostgreSQL real vía **Testcontainers** (nunca InMemoryDatabase de EF Core para probar restricciones SQL ni índices parciales).
- **E2E (Playwright)**: Escenarios de usuario completos, incluyendo pruebas con múltiples contextos de navegador simulando 3 dispositivos concurrentes en la misma casa.

---

## 5. Checklist Antes de Cerrar Cualquier Tarea

- [ ] Los cambios respetan el alcance del Tier ODD elegido.
- [ ] Las reglas de negocio coinciden con `docs/specs/domain-catalogs.md` y `docs/features/features-roadmap.md`.
- [ ] Tests unitarios y de integración en verde.
- [ ] Si afecta a flujos de UI o contratos críticos, tests de Playwright actualizados y en verde.
- [ ] Documentación sincronizada si cambió algún contrato, entidad o variable de entorno.
