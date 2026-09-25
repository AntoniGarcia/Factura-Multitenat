# Estado actual del sistema de facturación

**Corte documental: 25 de septiembre de 2026.** Este archivo sustituye el reporte del
24 de agosto, que describía la mitad B como un módulo vacío. Para comprobar el estado de
un entorno concreto, se deben revisar su versión desplegada, sus migraciones y sus pruebas:
un cambio en `develop` no demuestra por sí solo que Azure ya lo esté ejecutando.

## Alcance y estado verificable

El repositorio contiene una aplicación CFDI 4.0 multiempresa con cliente Blazor
WebAssembly, API ASP.NET Core y SQL Server. Están implementados la identidad, las
empresas emisoras, los usuarios y permisos, los catálogos SAT, los clientes, los
productos, las series y folios, y la bolsa de timbres. El panel del operador permite
gestionar cuentas, paquetes y compras; las compras pueden quedar pendientes y un
operador puede acreditar el pago manualmente.

La mitad de Documentos ya no está vacía. Tiene captura de borradores, cálculo de
impuestos, generación de salidas, flujo de timbrado, cancelación y complemento de pagos.
También hay pantallas y servicios para Carta Porte, Comercio Exterior, Notaría y
Constructoras. Estos módulos se agregaron después del reparto original: que el código
exista no equivale a haber validado todos sus casos fiscales de punta a punta.

La integración con un PAC está preparada en el código, pero **no se ha acreditado aquí
un timbrado real ni una cancelación real con las credenciales del ingeniero**. Para
revisión sin PAC se utiliza `Staging` con `Pac__Modo=Deshabilitado`: se pueden trabajar
catálogos y borradores, pero la emisión, los complementos y las llamadas al PAC quedan
bloqueados. `Production` no admite ese modo. No debe presentarse un PDF o XML preliminar
como CFDI timbrado.

La pasarela de pagos y el carrusel de pagos siguen fuera de esta fase, por decisión del
proyecto. Una solicitud de compra de timbres no acredita saldo automáticamente: la
acreditación manual es la operación vigente hasta que se decida e implemente una
integración de pago.

## Evidencia de la última entrega de código

Al cierre previo a este documento, `develop` estaba sincronizada con `origin/develop`.
Los últimos cambios publicados fueron `46305d6`, `8796b99`, `f7e64f4` y `ebadbca`:
impuestos y salidas de obra; Carta Porte; reimportación de productos y tasas; avisos
de interfaz. La compilación Release pasó sin errores ni advertencias nuevas; la suite
registró 115 pruebas aprobadas y una omitida. Esa verificación es de código local:
**no confirma** migraciones aplicadas, configuración ni funcionamiento de Azure.

## Pendientes antes de llamar al sistema completo

1. Validar manualmente, con datos representativos y por empresa, los flujos completos
   de factura estándar, Carta Porte, Comercio Exterior, Notaría y Constructoras;
   verificar borrador, impuestos, XML/PDF y errores de validación.
2. Revisar en Azure la versión realmente desplegada, las variables obligatorias,
   el almacenamiento persistente, las migraciones y las versiones de catálogos SAT.
   Seguir [la guía de despliegue](docs/DESPLIEGUE.md); no asumir que un `push` aplica
   migraciones de base de datos.
3. Retomar la auditoría de aislamiento entre inquilinos y sus correcciones pendientes
   que se pospusieron expresamente. No declarar cerrada la seguridad multiempresa
   basándose solo en los filtros o pruebas existentes.
4. Cuando el ingeniero proporcione las credenciales del PAC, configurar `Pac__Modo=Real`
   y probar timbrado, consulta, cancelación y recuperación de fallos en un entorno
   autorizado antes de pasar a producción.
5. Decidir la pasarela de pagos y diseñar la acreditación automática por separado.
   No simular un cobro ni liberar timbres por pulsar «comprar».

El objetivo de entrega indicado en [AGENTS.md](AGENTS.md) es el **5 de diciembre de
2026**. Los planes originales de [PROMPT-FASES-A.md](PROMPT-FASES-A.md),
[PROMPT-FASES-B.md](PROMPT-FASES-B.md) y [REPARTO-EQUIPO.md](REPARTO-EQUIPO.md) son
referencias históricas de alcance y secuencia, no un indicador del progreso actual.

## Para continuar

- [README.md](README.md): entrada al repositorio y mapa de documentación.
- [docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md): preparación del entorno local.
- [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md): configuración y comprobaciones del servidor.
- [docs/UI_Funcional.md](docs/UI_Funcional.md): inventario de campos del sistema anterior,
  no guía visual ni garantía de vigencia fiscal.
- [AGENTS.md](AGENTS.md) y [ARQUITECTURA.md](ARQUITECTURA.md): reglas del proyecto.

Actualizar este reporte después de cada fase que cambie el estado o sus pendientes.
