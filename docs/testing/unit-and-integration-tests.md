# Plan de Tests Unitarios y de Integración — Babasketet & WillIAM

La pirámide de testing de **Babasketet** garantiza que cada regla de dominio, restricción SQL de PostgreSQL y componente de interfaz quede verificado de forma automatizada y reproducible en Docker.

---

## 1. `Babasketet.API` (.NET 10) — Tests Unitarios

- **Framework**: `xUnit` + `FluentAssertions` (o `AwesomeAssertions`) + `NSubstitute`.
- **Proyecto**: `tests/Babasketet.API.UnitTests/`
- **Regla**: Cero I/O, ejecución < 5 segundos.

### Catálogo de Casos Unitarios Obligatorios

| ID | Dominio | Caso de Prueba |
| :--- | :--- | :--- |
| `UT-API-01` | Casas | Validar que un usuario anónimo no puede superar el límite de **1 casa** (`HouseLimitExceededException`). |
| `UT-API-02` | Casas | Validar que un usuario registrado puede tener hasta **4 casas** y falla al intentar crear/unirse a la 5.ª. |
| `UT-API-03` | Invitaciones | Verificar generación de expiración exactamente a `createdAt + 1 hour` y validación con `TimeProvider` falso (59m59s válido vs 60m01s expirado). |
| `UT-API-04` | Productos | Normalización de nombre de producto (`"  Leche   Entera "` → `"leche entera"`) para deduplicación y sugerencias. |
| `UT-API-05` | Productos | Valores por defecto al crear ítem solo con nombre: `Format = "Ud"`, `Price = 0.00m`, `ProductTypeId = null`. |
| `UT-API-06` | Formatos | Rechazar formatos fuera del catálogo permitido (`Ud`, `Docena`, `Kg`, `g`, `L`, `Cl`, `Pack-2`, `Pack-3`, `Pack-4`, `Pack-6`, `Pack-8`). |
| `UT-API-07` | Tipos | Validar formato hexadecimal de `fillHex` (obligatorio) y `borderHex` (opcional o `#RRGGBB` válido). |
| `UT-API-08` | Cesta | Cálculo exacto de `Total` de cesta con múltiples líneas, decimales y productos sin precio (tratados como `0,00`). |
| `UT-API-09` | Cesta | Validación de decremento de unidades (nunca permite `<= 0`). |
| `UT-API-10` | Precios SCD-2 | Lógica de actualización de precio: si el nuevo precio es igual al vigente, no cierra ni inserta nuevo registro; si cambia, cierra `valid_to = now` y abre nuevo con `valid_to = null`. |
| `UT-API-11` | Compras | Bloqueo por política `PremiumOnly` al intentar adjuntar foto de ticket sin rol/plan Premium. |

---

## 2. `Babasketet.API` (.NET 10) — Tests de Integración

- **Framework**: `xUnit` + `WebApplicationFactory<Program>` + **`Testcontainers.PostgreSql`** (`postgres:17-alpine`).
- **Proyecto**: `tests/Babasketet.API.IntegrationTests/`
- **Regla crítica**: Nunca usar `UseInMemoryDatabase`. Todas las pruebas de integración corren contra **PostgreSQL real** con las migraciones EF Core aplicadas para verificar índices únicos parciales, claves foráneas y transacciones.

### Catálogo de Casos de Integración Obligatorios

| ID | Área | Caso de Prueba |
| :--- | :--- | :--- |
| `IT-API-01` | Seeds | Al arrancar migraciones en BD vacía, existen exactamente las **24 tiendas ES/PT** (con `Default` marcado) y los **18 tipos de producto** con sus colores y bordes exactos. |
| `IT-API-02` | Casas + Auth | Crear casa como anónimo → devuelve `deviceToken`; intentar crear 2.ª casa con mismo `deviceToken` devuelve `409 Conflict`. |
| `IT-API-03` | Claim Anónimo→Registrado | Usuario anónimo crea casa y añade 2 items → llama `POST /houses/claim` con JWT válido de `WillIAM` → casa migrada al `userId` y ahora permite crear 3 casas más (hasta 4). |
| `IT-API-04` | Invitaciones 1h | Generar token → unirse desde un 2.º dispositivo antes de 1h (`200 OK`) → avanzar reloj / manipular `expires_at` > 1h → intentar unirse devuelve `410 Gone`. |
| `IT-API-05` | Lista & Reordenación | Añadir 3 items, reordenar (`PUT /list/reorder`), consultar `GET /list` y confirmar orden persistido; swipe derecha (`move-to-basket`) mueve exactamente el item a `basket_items` con `quantity = 1`. |
| `IT-API-06` | **Histórico Precios SCD-2** | Consignar precio `1,20` en `Mercadona`, luego `1,35`, luego `1,10`: verificar que existen 3 filas en `product_price_history`, 2 con `valid_to NOT NULL` y exactamente **1** con `valid_to IS NULL` (`1,10`). |
| `IT-API-07` | **Constraint Índice Parcial** | Intentar insertar manualmente vía SQL dos registros con `valid_to IS NULL` para el mismo `(product_id, store_id)` → PostgreSQL lanza error `23505` (`ux_product_price_active`). |
| `IT-API-08` | **Aislamiento de Precios por Tienda** | Consignar `1,20` para `"Leche"` en `Mercadona` y nada en `Lidl` → `GET /basket` con tienda activa `Mercadona` devuelve `1,20`; cambiar tienda activa a `Lidl` y `GET /basket` devuelve `price = null`. |
| `IT-API-09` | Efectuar Compra | `POST /basket/checkout` crea `purchases` + `purchase_items` con snapshot de precios/cantidades y deja `basket_items` vacío en una sola transacción. |
| `IT-API-10` | Compras (Manual Total + Ticket Locked) | `PATCH /purchases/{id}/total` actualiza `manual_total`; `POST /purchases/{id}/receipt` sin claim Premium responde `403 Forbidden`. |
| `IT-API-11` | **SignalR Multi-Dispositivo** | 3 clientes `HubConnection` conectados a la misma casa; Cliente 1 añade item en Lista → Clientes 2 y 3 reciben el evento `ListUpdated` en tiempo real. |

---

## 3. `Babasketet.WEB` (Flutter) — Tests Unitarios y de Widgets

- **Framework**: `flutter_test` + `mocktail` + `riverpod` overrides.
- **Ubicación**: `src/Babasketet.WEB/test/`

### Catálogo Clave
- `WT-WEB-01`: **Disco Rotatorio (`RotaryAuthDial`)**: girar el ángulo `+40°` / `-40°` actualiza el método seleccionado en el "norte" (12 en punto) y dispara el callback correcto para los **9 métodos** (incluyendo Passkey).
- `WT-WEB-02`: **Long-Press 800 ms**: mantener pulsado `400ms` **no** abre el popup; mantener pulsado `850ms` **sí** abre el popup de edición (nombre y tipo en `Lista`; nombre, unidades, tipo, formato y eliminar en `Cesta`).
- `WT-WEB-03`: **Gesto vertical en unidades (`Cesta`)**: drag vertical hacia arriba incrementa `1 → 2 → 3`; drag hacia abajo decrementa `3 → 2 → 1` y se detiene en el mínimo.
- `WT-WEB-04`: **Renderizado de círculo de Tipo**: item sin tipo renderiza texto negro sin círculo; item con tipo `Lácteos` renderiza círculo blanco (`#FFFFFF`) con borde fucsia (`#FF00FF`).
- `WT-WEB-05`: **Autocompletado `[+]`**: escribir `"le"` filtra y muestra sugerencias del historial previo de la casa.
- `WT-WEB-06`: **Teclado numérico y formato decimal**: introducir `1,95` actualiza el precio y recalcula el total visible en la barra inferior de `Cesta`.
- `WT-WEB-07`: **Modal de confirmación "Efectuar Compra"**: cancelar mantiene la cesta intacta; confirmar invoca checkout y limpia la cesta.
- `WT-WEB-08`: **Botón Ticket Premium Locked**: muestra icono de candado y abre aviso Premium al pulsar.

---

## 4. `WillIAM.UI` (SvelteKit) + `Zitadel` — Tests Unitarios e Integración

- **Framework**: `Vitest` + cliente mock de Zitadel API v2 (unitarios) e instancia `Zitadel` + PostgreSQL (`zitadel_test_db`) en Docker (integración).
- **Ubicación**: `src/WillIAM.UI/tests/`

### Catálogo Clave
- `UT-IAM-01`: Componente **Disco Rotatorio (9 métodos)**: rotación circular (`40°` por paso), selección en el norte (`12:00`), teclado accesible y renderizado del formulario correspondiente.
- `IT-IAM-01`: Flujo de sesión **Email + Password** contra Zitadel Session API v2 (`POST /v2/sessions`); rechazo de credenciales inválidas.
- `IT-IAM-02`: Flujo **Passkey (WebAuthn / FIDO2)**: generación de opciones de registro/autenticación desde Zitadel (`POST /v2/users/{id}/passkeys` y `webAuthN` check en sesión) y validación del reto.
- `IT-IAM-03`: Flujo **Email + Magic Link / OTP** (captura de correo vía Mailpit API) y **Teléfono E.164 + Magic Link / OTP** (vía webhook mock SMS).
- `IT-IAM-04`: Exposición de `/.well-known/openid-configuration` y `/oauth/v2/keys` (JWKS) en Zitadel validables desde `Babasketet.API`.
- `IT-IAM-05`: Flujo OIDC Authorization Code + PKCE completo devolviendo JWT con `sub`, `email` y `aud` esperados por `Babasketet.API`.
- `IT-IAM-06`: Creación de *IDP Intent* (`POST /v2/idp_intents`) para proveedores sociales (Google, Meta, Apple, Microsoft, LinkedIn).
