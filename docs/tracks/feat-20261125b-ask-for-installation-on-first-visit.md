# Track: `feat/20261125b-ask-for-installation-on-first-visit` — Prompt PWA, Icono Piña-Cesta y Cuenta en App Instalada

- **Estado**: Verificado
- **Rama**: `feat/20261125b-ask-for-installation-on-first-visit` (desde `dev`)
- **Spec de referencia**: `docs/frontend/babasketet-web.md`, `docs/features/features-roadmap.md`
- **Servicios afectados**: `Babasketet.WEB`, `e2e-playwright`

## Decisiones tomadas durante la ejecución
- **Prompt de instalación PWA en 1ª visita**: Se captura `beforeinstallprompt` desde `web/index.html` antes del arranque de Flutter y se muestra un banner no bloqueante (`PwaInstallBanner`) la primera vez que el usuario visita la PWA fuera del modo `standalone` (con botón directo para lanzar el diálogo nativo en Chromium/Android o guía clara de "Añadir a pantalla de inicio" en iOS/Safari, persistiendo que ya fue mostrado). Además queda accesible desde el menú lateral.
- **Icono y Favicon**: Ilustración de una piña entrando en una cesta de baloncesto con líneas negras sobre fondo naranja medio claro (`favicon.png`, `Icon-192.png`, `Icon-512.png`, `apple-touch-icon.png`).
- **Cuenta en App instalada (Standalone PWA)**:
  - Botón de cuenta siempre visible en el `AppBar` de `DashboardPage` (además del `Drawer` y `OnboardingPage`) para que la opción de crear cuenta / guardar sesión nunca quede oculta al entrar directo a una casa ya guardada.
  - Flujo OIDC PKCE adaptado para modo `standalone`: intenta abrir ventana popup (`window.open`) con retorno por `postMessage` en `/auth/callback` sin sacar a la PWA instalada de su ventana ni perder su `deviceToken` local (con fallback automático a `_self` si el navegador bloquea popups).

## Incrementos (Vertical Slices)
- [x] Slice 1: Generar e integrar el icono (piña entrando en cesta de baloncesto, líneas negras sobre fondo naranja medio claro) en `favicon.png`, `Icon-192.png`, `Icon-512.png` y `apple-touch-icon.png`
- [x] Slice 2: Adaptador aislado `pwa_install` + captura `beforeinstallprompt` en `index.html` + `PwaInstallBanner` en primera visita + botón en Drawer
- [x] Slice 3: Botón/Modal de cuenta visible en `DashboardPage` + flujo OIDC popup/`postMessage` seguro en modo `standalone`
- [x] Slice 4: Actualizar docs (`babasketet-web.md`, `features-roadmap.md`, `e2e-playwright.md`) y añadir tests unitarios/widget + Playwright E2E

## Verificación
- [x] `flutter test` en verde (`5/5` tests pasados dentro de `docker compose up -d --build web`)
- [x] `Playwright` E2E en verde (`10/10` specs pasadas: `01`..`08`, incluyendo `E2E-26`, `E2E-27` y `E2E-28` en modo `standalone`)
