# Track: [F1–F7] Frontend `Babasketet.WEB` (Flutter Web PWA)

- **Estado**: Verificado
- **Spec de referencia**: `docs/frontend/babasketet-web.md`, `docs/specs/domain-catalogs.md`, `docs/features/features-roadmap.md`
- **Servicios afectados**: `Babasketet.WEB`

## Decisiones tomadas durante la ejecución
- Cero dependencias de `dart:html` en lógica/widgets para mantener compatibilidad directa con Android e iOS.
- Cliente SignalR ligero sobre `web_socket_channel` (protocolo JSON v1 de ASP.NET Core SignalR) para evitar plugins heredados de `dart:html` y asegurar compatibilidad Wasm/CanvasKit y móvil.
- `SemanticsBinding.instance.ensureSemantics()` activado en el arranque para garantizar accesibilidad y selectores estables `flt-semantics-identifier` en Playwright.
- Long-press de **> 2 segundos (2000 ms)** implementado con `Listener` + `Timer(Duration(seconds: 2))` e indicador visual de progreso, tanto en `Lista` como en `Cesta`.

## Incrementos (Vertical Slices)
- [x] Slice 1: `pubspec.yaml`, `web/manifest.json`, `web/index.html`, `nginx.conf`, `Dockerfile` multi-stage
- [x] Slice 2: Capa `core/` (config, storage local, API client Dio, SignalR WebSocket client, OIDC PKCE + Zitadel)
- [x] Slice 3: Onboarding Casas (Crear / Unirse por token `/join/:token`), Compartir (WhatsApp, Correo, QR) y Selector 1..4 casas
- [x] Slice 4: Selector Disco Rotatorio en `WillIAM.UI` + conexión OIDC PKCE con `Zitadel` + `/houses/claim`
- [x] Slice 5: Dashboard (TopBar + Tiendas 24 ES/PT + agregar tienda) y 3 pestañas (`Lista`, `Cesta`, `Compras`) con todos los gestos y formatos
- [x] Slice 6: Widget Tests (`flutter test`) y build PWA en Docker

## Verificación
- [x] `flutter test` en verde dentro de Docker (`ghcr.io/cirruslabs/flutter:3.35.5`)
- [x] `docker compose build web` OK y PWA servida en Nginx
- [x] Suite E2E Playwright (7/7 escenarios) en verde contra el stack completo
