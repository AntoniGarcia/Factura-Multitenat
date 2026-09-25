# Resumen ejecutivo

Documento de contexto para quien no programa: gerentes de producto, clientes o cualquier
persona que necesite entender qué es este sistema y para qué sirve, sin tecnicismos.

**Estado al 25 de septiembre de 2026:** sistema en desarrollo y revisión. Para el detalle
técnico y los pendientes, ver [REPORTE_ESTADO_SISTEMA.md](REPORTE_ESTADO_SISTEMA.md).

---

## 1. ¿Qué problema resuelve?

En México, toda empresa que vende un producto o presta un servicio está obligada a emitir
una **factura electrónica (CFDI 4.0)** validada por el SAT. Hacerlo mal —un dato del cliente
que no coincide con su constancia fiscal, un impuesto mal calculado, un folio repetido—
significa que la factura se rechaza o, peor, que la empresa queda en incumplimiento.

Muchos negocios resuelven esto con programas de escritorio antiguos, instalados máquina por
máquina, difíciles de actualizar y pensados para una sola empresa. Un despacho contable que
lleva la facturación de varias empresas termina con un programa distinto por cliente.

Este sistema ataca ese dolor: es una **plataforma web única, multiempresa**, donde una misma
cuenta administra la facturación de varias empresas emisoras, cada una con sus propios
clientes, productos y folios completamente aislados entre sí. Se usa desde el navegador, sin
instalar nada, y se actualiza para todos a la vez.

## 2. ¿Qué puede hacer un usuario?

El sistema ya permite administrar varias empresas desde una cuenta, con usuarios,
clientes, productos, catálogos, borradores de facturas y paquetes de timbres. Incluye
pantallas y servicios para factura estándar, Carta Porte, Comercio Exterior, Notaría
y Constructoras. Los flujos de timbrado, cancelación, complemento de pagos, PDF y
correo tienen implementación en el código, pero todavía requieren validación integral
con el PAC y en el entorno donde se entregarán.

En el entorno de revisión sin PAC no se emiten CFDI oficiales. Las compras de timbres
permanecen pendientes hasta que el operador acredite el pago; aún no hay pasarela de
pago automático.

## 3. Estado actual del desarrollo

- **Plataforma base:** identidad, empresas, catálogos, clientes, productos, permisos,
  compras y bolsa de timbres implementados.
- **Documentos:** captura, cálculos y flujos fiscales implementados en código, con
  módulos especiales añadidos después del plan original. No se debe confundir esa
  implementación con una homologación o prueba fiscal completa.
- **Lo que sigue:** pruebas manuales de extremo a extremo por tipo de documento,
  verificación del despliegue y de las migraciones, correcciones de aislamiento entre
  empresas que se dejaron pendientes y conexión/pruebas del PAC por el ingeniero.
  El objetivo de entrega sigue siendo el **5 de diciembre de 2026**.

## 4. ¿Qué se necesita para ejecutarlo?

Es una aplicación web. Para levantarla en una computadora de desarrollo se necesita el
entorno de .NET y una base de datos SQL Server; luego se ejecutan un par de comandos y la
aplicación queda corriendo en el navegador. Los pasos detallados, paso a paso, están en
[docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md). La base de datos se crea sola; no hay que
armarla a mano.

Para revisión en servidor se usa un entorno `Staging` sin PAC. Antes de un uso real
hay que configurar el PAC y superar las verificaciones de
[despliegue](docs/DESPLIEGUE.md). Los usuarios acceden desde el navegador.

## 5. Impacto y beneficio esperado

- **Objetivo: menos rechazos del SAT:** las validaciones buscan detectar antes del timbrado
  errores de receptor, régimen, uso del CFDI e impuestos. Falta comprobarlo con el PAC.
- **Un solo lugar para varias empresas:** un despacho o un grupo empresarial factura para
  todas sus empresas desde una cuenta, sin duplicar herramientas ni licencias.
- **Actualización centralizada:** al ser web, no requiere instalar el programa en cada
  equipo; publicar código y mantener los catálogos SAT vigentes son tareas del operador.
- **Documentos de pago:** el complemento está implementado en código; su funcionamiento
  fiscal real sigue sujeto a las pruebas con el PAC.
