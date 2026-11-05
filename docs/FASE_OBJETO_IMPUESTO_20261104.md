# Fase 1: objeto de impuesto

Revisión: 4 de noviembre de 2026.

## Referencia fiscal

Se usaron el [Anexo 29 de la RMF 2026, VI.1.8 y VI.2.1.1](https://www.sat.gob.mx/minisitio/NormatividadRMFyRGCE/documentos2026/rmf/anexos/Anexo_29_RMF2026-09012026.pdf)
y las [preguntas del SAT sobre el Anexo 20](https://wwwmat.sat.gob.mx/consultas/35025/formato-de-factura-electronica-%28anexo-20%29).

Las reglas de desglose incorporadas son:

- `01`, `03`, `04`, `05`: sin impuestos federales desglosados.
- `02`: al menos un impuesto.
- `06`, `08`: retención de ISR opcional; no IVA ni IEPS desglosados.
- `07`: IEPS trasladado obligatorio; permite retención de IEPS e ISR y excluye IVA.
- Los conceptos `05` se facturan separados de otros objetos de impuesto.

Estas reglas no acreditan por sí solas que el contribuyente pueda aplicar un
estímulo fiscal: seleccionar una clave no sustituye sus requisitos legales.

## Cambios realizados

Se agregó una regla común en `Facturacion.Shared/Comun`, fuera de los contratos
congelados. El servidor la aplica al validar productos, calcular conceptos,
preparar XML y revisar documentos antes de reservar folio y timbres.

En el editor de productos se muestran impuestos compatibles, se retiran los
incompatibles al cambiar el objeto y las tasas predeterminadas respetan esa
selección. Los atajos de IVA solo aparecen cuando corresponden. La importación
CSV deja de exigir IVA a objetos que no lo admiten; al reimportar conserva
únicamente los impuestos adicionales compatibles.

En factura, las retenciones disponibles respetan la clave del producto. En pagos,
la validación admite los desgloses especiales del Anexo 29 y se aplica también
antes de guardar. Si todos los conceptos de la factura tienen el mismo objeto,
el recibo conserva esa clave en `ObjetoImpDR`, incluso cuando no haya desglose.

No se modificó la clasificación agregada existente de facturas con distintos
objetos: sigue usando `02` si hay desglose y `01` si no lo hay. Su captura y criterio
para combinaciones especiales quedan expresamente pendientes de la fase de pagos;
no se presenta esta fase como validación de todos los casos mixtos.

Los documentos emitidos conservan sus copias fiscales y el XML guardado. No se
reescribieron comprobantes ni productos existentes en la base principal.

## Verificación

- Compilación del servidor y cliente: sin errores ni advertencias.
- Suite final: 241 pruebas aprobadas y 2 omitidas. Las omitidas son la instalación
  especial del operador y una previsualización manual de PDF.
- 39 comprobaciones adicionales con SQL real, motor fiscal y esquemas XSD locales
  del SAT, cubriendo las ocho claves en factura y pago y rechazos de IVA indebido.
- Se creó una base temporal exclusiva y se eliminó al finalizar. De `FacturacionDev`
  solo se leyeron los catálogos para esas comprobaciones.
- Las ocho claves ya existen en el catálogo de la base principal y en el XSD local.
- Arranque local en `http://localhost:5184`: respuesta HTTP 200 con el app shell.
  El arranque encontró 28 catálogos preparados y no cargó ninguno nuevo.

Los XML de integración son documentos sintéticos de prueba para comprobar
estructura y reglas; no representan certificación o aceptación por un PAC real.
No se hizo una revisión visual autenticada de los nuevos controles en esta fase.

Evidencia local, excluida de Git: `tmp/objeto-impuesto-20261104/`. Contiene el
registro de integración, resultado TRX, registro de pruebas y registro de arranque.

## Base de datos y continuación

No se agregaron tablas, columnas ni migraciones. No se cambiaron firmas o DTO de
`Facturacion.Shared/Contratos/`.

La siguiente fase propuesta es cerrar los cruces de validación de Comercio
Exterior 2.0. Carta Porte, monedas distintas en pagos y demás escenarios del
diagnóstico continúan pendientes. No se implementó PAC ni pasarela de pagos.
