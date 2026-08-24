# Resumen ejecutivo

Documento de contexto para quien no programa: gerentes de producto, clientes o cualquier
persona que necesite entender qué es este sistema y para qué sirve, sin tecnicismos.

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

1. **Emitir facturas CFDI 4.0** con validación en tiempo real de los datos del cliente.
2. **Timbrar ante el SAT** a través de un proveedor autorizado (PAC), con el sello oficial.
3. **Cancelar comprobantes** con los motivos que exige el SAT (claves 01 a 04).
4. **Emitir el complemento de pagos** cuando la factura se cobra en parcialidades o a plazo.
5. **Generar el PDF** de la factura con su código QR y la cadena original, listo para enviar.
6. **Enviar la factura por correo** al cliente automáticamente.
7. **Administrar varias empresas** desde una sola cuenta, con usuarios y permisos por empresa.

## 3. Estado actual del desarrollo

- **Plataforma base (identidad, empresas, catálogos, clientes, productos, timbres):**
  terminada. El sistema ya gestiona cuentas, empresas emisoras, usuarios con permisos, y los
  catálogos oficiales del SAT.
- **Motor de facturación (emisión, impuestos, timbrado, PDF, cancelación, pagos):**
  implementado de extremo a extremo. Ya se puede capturar una factura, calcular sus impuestos,
  timbrarla contra el entorno de pruebas del PAC, generar su PDF y cancelarla.
- **Lo que sigue:** endurecimiento, pruebas de integración a fondo y validación contra el
  entorno real del SAT antes de la entrega del producto mínimo viable (MVP), previsto para el
  **5 de diciembre de 2026**.

## 4. ¿Qué se necesita para ejecutarlo?

Es una aplicación web. Para levantarla en una computadora de desarrollo se necesita el
entorno de .NET y una base de datos SQL Server; luego se ejecutan un par de comandos y la
aplicación queda corriendo en el navegador. Los pasos detallados, paso a paso, están en
[docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md). La base de datos se crea sola; no hay que
armarla a mano.

En producción vive en un servidor y los usuarios solo abren una dirección web, sin instalar
nada en sus equipos.

## 5. Impacto y beneficio esperado

- **Menos rechazos del SAT:** las validaciones evitan los errores que más comúnmente tumban
  un timbrado (nombre del receptor, régimen fiscal, uso del CFDI, cálculo de impuestos).
- **Un solo lugar para varias empresas:** un despacho o un grupo empresarial factura para
  todas sus empresas desde una cuenta, sin duplicar herramientas ni licencias.
- **Cero instalación y actualización centralizada:** al ser web, todos usan siempre la última
  versión y los catálogos del SAT vigentes.
- **Cumplimiento real:** al soportar el complemento de pagos, la empresa puede facturar a
  plazo sin quedar en falta con la autoridad.
