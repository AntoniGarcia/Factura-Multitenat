# Notas mixtas después de pagos

## Alcance

Se admiten notas sobre facturas básicas PPD en MXN con IVA mixto después de tener
pagos vigentes, dentro del importe y los impuestos que permanecen sin cobrar.
Continúan los límites de una factura de origen, receptor nominativo y ausencia
de retenciones, IEPS e impuestos locales en las notas.

El cálculo parte de la factura menos notas vigentes. Después descuenta las bases
e impuestos registrados en los pagos activos, agrupados por impuesto, factor y
tasa. La parte sin desglose se obtiene del abono que no cubren esas bases e IVA.
No se supone que una devolución puede usar el saldo de cualquier otro grupo.

Por ejemplo, tener saldo en conceptos exentos no permite devolver más IVA del
que continúa pendiente. El límite por concepto sigue evitando acreditar más de
lo facturado; el control adicional por grupo impide devolver lo ya cobrado.
Los pagos no identifican qué concepto individual de una misma tasa se cobró:
esa parte se controla por grupo, sin inventar una asignación a renglones.

Los pagos posteriores reparten el saldo fiscal pendiente. No vuelven a repartir
desde las bases completas de la factura ni modifican los pagos anteriores. El
servidor revalida tanto las notas como los pagos antes de apartar folio o timbre.
Se conservan el bloqueo por empresa y las comprobaciones de notas en proceso.

## Presentación y datos

La tabla de la nota identifica la columna como Base menos notas previas. No se
presenta ese importe como un saldo libre de pagos. Se muestra además el saldo
total disponible con IVA y el servidor comprueba el límite conjunto de los
renglones de cada grupo.

No hay tablas, columnas, migraciones ni contratos congelados nuevos. Se usan los
desgloses fiscales ya guardados de los pagos y las notas. La factura original y
los pagos timbrados conservan sus copias fiscales, importes y saldos históricos.

## Precisión e historial

Los desgloses de pago conservan seis decimales. Al restarlos se admite un margen
de una unidad del sexto decimal por operación de grupo para el redondeo; no se
agrega un ajuste en centavos ni un concepto ficticio. Un historial incompatible,
impuestos que no corresponden a su base o un grupo sin disponible se rechaza con
un mensaje de revisión de pagos, notas y redondeos.

Si un documento anterior no permite reconstruir el saldo fiscal, debe revisarse
antes de continuar; no se reescribe para forzar la emisión. La cancelación de una
nota sigue bloqueada mientras existan pagos activos. La sustitución de pagos
mantiene la comprobación de saldo e impuestos históricos de la fase anterior;
una nota posterior que cambie esos datos puede impedir la sustitución.

## Comprobaciones

- Compilación final: cero errores y cero advertencias.
- Regresión general: 317 pruebas correctas, dos omitidas y ninguna fallida.
- Arranque local: 31 catálogos preparados sin recarga y respuesta HTTP 200.

La integración terminó 302 comprobaciones con servicios reales, certificado
sintético y PAC simulado. Se incluyeron:

- Factura 416, primer pago 208 y nota posterior de base 25 más IVA 4: saldo 179.
- Segundo pago de 89.50: base IVA 16 % de 12.50 e impuesto 2; base de tasa cero
  25 y base exenta 25. El XML incluye ese desglose y el PDF lo muestra.
- Rechazo de una nota por base 60 del grupo gravado cuando solo quedan 50 sin
  cobrar, aunque exista saldo en otros grupos.
- Rechazo de un pago preparado antes de la nota; exige volver a guardar para
  actualizar saldo, parcialidad y desglose antes de emitir.
- Nota final de 89.50 que cierra exactamente el remanente después de dos pagos,
  sin duplicar impuestos cobrados.
- Conservación de los saldos anteriores 416 y 179 en los pagos ya emitidos.
- Cancelación ordenada de pagos y notas, recuperando saldo 416 sin borrar historial.
- Regresiones previas de sustitución, equivalencias, SPEI, aislamiento, impuestos
  mixtos y notas anteriores al primer pago.

La base temporal se eliminó al terminar. Se trabajó con compatibilidad SQL 120,
sin acreditar una ejecución en SQL Server 2014 nativo. Los XML se validaron contra
XSD y los PDF de nota y pago se renderizaron y revisaron visualmente. No se probó
aceptación ante un PAC real ni se completó revisión visual autenticada del
formulario en navegador.

## Pendientes de alcance

Egresos en otras monedas, varios orígenes, retenciones, IEPS, impuestos locales
y variantes especializadas de factura. La pasarela de cobro y el PAC real siguen
fuera de estas pruebas.
