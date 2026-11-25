# Módulo de Catálogo de Productos y Servicios

Cada instalación tiene un único catálogo perteneciente a la empresa propietaria
de la intranet. Los clientes comerciales (`Client`) no son propietarios de
catálogos. No se comparte catálogo ni base de datos entre instalaciones.

## Gestión

`/catalog` abre directamente categorías, productos y servicios, sin selector de
clientes ni rutas por `ClientId`. La gestión mantiene el acceso exclusivo del rol
`Admin` existente; no se amplían permisos. `ICatalogService`, las entidades y los
DTOs ya no reciben ni exponen un cliente propietario.

Las categorías mantienen su jerarquía y la protección contra ciclos. Los productos
y servicios conservan nombre, descripción, precio, moneda, categoría y estado activo.
Los artículos utilizados en pedidos no pueden eliminarse; se pueden desactivar para
preservar el historial. Los pedidos solo admiten artículos activos del catálogo único,
y conservan su aislamiento por cliente comercial y sus snapshots de nombre/precio.

## Campos genéricos opcionales

| Campo | Productos | Servicios | Límite / significado |
|---|:---:|:---:|---|
| Unidad de venta/facturación | Sí | Sí | 50 caracteres; unidad, caja, kg, hora, proyecto, etc. |
| Impuesto (%) | Sí | Sí | 0–100, máximo 2 decimales; nulo = sin definir, 0 = tasa cero |
| Proveedor/prestador | Sí | Sí | 200 caracteres |
| SKU / referencia | Sí | No | SKU existente de 64 caracteres |
| Marca / fabricante | Sí | No | 100 caracteres |
| Plazo de entrega (días) | Sí | No | Entero no negativo; nulo = desconocido |
| Referencia interna | No | Sí | 64 caracteres |
| Modalidad de prestación | No | Sí | 100 caracteres; presencial, remota, etc. |
| Periodicidad de facturación | No | Sí | 100 caracteres; mensual, por entrega, etc. |
| Duración (minutos) / recurrencia | No | Sí | Campos existentes; duración no negativa |

Los datos fiscales, unidades, plazos y periodicidad son informativos: no implementan
inventario, cálculo de impuestos, facturación automática ni cambios en los totales de
pedidos. Los textos opcionales se normalizan y se pueden borrar; los nuevos campos
rechazan valores demasiado largos en vez de truncarlos. Las actualizaciones se validan
completamente antes de modificar entidades seguidas por EF.

## Migración y aislamiento

`SingleInstallationCatalog` elimina las columnas/FKs `ClientId` únicamente de
`CatalogCategories` y `CatalogItems`, actualiza sus índices y añade campos opcionales.
Los nombres técnicos y las migraciones anteriores se conservan.

La conversión se ha acordado para una plantilla sin datos: tanto `Up` como `Down`
comprueban bajo bloqueo transaccional que ambas tablas de catálogo estén vacías.
Si contienen datos, fallan explícitamente, sin borrarlos ni unificar catálogos de
forma automática. Una instalación con datos requiere una migración de datos específica
y copia de seguridad; para revertir tras empezar a utilizar el catálogo, también.
No ejecutar el script sin transacción ni usarlo sobre otra instalación.

En desarrollo se aplica mediante el arranque habitual; en producción debe aplicarse
mediante el procedimiento controlado del runbook. No se han modificado los ámbitos
ni permisos de usuarios, mensajería o pedidos.

## Extensibilidad

`Product` y `Service` heredan de `CatalogItem` y comparten `CatalogItems` (TPH,
discriminador `ItemType`). Se pueden añadir atributos específicos del sector en cada
instalación. `Price` mantiene importe y moneda ISO 4217. La validación del árbol de
categorías corresponde a `CatalogService`.
