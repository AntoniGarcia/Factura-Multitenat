# Notas PPD con tratamientos de IVA mixtos

## Alcance

La factura básica PPD en MXN puede recibir notas antes de tener pagos vigentes,
aunque mezcle IVA trasladado, tasa cero, exento y conceptos sin desglose. Se
mantienen las restricciones de receptor nominativo, una factura de origen y
ausencia de retenciones, IEPS e impuestos locales.

El servidor calcula una copia fiscal del remanente: resta las bases e impuestos
de cada concepto acreditado usando su orden original. El reparto del pago usa el
total después de las notas y los impuestos restantes. La factura timbrada, sus
conceptos y los pagos ya emitidos no se modifican.

Los conceptos totalmente acreditados salen del cálculo del remanente. Su objeto
de impuesto se determina con los conceptos que aún se están pagando; si solo
queda un concepto no objeto, el pago no conserva impuestos de conceptos ya
devueltos. Se rechazan las notas cuyo desglose o relación no coincida con el
origen y los remanentes que no cuadren con los totales monetarios.

Las notas con IVA uniforme conservan el funcionamiento anterior: se pueden
aplicar después de un abono, dentro del saldo pendiente. Las mixtas se rechazan
al guardar o emitir cuando existe un pago timbrando, timbrado o en cancelación.
Los pagos cancelados no consumen saldo; sus números de parcialidad se conservan.

## Redondeos y concurrencia

Se comprueba que el IVA acumulado de las notas más el del remanente coincida con
el original a centavos. Si una división no lo permite, se exige ajustar la base;
no se inventa un ajuste de IVA. Por ejemplo, dividir una base de .08 al 16 % en
dos bases de .04 produciría dos importes de .01 frente al .01 original, por lo
que se rechaza esa captura.

También se bloquea la consulta, guardado y emisión de nuevos pagos cuando una
nota relacionada está timbrando o en cancelación. Esos estados siguen apartando
el disponible frente a otras notas, pero no se usan para emitir un pago antes de
conocer el resultado de la nota. El bloqueo evita que un pago se adelante a la
confirmación de una cancelación y conserve un saldo incorrecto.

Se mantiene el bloqueo de documentos por empresa al guardar y reservar. La
emisión y cancelación del PAC se realizan fuera de la transacción SQL.

## Esquema y permisos

No hay migración, columnas, permisos ni contratos congelados nuevos. Se usan los
comprobantes, conceptos, impuestos y relaciones existentes, con filtros globales
por empresa. El cálculo de la copia fiscal no se persiste como otro comprobante.

## Comprobaciones

- Compilación final: cero errores y cero advertencias.
- Regresión general: 313 pruebas correctas, dos omitidas y ninguna fallida.
- Integración: 212 comprobaciones correctas con servicios reales y PAC simulado;
  la base temporal se eliminó al terminar.
- Arranque local: 31 catálogos preparados sin recarga y respuesta HTTP 200.

La prueba temporal cubre una factura de 416 con cuatro conceptos de base 100:
IVA 16 %, IVA 0 %, exento y no objeto. Una nota acredita bases de 50, 20, 30 y 10,
por un total de 118. El saldo queda en 298. El pago de 149 usa bases fiscales de
25, 40 y 35, con IVA de 4; el concepto no objeto aporta el resto sin impuestos.

También se prueba la devolución total de los tres conceptos con desglose, que
deja 100 no objeto; el pago resultante no contiene impuestos de los conceptos ya
acreditados. Se verifican el rechazo de borradores anteriores a la nota, el
bloqueo de una nota mixta si un pago se adelantó y la recuperación del disponible
al cancelar los pagos y notas que correspondan. El XML se firma con certificado
sintético y se valida contra XSD usando los servicios reales del sistema y el
PAC simulado. Los PDF de nota y pago se renderizaron y revisaron visualmente.

Las pruebas SQL se ejecutan en bases temporales locales con compatibilidad 120,
que se eliminan al terminar. No sustituyen una ejecución en SQL Server 2014
nativo ni una prueba ante un PAC real. No se realizó una prueba visual autenticada
del formulario en navegador en esta fase.

## Pendientes

- Notas mixtas después de pagos vigentes y reparto del historial por grupo fiscal.
- Sustitución de pagos de facturas que tienen notas activas.
- Otras monedas, varios orígenes y variantes especializadas de factura.
- Retenciones, IEPS y complementos locales en las notas.

La guía de recepción de pagos del SAT, nota 4, conserva la distinción entre
egreso y pago: la nota reduce el comprobante de ingreso y no genera un abono
ficticio. El límite de captura antes de pagos vigentes es de esta fase del
sistema, no una prohibición fiscal general. Referencia del portal:
https://www.sat.gob.mx/portal/public/tramites/complemento-recepcion-de-pagos
