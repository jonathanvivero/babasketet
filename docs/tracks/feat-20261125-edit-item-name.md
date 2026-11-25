# Track: `feat/20261125-edit-item-name` — Edición de Nombre y Long-Press Reducido

- **Estado**: Verificado
- **Rama**: `feat/20261125-edit-item-name` (desde `dev`)
- **Spec de referencia**: `docs/features/features-roadmap.md`, `docs/specs/domain-catalogs.md`, `docs/backend/babasketet-api.md`, `docs/frontend/babasketet-web.md`
- **Servicios afectados**: `Babasketet.API`, `Babasketet.WEB`, `e2e-playwright`

## Decisiones tomadas durante la ejecución
- Se añade `Name` opcional a `UpdateBasketItemRequest` (`PATCH /api/v1/houses/{houseId}/basket/items/{itemId}`), compartiendo `ResolveOrRenameProductAsync` con `UpdateListItemAsync` para renombrar el `Product` o reasignarlo de forma segura si el nombre normalizado ya existiera en la casa (evitando colisión en índice único `products(house_id, normalized_name)`).
- El tiempo de pulsación larga continua se reduce desde `2000 ms` hasta **`800 ms`** (reducción de `1,2 s`, cumpliendo "1 seg o más") tanto en `Lista` como en `Cesta`.
- En el popup de pulsación larga de `Lista` y `Cesta` se incluye un campo de texto pre-rellenado para editar el nombre del ítem junto al selector de tipo (y unidades/formato en `Cesta`).

## Incrementos (Vertical Slices)
- [x] Slice 1: Crear rama `feat/20261125-edit-item-name` desde `dev` y actualizar specs ODD
- [x] Slice 2: Backend `.NET 10` (`UpdateBasketItemRequest.Name`, helper compartido `ResolveOrRenameProductAsync`, notificaciones SignalR y test `IT_API_12`)
- [x] Slice 3: Frontend Flutter (`ItemLongPressDetector` 800 ms, campo de nombre en popups de `Lista` y `Cesta`, widget tests)
- [x] Slice 4: Tests Playwright E2E verificando long-press ~800 ms y edición de nombre en `Lista` y `Cesta`

## Verificación
- [x] `Babasketet.API.UnitTests` (17/17) y `Babasketet.API.IntegrationTests` (8/8) en verde
- [x] `flutter test` (4/4) en verde
- [x] `Playwright` E2E (7/7) en verde contra el stack Docker
