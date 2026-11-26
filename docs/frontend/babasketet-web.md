# Especificación Técnica de Frontend — `Babasketet.WEB` (Flutter Web PWA)

## 1. Stack y Estrategia Multiplataforma

- **Framework**: Flutter (estable) compilado inicialmente a **Web (PWA)**, manteniendo compatibilidad limpia para futuros targets **Android e iOS**.
- **Renderer Web**: CanvasKit / WebAssembly (Wasm) según soporte del navegador, con `SemanticsBinding.instance.ensureSemantics()` activable para accesibilidad y tests E2E con **Playwright**.
- **Gestión de Estado**: **Riverpod** (`flutter_riverpod` + `riverpod_annotation`) para inyección de dependencias limpia, caché reactiva y fácil mocking en tests.
- **Navegación**: `go_router` con soporte de deep-links (`/`, `/onboarding`, `/join/:token`, `/auth`, `/house/:houseId`).
- **Almacenamiento Local**: `hive_ce` o `drift` (IndexedDB en Web, SQLite en Android/iOS) + `flutter_secure_storage` / storage web para tokens.
- **Comunicación**: `dio` (HTTP REST con interceptores de auth/retry) + `signalr_netcore` (WebSocket tiempo real).

### Estructura de Carpetas (`src/Babasketet.WEB/lib/`)
```text
lib/
├── core/
│   ├── config/            # Variables de entorno (--dart-define API_URL, IAM_URL)
│   ├── network/           # Dio client, SignalR client, cola offline
│   ├── storage/           # Adaptador local (casas locales, deviceId, caché)
│   ├── theme/             # Tipografía, paleta de colores, tokens de diseño
│   └── router/            # go_router + guards (onboarding / deep-link /join/:token)
├── features/
│   ├── houses/            # Crear casa, selector 1..4 casas, compartir (WhatsApp, Email, QR)
│   ├── auth/              # Integración WillIAM + widget Disco Rotatorio (Rotary Dial)
│   ├── stores/            # Selector de tienda arriba a la derecha + crear tienda
│   ├── dashboard/         # Shell principal (TopBar + 3 pestañas: Lista, Cesta, Compras)
│   ├── shopping_list/     # Pestaña Lista (input, historial, [+], reordenar, swipes, long-press)
│   ├── basket/            # Pestaña Cesta (unidades por gesto vertical, precio, total, checkout)
│   └── purchases/         # Pestaña Compras (histórico, detalle, total manual, ticket Premium Locked)
└── main.dart
```

---

## 2. Requisitos PWA

- `web/manifest.json`:
  - `id`: `"/"`, `start_url`: `"/"`, `scope`: `"/"`
  - `name`: `"Babasketet"`, `short_name`: `"Babasketet"`
  - `display`: `"standalone"`
  - `background_color` / `theme_color`: `"#FFB347"`
  - Iconos (`favicon.png`, `apple-touch-icon.png`, `Icon-192.png`, `Icon-512.png`): ilustración de una piña entrando en una cesta de baloncesto con líneas negras sobre fondo naranja medio claro.
- **Aviso de instalación en primera visita (`PwaInstallBanner`)**:
  - `web/index.html` captura `beforeinstallprompt` antes del arranque de Flutter.
  - En la primera visita fuera de modo `standalone`, se muestra un banner no bloqueante con opción directa de instalación nativa (Chromium/Android) o guía rápida (iOS Safari) y se recuerda la respuesta para no repetirlo tras cerrarlo.
- **Cuenta persistente en App instalada (`standalone`)**:
  - Botón de cuenta siempre accesible en el `AppBar` de `DashboardPage` (`account_btn`) además del menú lateral y Onboarding.
  - En modo `standalone`, el flujo OIDC PKCE intenta abrir un popup (`window.open`) con retorno por `postMessage` en `/auth/callback` para mantener intacta la ventana instalada y su `deviceToken` local (con fallback automático a `_self` si el navegador bloquea popups).
- **Service Worker**: Cacheo de shell de la aplicación para arranque instantáneo y funcionamiento offline básico dentro del supermercado.
- **Contenedor Docker**: Build multi-stage (`ghcr.io/cirruslabs/flutter` → `nginx:alpine`) con `try_files $uri $uri/ /index.html;` para que cualquier deep-link como `/join/<token>` cargue directamente la PWA.

---

## 3. Diseño de Pantallas e Interacciones Clave

### 3.1 Onboarding y Gestión de Casas
- Si no existe ninguna casa guardada localmente ni sesión activa: pantalla para **Crear Casa** (input de nombre) o **Unirse a una Casa** (pegar enlace o escanear QR).
- **Deep-link `/join/:token`**: Al abrir una URL compartida, la PWA llama automáticamente a `POST /api/v1/invitations/{token}/join`, guarda el `houseId` localmente y entra directo al **Dashboard** de esa casa.
- **Compartir Casa**:
  - Botón genera enlace con validez de **1 hora** y muestra cuenta atrás de expiración.
  - Acciones directas:
    - **WhatsApp**: `https://wa.me/?text=...` (o `share_plus` en móvil futuro).
    - **Correo**: `mailto:?subject=...&body=...`.
    - **Código QR**: renderizado en pantalla con `qr_flutter`.
- **Límite de casas**:
  - Sin alta (anónimo): 1 casa local; al intentar añadir una 2.ª, muestra diálogo invitando a registrarse en `WillIAM`.
  - Registrado: selector rápido en el TopBar/Menú para alternar entre hasta **4 casas**.

### 3.2 Selector de Login / Signup — Teléfono Antiguo (Disco Rotatorio)
- Flujo OIDC Authorization Code + PKCE contra **Zitadel**, con la interfaz de disco rotatorio servida por **`WillIAM.UI`** (y replicable como componente `RotaryAuthDial` en Flutter para modo nativo móvil futuro):
  - Los **9 modos de acceso** aparecen como botones/iconos circulares dispuestos equidistantemente en una circunferencia (`40°` de separación):
    1. **Passkey (WebAuthn / FIDO2)**
    2. Email + Password
    3. Email + Magic Link / OTP
    4. Teléfono (país + número) + Magic Link / OTP
    5. Google
    6. Meta (Facebook/Instagram)
    7. Apple
    8. Microsoft
    9. LinkedIn
  - El usuario **arrastra/rueda** el disco en sentido horario o antihorario (con inercia física y *snap* magnético al paso más cercano de `40°`).
  - Un indicador fijo en el **"norte" (12 en punto / `-π/2`)** resalta el método activo.
  - Al soltar o pulsar el centro/botón de confirmar, se despliega el formulario correspondiente (Passkey biométrico, Email/Pass, Email MagicLink, Selector de prefijo país + Teléfono) o se lanza el flujo OAuth2 del proveedor social en **Zitadel**.
  - **Accesibilidad / Playwright**: Además del gesto circular, cada icono puede seleccionarse con tap/click directo y soporta flechas izquierda/derecha + `data-testid` / `Semantics(identifier: 'auth_dial_option_<method>')`.

### 3.3 Dashboard (TopBar + 3 Pestañas)
- **TopBar**:
  - Izquierda: Icono de menú (acceso a Casas, Cuenta/Login, Tipos personalizados).
  - Centro: Nombre de la **Casa activa** (tappable si el usuario tiene >1 casa).
  - Derecha: **Icono del Supermercado elegido** (abre selector modal con las 24 tiendas por defecto + opción "Agregar supermercado").
- **Bottom / Top TabBar**: `Lista` | `Cesta` | `Compras`.

### 3.4 Pestaña `Lista`
- **Cabecera de entrada**:
  - Fila superior con: `[Botón Izquierdo: Historial de items]` | `[Caja de texto]` | `[Botón Derecho: Añadir]`.
  - **Icono redondo `[+]`** (FAB o acción destacada): enfoca/abre entrada rápida con **autocompletado en tiempo real** sobre productos ya añadidos anteriormente.
- **Lista de elementos (`ReorderableListView`)**:
  - Texto en **negro** por defecto.
  - Si tiene `Tipo`, muestra a la izquierda un **círculo** pintado con `fillHex` y `borderHex` (si el tipo tiene borde).
  - **Reordenación**: Drag & drop mediante handle o gesto de arrastre, persistiendo `PUT /list/reorder`.
  - **Swipe derecha (`Dismissible`)**: mueve el item a `Cesta` (feedback visual verde / icono carrito).
  - **Swipe izquierda (`Dismissible`)**: elimina el item de `Lista` (feedback visual rojo / papelera).
  - **Long-press (800 ms)**:
    - Usar detector dedicado con `Timer(Duration(milliseconds: 800))` (reducido en más de 1 segundo respecto a los 2000 ms iniciales, manteniendo indicador circular sutil de progreso para distinguir pulsación de tap/swipe).
    - Abre popup donde el usuario puede **editar el nombre del item**, elegir `Tipo` (círculo color+borde + nombre) y acceder a crear un tipo personalizado (nombre, color fondo, color borde).

### 3.5 Pestaña `Cesta`
- Muestra cada elemento movido desde `Lista`:
  - **Precio**: Si el producto tiene precio vigente en el supermercado activo, se muestra formateado (`1,45 €`); si no tiene precio consignado aún, no se muestra en la fila (al tocar para consignar parte de `0,00`).
  - **Unidades (default `1 ud.`)**:
    - Zona táctil de unidad con `GestureDetector(onVerticalDragUpdate)`:
      - Deslizar el dedo **hacia arriba** → incrementa unidades (`+1` por umbral de píxeles, con feedback háptico).
      - Deslizar el dedo **hacia abajo** → decrementa unidades (`-1`, mínimo `1` para `Ud`/`Pack`/`Docena` o paso decimal configurable para `Kg`/`L`).
  - **Consignar precio**:
    - Al tocar el área de precio, abre input enfocado con `TextInputType.numberWithOptions(decimal: true)` (teclado numérico del teléfono) precargado con el último precio en memoria (o `0,00`).
    - Al confirmar, envía `PUT /basket/items/{id}/price` (actualizando histórico SCD-2 para el supermercado actual).
  - **Swipe izquierda**: devuelve el producto a la pestaña `Lista`.
  - **Long-press (800 ms)**: abre popup con:
    1. **Cambiar nombre** (campo de texto pre-rellenado).
    2. **Eliminar** (desaparece definitivamente, no vuelve a `Lista`).
    3. **Cambiar ud.** (input numérico directo).
    4. **Cambiar tipo** (desplegable de tipos con círculo color+borde).
    5. **Cambiar formato** (`Ud`, `Docena`, `Kg`, `g`, `L`, `Cl`, `Pack-2`, `Pack-3`, `Pack-4`, `Pack-6`, `Pack-8`).
- **Pie fijo de la Cesta**:
  - **Total** acumulado en tiempo real (`SUM(precio * cantidad)`).
  - Botón **"Efectuar Compra"** → diálogo de confirmación → guarda en `Compras` y vacía la `Cesta`.

### 3.6 Pestaña `Compras`
- Lista cronológica inversa de compras realizadas (Fecha, Supermercado, Total).
- Al pulsar una compra, expande o navega al **detalle de elementos comprados**.
- Acciones en cada compra:
  - **Establecer / editar Total manual**: teclado numérico para ajustar el importe real del ticket.
  - **Adjuntar foto del ticket**: botón visible con icono de candado (**Locked — Solo versión Premium**); al pulsar muestra modal informativo de función Premium sin romper el flujo.

### 3.7 Panel de Administración (`/admin-bo`)
- Accesible exclusivamente para usuarios autenticados con rol global `Admin` desde `/admin-bo` (enlace en Drawer y Onboarding cuando `isAdmin == true`).
- Tres vistas principales:
  - **Casas**: listado con conteos y vista de detalle (miembros con roles/estado, ítems totales, Lista actual, Cesta actual, Compras e ítems).
  - **Usuarios**: listado y editor completo (Estado `Activo|Bloqueado`, rol `Guest|User|Owner|Admin`, indicador autenticado/anónimo, propiedades clave-valor en BD y casas con asignación de `Owner`).
  - **Ítems**: listado alfabético con filtro de texto y agrupación multi-casa (`"N casas"` cuando mismo texto case-insensitive y mismo tipo coinciden en más de una casa).
  - **Configuraciones → Correo Electrónico Saliente**: configuración SMTP (servidor, puerto, seguridad `StartTls`/`SslTls`/`None`/`Auto`, cuenta, contraseña protegida, remitente) con persistencia en BD y botón de prueba de envío.

---

## 4. Contrato de Semántica para Playwright

Todos los elementos críticos deben envolverse en `Semantics(identifier: '...')` con nombres estables:
- `onboarding_create_house_btn`, `onboarding_house_name_input`, `onboarding_admin_bo_btn`
- `tab_lista`, `tab_cesta`, `tab_compras`
- `store_selector_btn`, `list_input_field`, `list_add_btn`, `list_history_btn`, `list_fab_plus`
- `list_item_<normalized_name>`, `basket_item_<normalized_name>`, `basket_qty_<normalized_name>`, `basket_price_<normalized_name>`
- `basket_total_label`, `basket_checkout_btn`, `confirm_checkout_btn`
- `purchase_receipt_locked_btn`
- `auth_dial_wheel`, `auth_dial_option_<provider>`
- `admin_login_btn`, `admin_tab_houses`, `admin_tab_users`, `admin_tab_items`, `admin_user_editor`, `admin_user_save_btn`, `admin_items_search_input`
