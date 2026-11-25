# Módulo de pedidos (Orders)

Vista genérica de plantilla para la gestión de pedidos/órdenes. Los clientes
comerciales ordenan artículos activos del catálogo único de la instalación
(`MemByte.Domain.Catalog`). Los pedidos mantienen su aislamiento por `ClientId`:
compartir artículos no permite acceder ni modificar pedidos de otro cliente.

El módulo administra alta, líneas, confirmación, progreso, cierre, cancelación y
estado de pago. Su modelo (`Order`, `OrderLine`, `OrderStatus`, `OrderPaymentStatus`)
permanece mínimo y extensible para que cada empresa adapte su flujo de pedidos.
Los nuevos campos comerciales del catálogo son informativos: no modifican el
cálculo de totales ni implementan facturación o inventario.

- `OrderStatus`: flujo mínimo (Draft → Confirmed → InProgress → Completed,
  con Cancelled como salida anómala). Ampliable con más estados intermedios.
- `OrderPaymentStatus`: Pending/Paid. Ampliable a pagos parciales o
  reembolsos.
- `OrderLine`: referencia un artículo del catálogo (`CatalogItemId`) y
  conserva una "foto" (`ItemNameSnapshot`/`UnitPriceSnapshot`) del nombre y
  precio en el momento de añadirse a la línea, para que el histórico del
  pedido no cambie si el catálogo se actualiza después.
