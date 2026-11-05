# Notas de crédito sobre saldo PPD

Implementación y prueba de integración terminadas para el alcance indicado.
Complementa la modalidad PUE de la fase anterior.

## Alcance implementado

Facturas básicas PPD en MXN, nominativas y sin impuestos locales, retenciones ni
IEPS. Todos sus conceptos con importe neto positivo deben compartir objeto de
impuesto, factor y tasa de IVA. La nota sigue siendo un egreso PUE con uso G02,
relación 01 o 03, y serie de egreso. Conserva los límites de redondeo de la fase
PUE. Se admite aplicar una nota antes o después de un abono, hasta el saldo
pendiente; no se implementan devoluciones de importes ya pagados.

El saldo se obtiene del total original menos pagos y notas vigentes. Los estados
timbrando y en cancelación también apartan saldo. Una nota no es un pago ni
incrementa el número de parcialidad. Los documentos emitidos conservan los
saldos que tenían al emitirse; no se reescriben por una nota posterior.

Los borradores de pagos y notas se revalidan antes de reservar folio o timbre,
bajo el bloqueo de documentos existente. Si una operación anterior cambió el
disponible, se devuelve un error y se exige revisar el borrador.

El formulario de notas muestra el saldo disponible con IVA además de los límites
de base por concepto. El servidor vuelve a calcularlo al guardar y emitir.

## Límites de esta fase

- No se cancela una nota mientras existan pagos activos sobre la factura. Primero
  se cancelan los pagos que correspondan y después la nota. Es una restricción
  conservadora de esta modalidad para no alterar el historial de saldos.
- La sustitución de pagos se rechaza cuando las facturas involucradas tienen
  notas activas. Tampoco se emite una nota mientras la factura tenga pendiente
  la cancelación del origen de un pago sustituido.
- Se rechazan notas PPD con tratamientos fiscales mixtos. Los pagos sin notas
  conservan el soporte de mezclas de impuestos implementado anteriormente.
- Los egresos activos de otra modalidad se rechazan al resolver un pago en lugar
  de ignorar sus importes.
- No se integró un PAC real ni una pasarela de pagos.

## Datos y permisos

Sin migración ni cambios de esquema. No se modificó la configuración SQL ni la
base principal. Se usan las tablas de comprobantes, relacionados y pagos ya
existentes, con filtros globales por empresa. No se modificaron contratos
congelados. Se reutilizan los permisos de factura y pago.

## Verificación

La compilación de Shared, Client y Server fue confirmada por Luis. Después de
recuperarse la ejecución del agente, la integración terminó 173 comprobaciones
correctas sobre una base temporal con compatibilidad 120. La base se eliminó al
terminar. No fue necesario modificar el esquema.

La compilación final terminó con cero errores y cero advertencias. La regresión
general aprobó 309 pruebas, con dos omitidas y ninguna fallida. El sistema local
arrancó en http://localhost:5184, preparó los 31 catálogos configurados sin
recargarlos y respondió HTTP 200.

Se comprobó una factura PPD de 116, una nota de 29, saldo de 87 y un pago de 58
que deja 29. El XML del pago conserva saldo anterior 87 e IVA de 8 correspondiente
al abono. La nota no incrementa la parcialidad.

También se verificaron: rechazo de pagos preparados con el saldo anterior,
rechazo de notas superiores al saldo no pagado, revalidación de la nota cuando
un pago nuevo consume el remanente, cancelación condicionada a los pagos activos,
rechazo explícito de sustitución con notas activas y restauración del saldo de
116 tras cancelar pagos y notas. Se conservaron las regresiones anteriores de
equivalencias, SPEI, mezclas de impuestos, aislamiento y sustitución sin notas.

Comandos preparados, desde la raíz del proyecto:

```powershell
dotnet build .\Facturacion.Server\Facturacion.Server.csproj --no-restore -p:UseAppHost=false
dotnet test .\tests\Facturacion.Pruebas\Facturacion.Pruebas.csproj --no-restore -p:UseAppHost=false
dotnet run --project .\tmp\pagos-sat-20261104\RevisionNotasPpd.csproj -p:UseAppHost=false
```

El último comando es una herramienta temporal de comprobación: crea una base
local con nombre aleatorio y prefijo FacturacionNotasPrueba_, usa servicios reales
con certificado sintético y PAC simulado, y elimina esa base al terminar. Consulta
catálogos de FacturacionDev sin modificarla. No forma parte de la publicación.
Incluye pruebas de saldo, parcialidades, XML, permisos y cancelaciones, además de
las regresiones de pagos anteriores. Estas pruebas usan SQL local con
compatibilidad 120; no acreditan una ejecución en SQL Server 2014 nativo ni la
aceptación de un PAC real. No se completó una prueba visual autenticada del
formulario de notas en navegador en esta fase.

Referencia funcional consultada: guía del complemento de pagos del SAT conservada
en la revisión local, nota 4 y ejemplo del apéndice de pagos con egreso previo.
El egreso se aplica al comprobante de ingreso; no se crea un pago ficticio para
representarlo. La guía fue enlazada por el portal del complemento de recepción
de pagos durante las fases anteriores.
