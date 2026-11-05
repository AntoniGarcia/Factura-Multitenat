# Pagos de facturas con objetos de impuesto mixtos

Fecha: 4 de noviembre de 2026.

## Criterio conservado

Se conserva la clasificación existente del sistema: si todos los conceptos tienen
la misma clave, el documento pagado conserva esa clave. En una mezcla se utiliza
`02` cuando existe desglose federal y `01` cuando no existe. No se presenta esta
clasificación agregada como una nueva prioridad de claves prescrita por el SAT.

El desglose se toma de las copias fiscales de los conceptos de la factura, nunca
del catálogo actual de productos. Se agrupa por impuesto, factor, tasa y sentido,
y se aplica la proporción entre el abono y el total de la factura. Los conceptos
sin desglose no agregan bases a los grupos de los demás conceptos. Se conservan
la tasa cero, el exento, las retenciones y el IEPS según sus grupos originales.

Las restricciones de cada objeto se contrastaron con el
[Anexo 29 de la RMF 2026, VI.1.8 y VI.2.1.1](https://www.sat.gob.mx/minisitio/NormatividadRMFyRGCE/documentos2026/rmf/anexos/Anexo_29_RMF2026-09012026.pdf)
y con la matriz de Pagos 2.0 enlazada por el
[portal del SAT](https://www.sat.gob.mx/portal/public/tramites/complemento-recepcion-de-pagos).
Las reglas de presencia y ausencia del desglose no definen por sí solas una
prioridad entre claves distintas dentro de una factura.

## Correcciones

Se centralizó la preparación del desglose en `DesgloseDeFacturaParaPago`.
La consulta de saldo y el guardado comprueban los conceptos antes de agrupar:
una mezcla con objeto agregado `02` ya no puede ocultar, por ejemplo, un concepto
`06` con IVA desglosado. También se rechazan mezclas con `05`, claves desconocidas
y datos de impuestos incompletos. Un concepto totalmente descontado puede
conservar su base cero en la factura sin aportar base al impuesto del pago.

Antes de emitir se comparan el objeto, la moneda y cada grupo guardado con el
desglose calculado desde la factura y el abono. Un importe internamente coherente
con su base y tasa todavía puede ser incorrecto para ese pago; ahora se detecta
antes de reservar folio o timbre. Guardar nuevamente reconstruye el borrador.
La comparación no cambia la factura original ni los pagos ya emitidos.

En el XML se rechazan bases de ImpuestosDR no positivas. Los exentos siguen sin
importe ni tasa. El resumen del PDF muestra una raya para el importe exento,
en lugar de confundirlo visualmente con un impuesto de tasa cero.

## Datos y alcance

Esta fase no incorpora columnas, migraciones ni contratos compartidos nuevos.
No se aplicó ningún cambio al esquema ni a los datos de la base principal.
La aplicación debe actualizarse con los archivos compilados del servidor y
cliente juntos, como en las otras fases.

El reparto conserva el criterio proporcional y el redondeo existente de seis
decimales. No añade una asignación manual del abono a conceptos individuales ni
acredita la procedencia de un estímulo fiscal. Tampoco equivale a validar toda
la matriz fiscal o a obtener aceptación de un PAC real.

## Comprobaciones

- Compilación final: cero errores y cero advertencias.
- Regresión: 300 pruebas aprobadas, dos omitidas y ninguna fallida. Incluye 15
  casos nuevos de clasificación, reparto, descuento total y detección de cambios.
- Integración: 101 comprobaciones con servicios reales y SQL local en
  compatibilidad 120. Comprueba mezclas con IVA, ISR, IEPS, tasa cero y exento;
  guardado, rechazo de conceptos incompatibles, XML firmado validado contra XSD,
  aislamiento entre empresas y rechazo de un desglose alterado antes de abrir un
  intento o asignar folio. Incluye las regresiones de equivalencias, sustitución
  y SPEI. La base temporal se eliminó al terminar.
- PDF final renderizado y revisado visualmente. El ajuste distingue el importe
  ausente de exentos del importe cero de una tasa cero.
- No se contactó un PAC real ni se modificaron documentos emitidos de la base
  principal. No se realizó una revisión autenticada del formulario en navegador.
