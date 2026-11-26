# Especificaciones Funcionales y Roadmap de Features — Babasketet

Cada feature de este documento corresponde a un **Tier 3 (Feature Track)** en Organic-Driven Development y define los criterios de aceptación verificables (`Given / When / Then`) que alimentan tanto los tests de integración como la suite E2E de **Playwright**.

---

## F1: Gestión de Casas e Invitaciones

### Objetivo
Permitir crear una casa o unirse a una existente mediante enlace temporal (1 hora) o QR, con almacenamiento local del identificador único de casa y control estricto de límites (1 casa anónimo / hasta 4 casas registrado).

### Criterios de Aceptación
- **AC-F1.1 (Crear Casa)**:
  - *Given* un usuario que entra por primera vez sin casa local
  - *When* introduce un nombre de casa (ej. `"Casa Playa"`) y pulsa Crear
  - *Then* se crea la casa con un `houseId` único, se guarda localmente en la PWA y se navega al Dashboard con tienda activa `Default`.
- **AC-F1.2 (Compartir con TTL 1h)**:
  - *Given* un usuario dentro de una casa
  - *When* pulsa "Compartir casa"
  - *Then* la API genera una URL única válida exactamente **1 hora**, y la UI ofrece botones para enviarla por **WhatsApp**, **Correo** o mostrar su **código QR**.
- **AC-F1.3 (Unirse mediante Deep-Link)**:
  - *Given* un segundo dispositivo abre la URL de invitación dentro de la hora de vigencia
  - *When* carga la PWA
  - *Then* entra directamente al Dashboard de esa Casa sin pasos extra y guarda su `houseId` localmente.
- **AC-F1.4 (Enlace Expirado)**:
  - *Given* un enlace de invitación con más de 1 hora de antigüedad
  - *When* un usuario intenta abrirlo
  - *Then* la API responde `410 Gone` y la PWA informa que el enlace ha caducado.
- **AC-F1.5 (Límite 1 vs 4 casas)**:
  - *Given* un usuario no registrado que ya tiene 1 casa
  - *When* intenta crear o unirse a una 2.ª casa
  - *Then* el sistema bloquea la operación y le ofrece darse de alta en `WillIAM` para gestionar hasta 4 casas.
  - *Given* un usuario registrado con 4 casas
  - *When* cambia de casa desde el selector superior
  - *Then* el Dashboard carga la Lista, Cesta, Compras y Tienda activa de la casa elegida. Al intentar añadir una 5.ª casa, la API devuelve `409 Conflict` (`HOUSE_LIMIT_REACHED`).

---

## F2: Gestión de Identidades (`Zitadel` + `WillIAM.UI` + Passkeys + Disco Rotatorio)

### Objetivo
Permitir el uso anónimo sin fricción y ofrecer registro/login opcional mediante **9 métodos (incluyendo Passkeys WebAuthn/FIDO2)** sobre **Zitadel**, con un selector interactivo propio estilo **teléfono antiguo (disco rotatorio)**.

### Criterios de Aceptación
- **AC-F2.1 (Acceso sin registro)**:
  - *Given* un usuario sin cuenta en `Zitadel`
  - *Then* puede usar completamente `Lista`, `Cesta`, `Compras` y compartir su única casa.
- **AC-F2.2 (Selector Disco Rotatorio)**:
  - *Given* el usuario abre la pantalla de Login/Signup (`WillIAM.UI`)
  - *When* gira la rueda circular con los **9 iconos redondos** (Passkey, Email+Password, Email+MagicLink, Teléfono+MagicLink, Google, Meta, Apple, Microsoft, LinkedIn)
  - *Then* el método posicionado en el **"norte" (12 en punto)** queda seleccionado y habilita su flujo correspondiente contra Zitadel API v2.
- **AC-F2.3 (Passkeys — Registro y Login sin contraseña)**:
  - *Given* un usuario con un navegador/dispositivo compatible con WebAuthn
  - *When* selecciona **Passkey** en el norte del disco para registrarse o iniciar sesión
  - *Then* completa el reto WebAuthn/FIDO2 con Zitadel y recibe una sesión OIDC válida sin introducir contraseña.
- **AC-F2.4 (Migración de casa anónima al registrarse)**:
  - *Given* un usuario anónimo con 1 casa con productos
  - *When* se registra o inicia sesión mediante Zitadel
  - *Then* su casa local queda vinculada automáticamente a su cuenta (`POST /api/v1/houses/claim`) sin perder ningún dato, y su límite sube a 4 casas.
- **AC-F2.5 (Teléfono + Magic Link / OTP)**:
  - *Given* el usuario selecciona Teléfono + Magic Link en el norte del disco
  - *When* introduce prefijo de país (ej. `+34`) + número y envía
  - *Then* Zitadel envía el enlace/código temporal y al abrirlo autentica al usuario.

---

## F3: Gestión de Centro de Compra / Tienda

### Objetivo
Permitir asociar la compra y el histórico de precios a un supermercado concreto sin hacerlo obligatorio.

### Criterios de Aceptación
- **AC-F3.1 (Tienda por defecto)**:
  - *Given* una casa nueva
  - *Then* arriba a la derecha aparece el icono de tienda con `Default` seleccionado por defecto.
- **AC-F3.2 (Catálogo ES/PT)**:
  - *When* el usuario pulsa el icono de tienda arriba a la derecha
  - *Then* ve las **24 tiendas por defecto** definidas en `domain-catalogs.md` y un botón para agregar una nueva.
- **AC-F3.3 (Agregar tienda personalizada)**:
  - *When* el usuario añade `"Frutería Manolo"`
  - *Then* queda persistida para esa casa, aparece en el selector en todos los dispositivos conectados y puede elegirse como tienda activa.

---

## F4: Dashboard y Pestaña `Lista`

### Objetivo
Gestionar ágilmente los productos pendientes de comprar con autocompletado, reordenación, clasificación visual por tipos y gestos rápidos.

### Criterios de Aceptación
- **AC-F4.1 (Controles superiores y `[+]`)**:
  - *Given* el usuario está en la pestaña `Lista`
  - *Then* ve en la parte alta una caja de texto con 2 botones laterales (izquierda: seleccionar de items ya existentes; derecha: añadir el texto escrito) y un icono redondo `[+]` que al escribir sugiere elementos añadidos previamente.
- **AC-F4.2 (Defaults del ítem y color)**:
  - *When* añade `"Tomates"` sin especificar tipo, formato ni precio
  - *Then* se guarda con `Formato = Ud.`, `Precio = 0,00`, `Tipo = null` y se muestra en fuente **negra** sin círculo de color.
- **AC-F4.3 (Long-press 800 ms, edición de nombre y asignación de Tipo)**:
  - *When* el usuario mantiene pulsado un item durante **800 ms**
  - *Then* se abre el popup donde puede **editar el nombre del item**, elegir un Tipo en el desplegable (círculo con color de fondo y borde + nombre, los 18 por defecto + personalizados) o crear un tipo nuevo (nombre, color y borde).
  - *When* cambia el nombre o selecciona `Fruta/Verdura`
  - *Then* el nuevo nombre persiste y el item muestra a su izquierda el círculo verde claro sin borde.
- **AC-F4.4 (Reordenación)**:
  - *When* el usuario arrastra un item a otra posición
  - *Then* el nuevo orden persiste en BD y se refleja en los demás dispositivos.
- **AC-F4.5 (Swipes)**:
  - *When* desliza un item hacia la **derecha** → desaparece de `Lista` y aparece en `Cesta` (con `1 ud.`).
  - *When* desliza un item hacia la **izquierda** → se elimina de `Lista`.

---

## F5: Pestaña `Cesta` e Histórico de Precios (SCD-2)

### Objetivo
Simular el carrito físico en el supermercado: ajustar unidades por gesto vertical, consignar precios por supermercado con histórico temporal, visualizar el total y cerrar la compra.

### Criterios de Aceptación
- **AC-F5.1 (Visibilidad de precio por supermercado)**:
  - *Given* `"Leche"` tiene precio vigente `1,05 €` en `Mercadona` y ninguno en `Lidl`
  - *When* la tienda activa es `Mercadona` → en `Cesta` se muestra `1,05 €`.
  - *When* la tienda activa es `Lidl` → en `Cesta` no se muestra precio (hasta consignarlo).
- **AC-F5.2 (Unidades por gesto vertical)**:
  - *Given* un item en `Cesta` con `1 ud.`
  - *When* el usuario toca la unidad y desliza el dedo **hacia arriba** → aumenta las unidades (`2 ud.`, `3 ud.`...).
  - *When* desliza **hacia abajo** → decrementa las unidades (sin bajar de mínimo válido).
- **AC-F5.3 (Swipe izquierda en Cesta)**:
  - *When* el usuario desliza el producto hacia la **izquierda**
  - *Then* el producto vuelve a la pestaña `Lista`.
- **AC-F5.4 (Long-press 800 ms en Cesta)**:
  - *When* mantiene pulsado un producto durante **800 ms**
  - *Then* aparece popup con 5 acciones: **Cambiar nombre**, **Eliminar** (desaparece sin volver a Lista), **Cambiar ud.**, **Cambiar tipo**, **Cambiar formato** (`Ud`, `Docena`, `Kg`, `g`, `L`, `Cl`, `Pack-2`, `Pack-3`, `Pack-4`, `Pack-6`, `Pack-8`).
- **AC-F5.5 (Consignar precio y versionado SCD-2)**:
  - *When* toca el precio de un producto → aparece el teclado numérico del teléfono con el último precio (o `0,00`).
  - *When* guarda un precio distinto (ej. pasa de `1,05` a `1,15`) para `(Producto, Supermercado)`
  - *Then* en `product_price_history` el registro anterior queda con `valid_to = NOW()` y se inserta uno nuevo con `valid_from = NOW()` y `valid_to = NULL`. Solo existe un registro con `valid_to IS NULL`.
- **AC-F5.6 (Total y Efectuar Compra)**:
  - *Given* varios productos en `Cesta` con unidades y precios
  - *Then* se muestra el `Total` sumado correctamente (`SUM(precio * unidades)`).
  - *When* pulsa **"Efectuar Compra"** → pide confirmación → al confirmar, guarda la compra en `Compras` (con fecha, supermercado y snapshot de productos) y vacía la `Cesta`.

---

## F6: Pestaña `Compras` (Histórico)

### Objetivo
Consultar compras pasadas, revisar sus elementos, ajustar el total real y preparar la monetización futura del escaneo/adjunto de tickets.

### Criterios de Aceptación
- **AC-F6.1 (Listado y Detalle)**:
  - *Given* compras efectuadas previamente
  - *Then* se listan con **fecha**, **supermercado** y **total**, y al seleccionar una se muestran sus elementos comprados.
- **AC-F6.2 (Establecer total manual)**:
  - *When* el usuario edita el total de una compra histórica (ej. por descuentos en caja)
  - *Then* el nuevo total queda guardado y visible en el histórico.
- **AC-F6.3 (Ticket foto — Premium Locked)**:
  - *Given* un usuario en plan estándar (no Premium)
  - *When* ve la opción "Adjuntar foto de ticket"
  - *Then* aparece claramente bloqueada (`Locked — Solo versión Premium`) y el endpoint backend rechaza subidas sin suscripción Premium (`403 Forbidden`).

---

## F7: Sincronización Multi-Dispositivo (3+) y Modo PWA

### Objetivo
Garantizar que 3 o más dispositivos conectados a la misma casa vean cambios en tiempo real sin recargar la página.

### Criterios de Aceptación
- **AC-F7.1 (Propagación en tiempo real)**:
  - *Given* Dispositivo A, Dispositivo B y Dispositivo C abiertos en la misma casa
  - *When* Dispositivo A añade `"Pan"` en `Lista` y Dispositivo B lo mueve a `Cesta`
  - *Then* Dispositivo C refleja ambos cambios automáticamente (< 1 segundo en red estable) vía SignalR.
- **AC-F7.2 (Instalabilidad PWA, icono y aviso en 1ª visita)**:
  - *Given* un usuario visita la app web por primera vez fuera del modo `standalone`
  - *Then* ve un aviso invitándole a instalar la PWA como app en su dispositivo (con soporte de `beforeinstallprompt` o guía iOS) que no vuelve a aparecer tras descartarlo.
  - *Then* `Babasketet.WEB` cumple los criterios de instalabilidad PWA (`manifest.json` válido, Service Worker, favicon e iconos de la piña entrando en la cesta de baloncesto sobre fondo naranja).
- **AC-F7.3 (Acceso a cuenta persistente en App instalada)**:
  - *Given* el usuario abre la app instalada (`standalone`) directamente sobre una casa anónima
  - *Then* dispone de acceso visible en la barra superior (`account_btn`) y en el menú lateral para crear cuenta o iniciar sesión en `WillIAM` mediante flujo seguro sin perder su casa local.

---

## F8: Panel de Administración (`/admin-bo`) y Gobernanza de Roles

### Objetivo
Proporcionar un backoffice en `/admin-bo` exclusivo del rol `Admin` junto con el modelo de roles `Guest`, `User`, `Admin` y `Owner` por casa en `Zitadel`/`WillIAM` y `Babasketet.API`.

### Criterios de Aceptación
- **AC-F8.1 (Roles y Usuario Admin Inicial)**:
  - *Given* el proyecto `Babasketet` en Zitadel
  - *Then* define los roles `Guest` (global, solo no registrados), `User` (global, asignado al registrarse), `Admin` (global, inicial en `jonathanvivero@gmail.com`) y `Owner` (por casa, solo `User`, inicialmente 1 por casa).
  - *And* `jonathanvivero@gmail.com` nace con contraseña temporal `QWERTYUIOP-789456123!` y `changeRequired = true`, obligado a cambiarla en su primer acceso en `WillIAM.UI`.
- **AC-F8.2 (Acceso restringido a `/admin-bo`)**:
  - *Given* un usuario `Guest` o `User` sin `Admin`
  - *Then* no puede acceder a `/admin-bo` ni a `/api/v1/admin/*` (`403 Forbidden`).
- **AC-F8.3 (Casas, Usuarios e Ítems en `/admin-bo`)**:
  - *Given* un usuario `Admin` autenticado en `/admin-bo`
  - *Then* consulta casas y detalle de casa (usuarios, ítems totales, Lista, Cesta, compras con ítems), lista y edita usuarios (estado `Activo|Bloqueado`, rol, estado autenticado, propiedades en BD, casas), y consulta el listado alfabético y filtrable de ítems mostrando `"N casas"` cuando coinciden texto (case-insensitive) y tipo en varias casas.
- **AC-F8.4 (Configuraciones del sistema → Correo Electrónico Saliente)**:
  - *Given* un usuario `Admin` en `Configuraciones → Correo Electrónico Saliente`
  - *Then* puede configurar servidor SMTP, puerto, usuario, contraseña, cifrado y remitente, persistir la configuración en PostgreSQL y enviar un email de prueba desde la interfaz.
- **AC-F8.5 (Notificaciones en `/admin-bo`)**:
  - *Given* llega una nueva notificación
  - *Then* se envía por correo a todos los administradores, aparece paginada de 10 en 10 en el tercio derecho del dashboard filtrada por `Nuevas` (con filtros `Todas`, `Nuevas`, `Leídas`, `Archivadas`), y al abrirla en el popup se marca como `Leída` permitiendo navegar `Anterior`/`Siguiente`.
