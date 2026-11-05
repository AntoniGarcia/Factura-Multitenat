# Sustitución del complemento de pagos

Fecha: 4 de noviembre de 2026.

## Funcionamiento

En el formulario de pagos se puede escribir el folio fiscal del pago anterior y
cargar sus datos para corregirlos. Al guardar, el nuevo comprobante conserva una
relación `04` con ese folio. La relación aparece en el XML y en el PDF.

El servidor exige un pago anterior de la misma empresa, del mismo cliente y con
las mismas facturas. Solo admite el último pago: los pagos posteriores, incluidos
los cancelados de parcialidad mayor, impiden sustituirlo desde este flujo. No se
admiten folios externos que no estén registrados en la empresa.

El cálculo excluye el abono anterior al preparar el sustituto y conserva su número
de parcialidad. Los importes y equivalencias se vuelven a validar; no se modifica
el documento original. Se puede corregir un sustituto cuando su antecesor ya está
cancelado y no tiene pagos posteriores.

## Emisión y cancelación

La [guía de llenado del SAT](https://www.sat.gob.mx/portal/public/tramites/complemento-recepcion-de-pagos),
pregunta frecuente 15, establece este orden: emitir el comprobante correcto con
relación `04`, y solicitar después la cancelación del anterior con motivo `01` y
el UUID del nuevo comprobante.

El flujo del sistema mantiene ese orden. Una sustitución emitida o en proceso
bloquea otro sustituto activo y los abonos nuevos a las facturas mientras siga
pendiente cancelar el anterior. Así no se publica un saldo calculado contando
ambos pagos. Si falla la emisión y queda en error, el pago original sigue
consumiendo su saldo.

La cancelación del anterior comprueba el motivo y que el sustituto esté timbrado
y lo relacione con clave `04`. El sustituto no se puede cancelar mientras su
antecesor siga vigente. Tampoco se puede cancelar un pago con parcialidades
posteriores vigentes: primero se cancelan las posteriores. La cancelación
pendiente del receptor sigue consumiendo saldo hasta su confirmación.

Guardado, preparación de emisión y solicitud de cancelación usan el bloqueo de
pagos de la empresa dentro de transacciones cortas. Las llamadas al PAC siguen
fuera de las transacciones.

## Datos y límites

No hay migración ni cambio de esquema en esta fase. Se utiliza la tabla existente
`ComprobantesRelacionados`. Los DTO de Documentos incorporan el UUID sustituido;
no se modificó la carpeta congelada de contratos compartidos.

Este flujo no cambia las facturas incluidas ni reconstruye pagos posteriores.
Esas correcciones requieren revisar primero la secuencia de pagos. El grupo
opcional SPEI queda pendiente. No se conectó un PAC real ni una pasarela.

## Verificación

- Compilación final: cero errores y cero advertencias.
- Regresión: 282 pruebas aprobadas, dos omitidas y ninguna fallida.
- Integración: 56 comprobaciones con servicios reales y SQL Server local en
  compatibilidad 120. Incluyen guardado, reapertura, precisión, aislamiento de
  empresa, XML firmado validado contra XSD, sustitución encadenada, rechazo de
  duplicados, conservación del histórico y recuperación del saldo corregido.
- La emisión y cancelación de integración usan el PAC simulado existente para
  comprobar la secuencia; no prueban aceptación fiscal ante un PAC real.
- PDF de sustitución renderizado y revisado visualmente, sin recortes en el caso
  probado. La base temporal se eliminó al terminar.
- No se realizó una revisión visual autenticada del formulario en navegador.
  La aplicación local se dejó en ejecución para esa revisión.
