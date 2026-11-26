# Plan de Automatización de UI (E2E) con Playwright — Babasketet

La suite de **Playwright** valida de extremo a extremo la integración real de `Babasketet.WEB` (Flutter Web PWA) + `Babasketet.API` (.NET 10) + `Zitadel` + `WillIAM.UI` (SvelteKit) + `PostgreSQL` levantados mediante **Docker Compose**.

---

## 1. Consideración Técnica Crítica: Playwright + Flutter Web

Flutter Web renderiza sobre `<canvas>` (CanvasKit/Wasm), por lo que los elementos HTML clásicos (`button`, `input`) no existen en el DOM hasta que se activa el **árbol de accesibilidad (`flt-semantics`)**.

### Estrategia Obligatoria en `Babasketet.WEB` + Playwright
1. **Activar semántica en Flutter Web**:
   - Llamar a `SemanticsBinding.instance.ensureSemantics()` en el arranque cuando `--dart-define=ENABLE_SEMANTICS=true` (o automáticamente al detectar accesibilidad).
   - Como alternativa de respaldo en tests, el helper de Playwright activa el nodo `<flt-semantics-placeholder>` antes de interactuar.
2. **Selectores estables por `Semantics(identifier: ...)`**:
   - Flutter expone `identifier` como atributo `flt-semantics-identifier` / `aria-label` en el DOM semántico.
   - Playwright utiliza locators semánticos (`page.getByRole(...)`, `page.locator('[flt-semantics-identifier="..."]')`) en lugar de coordenadas frágiles siempre que sea posible.
3. **Gestos táctiles complejos (Swipe, Long-Press > 2s, Vertical Drag, Disco Rotatorio)**:
   - Se prueban con la API de puntero de Playwright (`page.mouse.move` / `down` / `up` y `touchscreen` en proyectos móviles emulados como `Pixel 7` e `iPhone 14`), incluyendo esperas reales de `2100 ms` para validar el umbral de **long-press > 2 segundos**.

---

## 2. Estructura del Proyecto Playwright (`tests/e2e-playwright/`)

```text
tests/e2e-playwright/
├── playwright.config.ts       # Proyectos: Mobile Chrome (Pixel 7), Mobile Safari (iPhone 14), Desktop Chrome
├── fixtures/
│   └── multi-device.ts        # Helper que crea 3 BrowserContexts aislados (Dispositivo A, B y C)
├── helpers/
│   ├── flutter-semantics.ts   # Activación de flt-semantics y helpers de swipe / long-press >2s / vertical-drag
│   └── mailpit.ts             # Lectura de Magic Links desde Mailpit en entorno Docker de tests
└── specs/
    ├── 01-houses-and-sharing.spec.ts
    ├── 02-auth-rotary-dial.spec.ts
    ├── 03-stores-catalog.spec.ts
    ├── 04-list-gestures-and-types.spec.ts
    ├── 05-basket-prices-and-checkout.spec.ts
    ├── 06-purchases-and-premium-lock.spec.ts
    └── 07-multi-device-realtime-sync.spec.ts
```

---

## 3. Catálogo de Escenarios E2E Obligatorios

### `01-houses-and-sharing.spec.ts` — Casas, Enlace 1h y Límites
- **E2E-01**: Usuario anónimo entra por primera vez, crea `"Mi Casa"` y aterriza en el Dashboard con tienda `Default`.
- **E2E-02**: Genera enlace de invitación, verifica presencia de botones **WhatsApp**, **Correo** y renderizado del **QR**; un segundo `BrowserContext` abre la URL de invitación y entra directamente en `"Mi Casa"`.
- **E2E-03**: Usuario anónimo intenta crear una 2.ª casa → aparece aviso de límite (1 casa sin registro) con CTA hacia Login/Signup.
- **E2E-04**: Enlace caducado (> 1h, simulado vía endpoint/fixture de test) muestra pantalla de enlace expirado.

### `02-auth-rotary-dial.spec.ts` — `Zitadel`, `WillIAM.UI`, Disco Rotatorio y Passkeys
- **E2E-05**: Abrir Login/Signup (`WillIAM.UI`), verificar el **disco rotatorio** con los **9 métodos** en círculo; rotar el disco hasta colocar **Email + Password** en el "norte", registrarse en Zitadel y confirmar que la casa anónima previa se mantiene vinculada (`POST /houses/claim`).
- **E2E-05b (Passkey / WebAuthn)**: Usando el autenticador virtual de Chromium vía CDP (`WebAuthn.enable` + `WebAuthn.addVirtualAuthenticator` con `protocol: 'ctap2'`, `hasResidentKey: true`, `hasUserVerification: true`), registrar una **Passkey**, cerrar sesión, girar el disco rotatorio hasta **Passkey** en el norte y volver a iniciar sesión sin contraseña.
- **E2E-06**: Rotar el disco hasta **Email + MagicLink/OTP**, solicitar acceso, recuperarlo de **Mailpit**, completarlo y verificar sesión iniciada.
- **E2E-07**: Rotar el disco hasta **Teléfono + MagicLink/OTP**, introducir país `+34` + número, recuperar enlace/código del webhook mock SMS y autenticar.
- **E2E-08**: Usuario registrado crea hasta **4 casas**, alterna entre ellas desde el TopBar verificando aislamiento de listas, y comprueba que la 5.ª casa queda bloqueada.

### `03-stores-catalog.spec.ts` — Supermercados
- **E2E-09**: Abrir selector de tienda arriba a la derecha → verificar `Default` marcado y presencia de tiendas clave (`Mercadona`, `Carrefour`, `Lidl`, `Pingo Doce`, `Continente`, etc.).
- **E2E-10**: Agregar un supermercado personalizado (`"Mercado Central"`), seleccionarlo y verificar que el icono/etiqueta superior se actualiza.

### `04-list-gestures-and-types.spec.ts` — Pestaña `Lista`
- **E2E-11**: Añadir `"Manzanas"` con la caja de texto y botón derecho; usar el botón izquierdo para re-seleccionar un item usado anteriormente; usar el botón redondo `[+]` y comprobar sugerencias mientras escribe.
- **E2E-12**: Verificar texto en **negro** sin círculo por defecto; realizar **long-press (~900 ms)** → abrir popup → **editar el nombre** y asignar `Fruta/Verdura` → verificar nuevo nombre e indicador visual del tipo.
- **E2E-13**: Crear un **tipo personalizado** (nombre, color de fondo y borde) desde el popup y asignarlo a un item.
- **E2E-14**: **Reordenar** 2 items mediante drag & drop y recargar página para confirmar persistencia del orden.
- **E2E-15**: **Swipe derecha** sobre un item lo mueve a `Cesta`; **Swipe izquierda** sobre otro lo elimina de `Lista`.

### `05-basket-prices-and-checkout.spec.ts` — Pestaña `Cesta` y Precios
- **E2E-16**: En `Cesta`, tocar la unidad (`1 ud.`) y deslizar hacia arriba → pasa a `2 ud.`; deslizar hacia abajo → vuelve a `1 ud.`.
- **E2E-17**: Consignar precio (`2,50`) para `"Leche"` en `Mercadona` → verificar actualización del **Total** (`SUM(precio * unidades)`). Cambiar supermercado a `Lidl` → verificar que no se muestra precio para `"Leche"`; volver a `Mercadona` → vuelve a mostrarse `2,50`.
- **E2E-18**: Actualizar precio de `2,50` a `2,75` en `Mercadona` y confirmar que el nuevo precio rige el Total.
- **E2E-19**: **Long-press (~900 ms)** en `Cesta` → **editar el nombre**, cambiar formato a `Pack-6` y cambiar unidades desde el popup; probar opción **Eliminar** (desaparece sin volver a `Lista`).
- **E2E-20**: **Swipe izquierda** en `Cesta` devuelve el producto a `Lista`.
- **E2E-21**: Pulsar **"Efectuar Compra"** → verificar diálogo de confirmación → confirmar → la `Cesta` queda vacía y la compra aparece en `Compras`.

### `06-purchases-and-premium-lock.spec.ts` — Pestaña `Compras`
- **E2E-22**: Abrir la compra recién realizada en `Compras`, verificar fecha, supermercado y listado de elementos.
- **E2E-23**: Modificar el **total manual** de la compra y comprobar persistencia tras recarga.
- **E2E-24**: Pulsar en "Adjuntar ticket como foto" → verificar estado **Locked (Solo versión Premium)** y que no permite subir archivo.

### `07-multi-device-realtime-sync.spec.ts` — 3 Dispositivos Concurrentes
- **E2E-25**: Levantar **3 `BrowserContext` simultáneos** (`Dispositivo 1`, `Dispositivo 2`, `Dispositivo 3`) conectados a la misma casa:
  1. `Dispositivo 1` añade `"Café"` en `Lista` → `Dispositivo 2` y `Dispositivo 3` lo ven aparecer sin recargar.
  2. `Dispositivo 2` desliza `"Café"` a `Cesta` y sube a `2 ud.` → `Dispositivo 1` y `Dispositivo 3` ven el cambio en `Cesta`.
  3. `Dispositivo 3` consigna precio y pulsa **"Efectuar Compra"** → los 3 dispositivos ven la `Cesta` vacía y la nueva compra en `Compras`.

---

### `08-pwa-install-and-account.spec.ts` — Prompt PWA, Icono y Cuenta en App Instalada
- **E2E-26**: En la primera visita aparece `pwa_install_banner`; al cerrarlo y recargar la página no vuelve a aparecer.
- **E2E-27**: `manifest.json`, `favicon.png`, `icons/Icon-192.png` y `icons/Icon-512.png` responden `200 OK` con iconos válidos.
- **E2E-28**: En el Dashboard (y simulando modo `standalone`), el botón de cuenta (`account_btn`) permanece visible y abre el diálogo con acceso a `"Crear cuenta / Iniciar sesión"`.

### `09-admin-panel-and-roles.spec.ts` — Panel `/admin-bo`, Primer Acceso Admin y Roles
- **E2E-29**: Verifica en Zitadel los 4 roles (`Guest`, `User`, `Admin`, `Owner`) y el usuario `jonathanvivero@gmail.com` con `passwordChangeRequired: true`; valida el flujo completo de cambio obligatorio de contraseña en primer acceso vía `WillIAM.UI` y recorre `/admin-bo` (Casas + detalle, Usuarios + edición, Ítems ordenados/filtrados).
- **E2E-30**: Verifica que un usuario registrado estándar (`User` sin `Admin`) recibe `403 · Acceso denegado` en `/admin-bo`.

### `10-admin-email-config.spec.ts` — Configuraciones > Correo Electrónico Saliente
- **E2E-31**: Admin accede a `Configuraciones → Correo Electrónico Saliente`, configura SMTP contra `mailpit:1025`, recarga para confirmar persistencia en BD y envía email de prueba real verificado en Mailpit API.

### `11-admin-notifications.spec.ts` — Notificaciones Admin
- **E2E-32**: Verifica que las notificaciones envían correo real a admins, se muestran en el panel de `/admin-bo`, se abren en popup marcándose como `Leídas`, navegan `Siguiente`/`Anterior` y pasan a `Archivadas`.

### `12-admin-invitations.spec.ts` — Sistema de Invitaciones Admin
- **E2E-33**: Admin genera invitación (+7d por defecto), copia URL / WhatsApp y envía por email (verificado en Mailpit), renueva `+7d`, cancela, filtra y reactiva `7d`.

### `13-invitations-redeem.spec.ts` — Canje `/invitation-redeem/:code`
- **E2E-34**: Código expirado solicita renovación (notifica en `/admin-bo` y Mailpit) y vuelve al prompt de código; código válido registra en WillIAM, canjea y pasa a `/onboarding` dejando vínculo en `Canjeadas`.

### `14-house-joining-takeoff.spec.ts` — Decisión al Entrar por `/join/:token`
- **E2E-35**: Al llegar al enlace de invitación a una casa pregunta al usuario entre **Registrarme ahora**, **Introducir credenciales** o **Continuar como invitado**, completando las 3 vías hasta el Dashboard de la casa.

---

## 4. Ejecución en Docker / CI

La suite corre completamente contenedorizada usando la imagen oficial `mcr.microsoft.com/playwright` dentro de un perfil dedicado de Docker Compose:

```bash
# Ejemplo de ejecución objetivo cuando exista el código:
docker compose -f docker-compose.yml -f docker-compose.test.yml up --abort-on-container-exit --exit-code-from playwright
```

- **Artefactos generados en caso de fallo**: Traces de Playwright (`trace.zip`), capturas de pantalla y vídeos de la sesión móvil guardados en `tests/e2e-playwright/test-results/` para depuración inmediata.
