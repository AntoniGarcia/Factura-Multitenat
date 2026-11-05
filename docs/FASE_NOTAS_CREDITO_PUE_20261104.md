# Notas de crédito: primera modalidad

## Alcance

Se agregó la captura de CFDI de egreso desde Documentos → Nuevo documento → Nota
de crédito. La nota se relaciona con una sola factura básica de ingreso, timbrada,
vigente, PUE, en MXN y con receptor nominativo. Se admiten IVA trasladado, tasa
cero, exento y conceptos sin desglose; se excluyen retenciones, IEPS e impuestos
locales. La nota usa PUE, G02 y una serie activa de tipo Egreso.

Permite bonificación/descuento (relación 01) y devolución (03), con cantidades
monetarias positivas. Cada renglón identifica el concepto original mediante su
orden; conserva su descripción y tratamiento fiscal. Las claves de la nota son
84111506 y ACT. No se vuelve a consultar el producto actual para recalcular el
documento histórico. El receptor se copia de la factura original.

El formulario permite guardar, reabrir, modificar y generar el PDF del borrador.
La emisión usa los servicios existentes de CSD, folios, bolsa de timbres y PAC.
La descarga de PDF/XML y el envío usan los permisos de factura y los servicios
existentes del listado. No se configuró un PAC real.

## Controles

- Los permisos se comprueban en el servidor. Crear permite editar borradores
  propios; modificar también permite editar los de otros usuarios.
- Los filtros globales de empresa impiden consultar o acreditar una factura de
  otra empresa, incluso de la misma cuenta.
- Las notas timbrando, timbradas o en cancelación consumen el disponible de cada
  concepto; los borradores, errores y canceladas no lo consumen.
- Se recalcula el disponible al guardar y antes de reservar folio o timbre. El
  bloqueo de documentos de la empresa serializa esa comprobación.
- Se validan serie, forma de pago vigente, compatibilidad del uso G02, RFC del
  emisor y consistencia de conceptos, impuestos y totales.
- No se cancela la factura original mientras tenga notas vigentes. Esta modalidad
  permite cancelar notas con motivos 02 o 03; la cancelación confirmada libera
  el importe acreditado, sin devolver el timbre consumido.
- Los importes de egresos restan en las sumas del tablero. Los conteos siguen
  representando documentos.
- Guardado, emisión y cancelación conservan los registros de bitácora existentes.

La captura monetaria de esta modalidad usa centavos. Se rechaza una factura cuyo
neto por concepto difiera entre el cálculo de seis decimales y el importe/ descuento
publicados en el XML a dos decimales. También se rechaza una nota parcial si su
IVA redondeado, sumado al de las notas activas, difiere del IVA acumulado esperado.
Se informa al usuario para ajustar el importe o revisar las notas anteriores;
no se añade ni se oculta un centavo de ajuste. El IVA acumulado se toma de las
notas que continúan vigentes, incluyendo el efecto de sus cancelaciones.

## Base de datos

No hay migración ni columnas nuevas en esta fase. Se utilizan Comprobantes,
Conceptos, ImpuestosConcepto y los CFDI relacionados existentes. La variante
`nota_credito` distingue esta captura. No se modificaron contratos congelados.
Las migraciones de las fases anteriores siguen siendo necesarias al desplegar.

## Verificación

La prueba de integración creó una base temporal con nivel de compatibilidad 120,
usó los servicios reales de plataforma y un CSD sintético con el PAC simulado de
documentos. Terminó 139 comprobaciones y eliminó su base temporal. Incluyó:

- Guardar, reabrir y generar PDF del borrador.
- Emitir dos notas por 60 y 40 de base sobre una factura de 100 más IVA.
- Rechazar un segundo borrador que excede el remanente al intentar emitirlo.
- Rechazar serie inexistente, forma 99, importe excesivo y acceso de otra empresa.
- Descargar XML/PDF emitidos y comprobar la relación con el UUID original.
- Restar el egreso en el tablero, impedir la cancelación prematura del ingreso y
  liberar el importe al cancelar la nota, sin devolver timbres.
- Mantener los escenarios de pagos, SPEI y sustitución de las fases anteriores.

Se añadieron pruebas de cálculo para límites, tipo de origen, conceptos repetidos,
IVA cero/exento y diferencias de redondeo. El PDF generado se renderizó y revisó
visualmente. La regresión final terminó con 308 pruebas correctas, dos omitidas
y ninguna fallida. La compatibilidad 120 en SQL local no sustituye una prueba sobre el
servidor SQL Server 2014 del destinatario. No se probó aceptación ante un PAC real.

## Pendientes para otras fases

PPD y su interacción con saldos y complementos de pago; otras monedas; notas a
público en general o extranjeros; varios CFDI de origen; impuestos retenidos,
IEPS o locales; variantes Notaría, Obra y Comercio Exterior; sustitución de notas;
casos con redondeos de origen no admitidos en esta modalidad.

## Referencias

- La estructura y el sellado se validan con los recursos CFDI 4.0 que usa el
  servidor. Debe distinguirse esa validación técnica de la aceptación del PAC.
- [Anexo 29 de la RMF 2026](https://www.sat.gob.mx/minisitio/NormatividadRMFyRGCE/documentos2026/rmf/anexos/Anexo_29_RMF2026-09012026.pdf).
- [Guía histórica del SAT, apéndice 5, egresos](https://wwwmat.sat.gob.mx/cs/Satellite?blobcol=urldata&blobkey=id&blobtable=MungoBlobs&blobwhere=1461173358220&ssbinary=true).
  Es una guía de CFDI 3.3 y se conserva como referencia del procedimiento; no se
  presenta como especificación vigente de CFDI 4.0. Los límites PUE/MXN y de
  redondeo indicados arriba corresponden a esta fase del sistema.
