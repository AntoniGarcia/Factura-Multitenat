# Sustitución de pagos con notas de crédito

## Alcance

Se habilitó la sustitución del último pago de facturas PPD en MXN que tienen notas
de crédito, siempre que el saldo anterior y el desglose fiscal del pago original
sigan coincidiendo con los datos vigentes. Puede tratarse de IVA uniforme o de
un remanente de conceptos mixtos admitido por las fases anteriores.

La sustitución conserva las mismas facturas, receptor y número de parcialidad.
Se mantiene la relación 04; después de emitir el reemplazo, el pago anterior se
cancela con motivo 01 y el UUID del sustituto. Mientras ambos estén activos se
bloquean otros abonos y notas, como en la sustitución sin egresos.

No hay una pantalla nueva. En Nuevo pago → General se captura el folio fiscal
del pago anterior y se pulsa Cargar pago anterior. Después se revisan los importes,
se guarda y se emite el reemplazo mediante la integración configurada.

## Cálculo y controles

El saldo recuperado es el total de la factura menos notas vigentes y otros pagos
activos. Se excluyen el pago sustituido y el borrador actual. La nota se resta una
vez; el abono del documento anterior tampoco se conserva como otro cobro después
de su cancelación.

Antes de preparar y emitir se comprueba que el saldo recuperado sea igual al
saldo anterior guardado en el pago original. También se calcula el desglose que
correspondía al abono original y se compara con objeto de impuesto, bases,
factores, tasas e importes guardados. Coincidir en el total no basta.

La verificación contempla notas vigentes y el historial de notas canceladas.
Si una corrección posterior cambió el saldo o su composición fiscal, se rechaza
la sustitución. No se reescribe el pago original para que pase la validación.

Se conservan los controles de último pago, rechazo de sustitutos duplicados,
secuencia de cancelación e impedimento de registrar pagos mientras una nota
relacionada está emitiéndose o cancelándose. La validación al reservar se ejecuta
bajo el bloqueo de documentos por empresa; la comunicación con el PAC queda
fuera de la transacción SQL.

## Ejemplos verificados

- Factura 416 menos nota 316: el saldo anterior es 100. Sustituir el pago de 100
  por 90 deja 10; una segunda sustitución por 85 deja 15, sin acumular los abonos
  anteriores. La parcialidad no aumenta por sustituir.
- Factura 116 menos nota 29: el saldo anterior es 87. Sustituir el abono de 58 por
  50 deja 37, manteniendo el desglose de IVA que corresponde al nuevo abono.
- Una nota uniforme posterior altera ese saldo y bloquea otra sustitución.
- Dos notas distintas de total 316 pueden dejar saldo 100, pero una deja solo
  no objeto y la otra deja IVA y no objeto. Se rechaza sustituir usando el
  desglose del pago anterior, aunque los totales coincidan.
- Si se cancelan las notas que originaban un saldo anterior reducido, también
  se detecta el cambio al intentar sustituir un pago ya cancelado.

La integración terminó 268 comprobaciones con servicios reales, certificado
sintético y PAC simulado. Incluyó las regresiones previas de notas, pagos mixtos,
equivalencias, SPEI, sustitución y aislamiento por empresa. Se validaron XML
firmados contra XSD y se renderizó y revisó el PDF con relación 04. La base temporal
en compatibilidad 120 se eliminó al terminar. Esto no acredita aceptación ante
un PAC real ni una ejecución en SQL Server 2014 nativo.

La regresión general terminó con 313 pruebas correctas, dos omitidas y ninguna
fallida. La compilación de servidor y cliente pasó sin errores ni advertencias.

## Datos y límites

No se agregaron tablas, columnas, migraciones, permisos, DTO ni contratos. Se
amplió una consulta interna para excluir los pagos involucrados al sumar abonos.
Los filtros globales de empresa y el historial fiscal se conservan.

Esta fase no habilita notas mixtas después de pagos activos, egresos en otras
monedas, varios orígenes o modalidades fiscales que las notas aún no admiten.
La sustitución con notas que cambiaron el saldo o los impuestos históricos queda
bloqueada y requiere otra fase. No se realizó una prueba visual autenticada del
formulario en navegador; se probaron sus operaciones mediante los servicios.
