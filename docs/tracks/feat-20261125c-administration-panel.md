# Track: `feat/20261125c-administration-panel` — Panel de Administración (`/admin-bo`), Roles Zitadel/WillIAM y Migraciones Reversibles

- **Estado**: Verificado
- **Rama**: `feat/20261125c-administration-panel` (desde `dev`)
- **Spec de referencia**: `docs/iam/william-iam.md`, `docs/backend/babasketet-api.md`, `docs/frontend/babasketet-web.md`, `docs/features/features-roadmap.md`
- **Servicios afectados**: `Zitadel Bootstrap`, `WillIAM.UI`, `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Decisiones de Diseño

### 1. Roles Globales y por Casa (`Zitadel/WillIAM` + `Babasketet.API`)
- **Roles de Proyecto en Zitadel (`Babasketet`)**:
  - `Guest` (Global): Usuarios anónimos de la app identificados únicamente por `deviceToken` (`DeviceId`), sin registro en Zitadel. Exclusivo: un usuario `Guest` solo puede tener el rol `Guest`.
  - `User` (Global): Todo usuario registrado en Zitadel (Email+Password, Passkey, MagicLink Email/SMS o Social SSO). En cuanto se registra, deja de ser `Guest` y obtiene el rol `User`.
  - `Admin` (Global): Acceso exclusivo al panel `/admin-bo` y endpoints `/api/v1/admin/*`. Inicialmente concedido únicamente a `jonathanvivero@gmail.com`.
  - `Owner` (Específico por casa): Solo aplicable a usuarios que ya son `User`. Inicialmente máximo 1 `owner` por casa:
    1. Si un `User` registrado crea una casa -> queda como `owner`.
    2. Si una casa no tiene ningún `User` registrado (solo anónimos) y el primer miembro anónimo de esa casa se registra / hace `claim` -> queda como `owner` de esa casa.
    3. Si una casa no tiene ningún `User` registrado y se invita a un usuario ya registrado (`User`), al canjear `/invitations/{token}/join` -> ese usuario queda como `owner` de esa casa.
  - Combinaciones válidas: `Guest` (solo); o con `User`: `User`, `User + Owner (1..N casas)`, `User + Owner (1..N casas) + Admin`, `User + Admin`.

### 2. Usuario Admin Inicial y Cambio Obligatorio de Contraseña
- `infra/zitadel/bootstrap.mjs` crea de forma idempotente el proyecto `Babasketet` con los 4 roles (`Guest`, `User`, `Admin`, `Owner`) y el usuario humano `jonathanvivero@gmail.com` con contraseña inicial `QWERTYUIOP-789456123!` y `password.changeRequired = true`, asignándole los roles `User` + `Admin`.
- `WillIAM.UI` detecta `passwordChangeRequired` tras validar la sesión de contraseña en `POST /api/auth/password`, presenta el formulario de cambio obligatorio de contraseña y completa el cambio vía `POST /api/auth/change-password` antes de finalizar la `authRequest` OIDC.
- Todo registro nuevo desde `WillIAM.UI` (Password, Passkey, MagicLink o Social IDP) recibe automáticamente el grant del rol `User` en el proyecto `Babasketet`.

### 3. Persistencia de Usuarios y Migraciones Reversibles (`Babasketet.API`)
- Nueva entidad `AppUser` (`app_users`) en `babasketet_db` (sin acoplamiento con `zitadel_db`) vinculada opcionalmente por `zitadel_user_id` (`sub`) o `device_id`, almacenando `email`, `display_name`, `status` (`Active`/`Blocked`), `is_admin`, `auth_method`, `custom_properties_json` (propiedades editables clave-valor en BD), `created_at` y `last_seen_at`.
- Migraciones EF Core reales y reversibles (`InitialBaseline` + `AddAppUsersAndOwnershipRules`) con soporte seguro para bases existentes previamente creadas con `EnsureCreated`.
- Bloqueo inmediato de usuarios con `status == Blocked` tanto para JWTs de Zitadel como para `DeviceToken` anónimos.

### 4. Panel `/admin-bo` en `Babasketet.WEB`
- Ruta `GoRoute(path: '/admin-bo', ...)` accesible únicamente a usuarios autenticados con rol `Admin`.
- Módulos incluidos:
  - **Casas**: Listado de casas + vista detalle de casa seleccionada (usuarios/roles, total de ítems únicos/productos, ítems actuales en Lista, ítems actuales en Cesta, compras y sus ítems).
  - **Usuarios**: Listado y editor completo de usuario (Estado `Activo|Bloqueado`, Rol global `Guest|User|Admin`, si está autenticado, propiedades guardadas en BD editables en formato clave-valor, y casas a las que pertenece con toggle `Owner` respetando la regla `Owner => User` y `Guest` exclusivo).
  - **Ítems**: Listado global ordenado alfabéticamente con nombre, tipo y casa; filtro por texto en tiempo real; agrupación automática si el mismo término (case-insensitive) y mismo tipo aparece en múltiples casas, mostrando `"N casas"` en lugar de una sola casa.

## Incrementos (Vertical Slices)
- [x] Slice 1: Bootstrap Zitadel (roles `Guest`, `User`, `Admin`, `Owner` + `jonathanvivero@gmail.com` con `changeRequired: true` + habilitación `loginV2`) + cambio obligatorio de contraseña y auto-asignación de rol `User` en `WillIAM.UI`
- [x] Slice 2: Modelo `AppUser`, migraciones reversibles EF Core, reglas de `Owner`/`Guest`/`User`/`Admin` en `HouseService` y bloqueo de usuarios en `Babasketet.API`
- [x] Slice 3: Endpoints `/api/v1/admin/*` (casas, detalle de casa, usuarios, edición de usuario/casas/propiedades, listado global de ítems agrupado) + `GET /api/v1/me`
- [x] Slice 4: UI `/admin-bo` en `Babasketet.WEB` (guard Admin, tabs Casas/Usuarios/Ítems, detalle de casa, editor de usuario) y scope `urn:zitadel:iam:org:project:roles` en `OidcService`
- [x] Slice 5: Documentación viva y tests unitarios, integración (Testcontainers) y Playwright E2E

## Verificación
- [x] `dotnet test` (`Babasketet.API.UnitTests`: `17/17` en verde; `Babasketet.API.IntegrationTests` con Testcontainers y test `Down`/`Up` de migraciones reversibles: `11/11` en verde)
- [x] `WillIAM.UI` (`npm run check && npm run test && npm run build`) y `Babasketet.WEB` (`flutter test`: `6/6`) en verde
- [x] `Playwright` E2E en verde (`12/12` pruebas pasadas: `01`..`09`, incluyendo verificación de `jonathanvivero@gmail.com`, cambio obligatorio de contraseña, guard `403` y panel `/admin-bo`)
