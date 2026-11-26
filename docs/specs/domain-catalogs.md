# Catálogos de Dominio y Reglas Semilla — Babasketet

Este documento constituye la **fuente de verdad** para los datos iniciales (seeds) de `babasketet_db` y las reglas de negocio transversales.

---

## 1. Catálogo de Supermercados / Tiendas por Defecto (España y Portugal)

Siempre existe una tienda por defecto (`Default`), marcada inicialmente al crear una casa. El usuario puede seleccionar cualquier tienda de la lista o **agregar sus propios supermercados** (vinculados a su casa).

| # | Nombre | Marcado por defecto | Notas |
| :-: | :--- | :-: | :--- |
| 1 | **Default** | **Sí** | Tienda genérica cuando el usuario no especifica supermercado |
| 2 | Mercadona | No | España / Portugal |
| 3 | Carrefour | No | España |
| 4 | Dia | No | España |
| 5 | Lidl | No | España / Portugal |
| 6 | Aldi | No | España / Portugal |
| 7 | Tienda de Barrio | No | Comercio local |
| 8 | Spar | No | España / Portugal |
| 9 | Condis | No | España |
| 10 | El Jamón | No | España |
| 11 | Eroski | No | España |
| 12 | Consum | No | España |
| 13 | Caprabo | No | España |
| 14 | Costco | No | España |
| 15 | Supercor | No | España |
| 16 | Hipercor | No | España |
| 17 | Alcampo/Auchan | No | España / Portugal |
| 18 | Supeco | No | España |
| 19 | Intermarché | No | Portugal |
| 20 | Continente | No | Portugal |
| 21 | Coviran | No | España / Portugal |
| 22 | Pingo Doce | No | Portugal |
| 23 | Alsara | No | España |
| 24 | Comarket | No | España |

---

## 2. Catálogo de Tipos de Producto por Defecto (18 Tipos)

Cuando un elemento en la **Lista** no tiene tipo asignado, se muestra únicamente su nombre en **color de fuente negro**. Cuando tiene un tipo asignado, se muestra a la izquierda un **círculo** relleno con el color de fondo y, si aplica, con el color de borde especificado.

El usuario también puede **crear sus propios tipos** asignando nombre, color de fondo y borde (opcional).

| # | Nombre del Tipo | Color de Fondo (Spec) | Borde (Spec) | Hex Fondo Sugerido | Hex Borde Sugerido |
| :-: | :--- | :--- | :--- | :---: | :---: |
| 1 | **Fruta/Verdura** | Verde Claro | Sin borde | `#86EFAC` | `null` |
| 2 | **Carne** | Rojo | Sin borde | `#EF4444` | `null` |
| 3 | **Charcuteria/Queso** | Rosa | Sin borde | `#F472B6` | `null` |
| 4 | **Pescado** | Azul Claro | Sin borde | `#7DD3FC` | `null` |
| 5 | **Baño** | Azul | Sin borde | `#2563EB` | `null` |
| 6 | **Hogar/Limpieza** | Verde Oscuro | Sin borde | `#166534` | `null` |
| 7 | **Conservas/Sopas/Cremas** | Naranja claro | Sin borde | `#FDBA74` | `null` |
| 8 | **Aceite/Salsas/Especias** | Oliva | Borde negro | `#808000` | `#000000` |
| 9 | **Agua/Refrescos** | Azul claro | Borde marrón | `#7DD3FC` | `#78350F` |
| 10 | **Arroz/Legumbres/Pasta** | Marrón oscuro | Sin borde | `#5C3A21` | `null` |
| 11 | **Dulces/Azucarados/Postres** | Marrón claro | Borde marrón oscuro | `#C4A484` | `#5C3A21` |
| 12 | **Lácteos** | Blanco | Borde rosa fucsia | `#FFFFFF` | `#FF00FF` |
| 13 | **Alcohol** | Amarillo | Borde marrón claro | `#FACC15` | `#C4A484` |
| 14 | **Desayuno** | Marrón claro | Borde azul | `#C4A484` | `#2563EB` |
| 15 | **Congelado** | Azul oscuro | Borde azul claro | `#1E3A8A` | `#7DD3FC` |
| 16 | **Droguería** | Morado claro | Sin borde | `#C084FC` | `null` |
| 17 | **Mascotas** | Beige | Borde negro | `#F5F5DC` | `#000000` |
| 18 | **Panadería** | Marrón claro | Sin borde | `#C4A484` | `null` |

> **Nota visual**: El grosor de borde recomendado en el círculo indicador es `2.0 dp` (para que sea nítido en pantallas móviles de alta densidad, especialmente en `Lácteos` blanco con borde fucsia y `Mascotas` beige con borde negro).

---

## 3. Catálogo de Formatos de Producto (11 Formatos)

Valor por defecto al crear cualquier item: **`Ud`** (`Ud.`).

| Código | Etiqueta UI |
| :--- | :--- |
| `Ud` | `Ud.` (Por defecto) |
| `Docena` | `Docena` |
| `Kg` | `Kg` |
| `g` | `g` |
| `L` | `L` |
| `Cl` | `Cl` |
| `Pack-2` | `Pack-2` |
| `Pack-3` | `Pack-3` |
| `Pack-4` | `Pack-4` |
| `Pack-6` | `Pack-6` |
| `Pack-8` | `Pack-8` |

---

## 4. Constantes y Reglas de Negocio Transversales

| Regla | Valor / Comportamiento |
| :--- | :--- |
| **Casas máximas (usuario anónimo)** | `1` casa |
| **Casas máximas (usuario registrado en WillIAM)** | `4` casas |
| **Duración de enlace de invitación** | `1 hora` (`3600 segundos`) desde su creación |
| **Canales de compartición de Casa** | Enlace URL directo, WhatsApp, Correo electrónico, Código QR |
| **Campos obligatorios de un ítem** | Solo `Nombre`. Valores por defecto: `Tipo = null`, `Formato = Ud.`, `Precio = 0,00` |
| **Duración de Long-Press para popup** | **800 ms** (reducido desde > 2 s en ≥ 1 s) tanto en `Lista` como en `Cesta` |
| **Unidades por defecto en `Cesta`** | `1 ud.` |
| **Histórico de precios** | Clave temporal por `(Producto, Supermercado)` con `FechaAlta` y `FechaBaja`. Exactamente un registro vigente (`FechaBaja IS NULL`) por par `(Producto, Supermercado)` |
| **Foto del ticket en `Compras`** | **Bloqueado (Locked)** — reservado exclusivamente para versión Premium |
