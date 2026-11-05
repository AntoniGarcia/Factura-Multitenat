# Comparación funcional con el sistema anterior

Fecha de revisión: 4 de noviembre de 2026.

## Alcance de esta revisión

Se contrastaron las 31 secciones de `UI_Funcional.md`, los archivos de la carpeta
`Sistema anterior`, los DTO, formularios, servicios y generadores XML actuales.
Se inspeccionaron visualmente capturas clave de factura, constructoras, Comercio
Exterior, Notaría, Carta Porte y SPEI. El inventario escrito se usó para recorrer
los demás apartados. No se afirma haber repetido todos los flujos en pantalla en
esta revisión.

Es una revisión de cobertura y reglas por inspección. Las pruebas de ejecución de
la fase anterior están documentadas por separado en `REVISION_LOCAL_20261104.md`.
No se modificaron código funcional, configuración, esquema ni datos. El PAC real
y la pasarela de pagos quedan fuera de esta comparación.

## Resultado

La estructura principal del sistema anterior está implementada. Los pendientes
más importantes son reglas fiscales y escenarios condicionales, no pestañas
completas de Notaría ni volver a copiar el diseño anterior.

Que el XML pase el XSD demuestra estructura y tipos; no demuestra que cumpla todas
las reglas de la matriz del SAT, las relaciones entre catálogos o las condiciones
particulares de la operación.

## 1. Corregir el tratamiento de objeto de impuesto

Prioridad alta. Hallazgo confirmado por inspección.

`ValidadorDeProducto.cs:19` solo considera sin desglose las claves `01` y `03`.
En las líneas 80–92 exige impuestos a cualquier otra clave. Por ello un producto
con clave `04` u `08`, sin impuestos desglosados, termina rechazado como
`impuestos-faltan`. No basta con que esas claves aparezcan en el desplegable.

El SAT contempla `04` para operaciones objeto de IVA que no lo causan, y `08`
para determinados casos sin traslado de IVA ni desglose de IEPS. También deben
revisarse por separado `05`, `06` y `07`, según sus reglas; no conviene reducirlas
a una sola condición de «lleva impuestos / no lleva impuestos».

Además, `ServicioDeEmision.cs:619` admite las retenciones seleccionadas en factura
solo cuando el producto tiene `ObjetoImp = 02`. Debe revisarse junto con el motor
y con `ObjetoImpDR` en pagos para que todos apliquen el mismo criterio.

Fuente: [SAT, Anexo 20 y preguntas sobre objeto del impuesto](https://wwwmat.sat.gob.mx/consultas/35025/formato-de-factura-electronica-%28anexo-20%29).

## 2. Comercio Exterior: hay campos, faltan cruces de validación

Prioridad alta. La versión XML es 2.0. Están los domicilios del emisor y receptor,
residencia fiscal, registro tributario, pedimento, origen, INCOTERM, USD,
fracciones, unidades aduaneras y descripciones específicas.

Pendientes observados:

- `SatFraccionArancelaria` conserva clave, descripción y vigencia, pero no la
  unidad de medida de la tarifa asociada a la fracción. Se valida que fracción y
  unidad existan por separado; no su correspondencia.
- Falta cerrar las tolerancias y correspondencias de cantidad aduanera, valor
  unitario, valor en dólares y datos de los conceptos. La suma de valores en USD
  y el grupo cantidad/unidad/valor unitario ya tienen validación; no se cuentan
  otra vez como pendientes.
- El generador trata `CurpEmisor` e `Incoterm` como atributos opcionales y el
  servicio valida principalmente formato, longitudes y existencia en catálogo.
  Falta completar las condiciones fiscales de presencia y ausencia, incluido el
  tipo de persona del emisor.
- Revisar la prohibición de número de certificado cuando no corresponde y el
  ámbito del número de exportador confiable. La interfaz limpia el certificado
  al desmarcar origen, pero eso no sustituye la validación del servidor.
- Completar cruces de domicilio del emisor: actualmente se comprueban país,
  estado y municipio; falta cerrar el conjunto CP, localidad y colonia contra
  los datos oficiales que correspondan.

Referencias de código: `ServicioDeComercioExterior.cs:95`, `:123`, `:144`;
`GeneradorDeXmlComercioExterior.cs:32`, `:75`, `:93`, `:97`;
`CatalogosComercioExterior.cs:21`.

La captura actual se limita a ingreso con exportación definitiva `02` y
pedimento `A1`. Propietarios, destinatarios diferentes y otras modalidades son
ampliaciones condicionales; no son campos obligatorios en toda factura.

No recuperar `TipoOperacion` ni `Subdivision` de la captura antigua: el SAT los
retiró en 2.0. Fuente: [SAT, Comercio Exterior](https://wwwmat.sat.gob.mx/consultas/61165/comprobante-de-comercio-exterior).

## 3. Carta Porte: distinguir cobertura básica y escenarios no admitidos

Prioridad alta para evitar capturas que parezcan válidas y no puedan emitirse.

El generador declara Carta Porte 3.1 y cubre el traslado nacional por
autotransporte propio: mercancías, pesos, distancia, fechas, vehículo, permiso,
seguro de responsabilidad civil y operador. Es coherente con el caso básico
que muestra la captura 26.

Límites comprobados en `GeneradorDeXmlCartaPorte.cs`:

- Línea 45: `TranspInternac` se fija en `No`.
- Línea 113: exige exactamente dos ubicaciones.
- Línea 124: solo admite figura `01`.
- Líneas 78–99: no incorpora materiales peligrosos, embalaje, seguro ambiental
  ni remolques.

`SatClaveProdServCartaPorte` y `SatConfiguracionAutotransporte` solo conservan
clave, descripción y vigencia. No conservan los indicadores necesarios para
decidir si una mercancía requiere datos de material peligroso o una configuración
necesita remolques. Es un pendiente más importante que añadir casillas sueltas.

Para el alcance básico, se deben detectar y rechazar explícitamente esos casos
no soportados. Para ampliarlo, harán falta captura, persistencia, catálogos,
validación, XML y PDF correspondientes. Una unidad con remolque o mercancía
peligrosa puede ser un caso nacional: no debe confundirse con una ampliación
exclusivamente internacional.

Internacional, múltiples paradas, más figuras, logística inversa y transportes
marítimo, aéreo o ferroviario requieren una decisión de alcance. No todo atributo
opcional del esquema tiene que mostrarse a todos los usuarios.

Fuente: [SAT, Carta Porte](https://wwwmat.sat.gob.mx/consultas/68823/complemento-carta-porte-).
El esquema local `CartaPorte31.xsd` documenta estos grupos condicionales.

## 4. Complemento de pagos: tres diferencias concretas

1. **Monedas distintas.** `ServicioDePagos.cs:362` rechaza una factura cuya moneda
   difiera de la del pago. `GeneradorDeXmlPago.cs:84` también exige equivalencia
   igual a 1. Falta la captura y el cálculo con `EquivalenciaDR` para recibir,
   por ejemplo, MXN por una factura en USD. Actualmente la restricción es
   explícita; no se está suponiendo una equivalencia incorrecta.
2. **CFDI relacionados del recibo.** El generador sabe escribir esos nodos, pero
   `PeticionGuardarPago` y el formulario no permiten capturarlos. Las facturas
   abonadas en `DoctoRelacionado` no sustituyen esta relación de la raíz.
   Falta cerrar el flujo de sustitución de un recibo, presente como pestaña en
   el inventario anterior.
3. **SPEI opcional.** No están los cuatro campos antiguos: `TipoCadPago`,
   `CertPago`, `CadPago`, `SelloPago`. Es una diferencia funcional, pero no debe
   convertirse en requisito de toda transferencia. Si se incorpora el tipo de
   cadena, deben incorporarse y validarse los otros tres.

Fecha, forma de pago, referencia, bancos, documentos, parcialidades, saldos e
impuestos del complemento ya tienen representación. Conviene completar también
las validaciones condicionales bancarias antes de la generación XML; el servicio
comprueba longitudes, pero eso no cubre por sí solo las relaciones entre campos.

Fuente: [SAT, estándar de Pagos 2.0](https://wwwmat.sat.gob.mx/cs/Satellite?blobcol=urldata&blobkey=id&blobtable=MungoBlobs&blobwhere=1461175070885&ssbinary=true).

## 5. Constructoras: la estimación existe; no cubre cualquier supuesto fiscal

Los campos de la captura 15 tienen equivalente: importe de trabajos,
amortización, retenciones y devoluciones contractuales, deducciones e importe
líquido. Hay separación entre estimación comercial y CFDI, y control de anticipos.
No se deben copiar sin revisión las fórmulas fiscales del sistema antiguo.

Restricciones actuales de `ValidadorFiscalDeObra.cs`: ingreso nacional, MXN,
IVA trasladado y sin retenciones fiscales del CFDI. Una retención contractual
de garantía es distinta de una retención de IVA o ISR. Si la operación necesita
estas últimas, la variante actual no la admite.

No encontré implementación del complemento de servicios parciales de
construcción para casa habitación: faltan su captura y su generación, incluido
el permiso/licencia/autorización y los datos del inmueble. Tener su XSLT en el
repositorio no equivale a implementarlo. Es condicional al supuesto fiscal;
no se exige por el solo hecho de ser una constructora.

Fuente: [SAT, servicios parciales de construcción](https://wwwmat.sat.gob.mx/consulta/18158/si-prestas-servicios-parciales-de-construccion%2C-genera-facturas-electronicas-con-este-complemento).

## 6. Notaría: bloques principales cubiertos

Las secciones 20–25 están representadas: factura, perfil del notario, inmuebles,
instrumento y fecha, importes, vendedores, compradores y copropiedad. La captura
actual admite varios inmuebles y varias partes; no se detectó un bloque completo
faltante respecto al inventario.

El complemento sigue usando versión 1.0; eso no significa que el CFDI sea 1.0.
El SAT publicó revisión A para CFDI 4.0. El documento técnico vigente consultado
requiere, entre otros, CP de cinco dígitos e instrumento numérico de hasta seis
dígitos. No debe recuperarse una captura libre de esos campos porque el inventario
anterior los describa de otra manera.

Quedan las verificaciones de casos particulares del negocio, sin presentar la
ausencia de hallazgos de campos como una certificación fiscal completa.

Fuente: [SAT, estándar de Notarios Públicos](https://www.sat.gob.mx/sitio_internet/cfd/notariospublicos/notariospublicos.pdf).

## 7. Facturación general y catálogos

Factura básica, datos fiscales del receptor, conceptos, precio editable,
descuentos, impuestos, condiciones, observaciones, información global y grupos
de CFDI relacionados están implementados. Los folios reservados al emitir,
monedas habilitadas por empresa y catálogo de clientes/productos compartido por
cuenta responden a decisiones posteriores, no a campos olvidados.

Hay capacidades de CFDI 4.0 que no están cubiertas en la captura general:

- Emisión de egresos/notas de crédito: `ServicioDeEmision` crea exclusivamente
  tipo `I`. Que exista la constante `E` o el catálogo no habilita ese flujo.
- Información aduanera de conceptos para casos de importación, cuenta predial
  y operaciones a cuenta de terceros: no se localizaron en los DTO de conceptos
  ni en el generador general. Son datos condicionales; deben incorporarse solo
  si se ofrecen esas operaciones.

Nómina no forma parte del alcance acordado. Tampoco cotizaciones, Addendas, INE
e inventario. No se cuentan como errores de la migración actual.

La consulta y actualización de catálogos por el operador ya existe. Sin embargo,
los archivos iniciales tienen fechas distintas: CFDI usa un archivo nombrado
20260806 y fracciones uno nombrado 20240513. El nombre del archivo no demuestra
que el contenido sea la última publicación del SAT. Falta verificar la versión
instalada de cada catálogo frente al archivo oficial más reciente; esta revisión
no certifica esa igualdad para todos los catálogos.

Fuente: [SAT, Anexo 20](https://wwwmat.sat.gob.mx/consultas/35025/formato-de-factura-electronica-%28anexo-20%29).

## Correspondencia del resto del inventario

- Secciones 1–2: documentos y navegación actuales cubren la estructura principal.
  Los filtros modernos sustituyen controles antiguos; no hace falta repetirlos.
- Secciones 3–4: configuración, empresa, CSD, logo y SMTP están distribuidos entre
  cliente y operador. Las credenciales PAC del sistema viejo no deben pedirse al
  cliente. El SMTP de registro sigue separado del de comprobantes.
- Secciones 5–9: clientes, productos y usuarios tienen listados y formularios.
  Cuenta, accesos a empresas y permisos granulares reemplazan el modelo anterior.
- Secciones 10–11: vehículos y figuras cubren los datos básicos antiguos. Las
  condiciones adicionales de Carta Porte están descritas en el punto 3.
- Sección 30: las solicitudes de cancelación tienen motivo, UUID sustituto y
  campos de estatus SAT. Confirmar su respuesta real corresponde a la integración
  PAC/SAT, excluida aquí.
- Sección 31: el autocompletado sustituye la ventana antigua de claves. Que no
  exista el botón de búsqueda anterior no representa pérdida de la función.

## Orden recomendado para las siguientes fases

1. Corregir objeto de impuesto y comprobar coherencia producto → factura → pago.
2. Completar validaciones del caso actual de Comercio Exterior 2.0.
3. Cerrar los límites de Carta Porte: detectar lo no admitido y, según alcance,
   implementar remolques y materiales peligrosos.
4. Completar pagos: equivalencias, relación de sustitución y grupo SPEI opcional.
5. Acordar si la entrega incluye egresos, servicios parciales de construcción y
   otros escenarios condicionales. Después implementar solo los acordados.
6. Verificar publicaciones de catálogos y actualizar el inventario funcional para
   que separe claramente lo heredado, lo vigente y lo que no se ofrece.

No se empezó ninguna de estas fases de implementación en esta revisión.

Actualización posterior: se implementó la primera fase de reglas de objeto de
impuesto. Su alcance, pruebas y límites están en `FASE_OBJETO_IMPUESTO_20261104.md`.

Segunda actualización: se completaron las validaciones y cruces de catálogos del
caso actual de Comercio Exterior, con una migración de metadatos SAT. Detalles y
límites en `FASE_COMERCIO_EXTERIOR_20261104.md`.

Tercera actualización: Carta Porte conserva los indicadores SAT y la declaración
de peligrosidad. Se valida al guardar y generar XML; se bloquean los casos que
requieren remolques o datos de material peligroso aún no capturados. La migración,
las pruebas y el alcance están en `FASE_CARTA_PORTE_20261104.md`.

Cuarta actualización: se incorporaron hasta dos remolques por traslado con subtipo
SAT, placa, validación por configuración vehicular y salida en XML/PDF. El catálogo
se carga automáticamente. Detalles de la migración y las pruebas en
`FASE_REMOLQUES_CARTA_PORTE_20261104.md`. La captura de materiales peligrosos sigue
pendiente.

Quinta actualización: se incorporó la ficha de material peligroso, embalaje y
seguro ambiental al traslado nacional por autotransporte, con validación de
catálogos, guardado y salida XML/PDF. La migración y las pruebas están en
`FASE_MATERIALES_PELIGROSOS_20261104.md`. Esto no completa los campos sanitarios
COFEPRIS ni otras modalidades de transporte.

Sexta actualización: se verificó la vigencia sanitaria contra el libro enlazado
por el portal actual del SAT. Sector COFEPRIS, forma farmacéutica y condiciones
especiales siguen con fecha «Por definir»; no se habilitaron como requisitos del
formulario actual. Se actualizaron trece descripciones de bienes transportados,
sin cambios de claves o clasificación. Evidencia y alcance en
`REVISION_COFEPRIS_20261104.md`.

Séptima actualización: el complemento de pagos admite facturas en una moneda
distinta de la recibida, con equivalencias de diez decimales y saldos conservados
en la moneda de cada factura. Se corrigió la precisión importada de monedas y se
comprobaron guardado, reparto, XML y PDF. Migración, actualización del catálogo y
evidencia en `FASE_EQUIVALENCIAS_PAGOS_20261104.md`. Siguen pendientes la relación
de sustitución y el grupo opcional SPEI.

Octava actualización: se agregó la sustitución del último pago de las mismas
facturas mediante relación 04, con carga del anterior, guardado, XML/PDF y
validación de la cancelación con motivo 01. Los saldos se comprueban sin duplicar
el abono y se bloquean correcciones que afecten pagos posteriores. No hubo
migración en esta fase. Evidencia y límites en `FASE_SUSTITUCION_PAGOS_20261104.md`.
Queda pendiente el grupo opcional SPEI.

Novena actualización: se incorporó el grupo opcional SPEI al pago, con captura,
validación del servidor, guardado y reapertura, atributos XML e indicador PDF.
La migración agrega cuatro columnas opcionales sin cambiar los pagos anteriores.
Pruebas, script incremental y límites en `FASE_SPEI_PAGOS_20261104.md`. Esto no
incluye la autenticación del CEP con Banco de México ni la aceptación de un PAC
real. Los escenarios pendientes de obra, egresos y transporte internacional
siguen sujetos al alcance indicado en esta revisión.

Décima actualización: se cerró la revisión de pagos con objetos de impuesto
mixtos conservando la clasificación agregada existente. Se validan los conceptos
antes de agrupar y se compara el desglose guardado con el esperado antes de
reservar folio o timbre. Se comprobaron IVA, ISR, IEPS, tasa cero, exentos y
conceptos sin desglose, sin cambios de esquema. Evidencia y límites en
`FASE_PAGOS_OBJETOS_MIXTOS_20261104.md`. La emisión de egresos/notas de crédito
continúa pendiente de implementación.

Undécima actualización: se incorporó la primera modalidad de notas de crédito
relacionadas con una factura básica PUE en MXN, con captura, guardado, PDF/XML,
emisión y cancelación mediante la infraestructura existente. Se controla el
disponible por concepto y los egresos restan en el tablero. No hay migración en
esta fase. Alcance, comprobaciones y límites de redondeo en
`FASE_NOTAS_CREDITO_PUE_20261104.md`. Continúan pendientes PPD y sus saldos,
otras monedas, variantes y demás casos señalados en ese documento.

Duodécima actualización (5 de noviembre): las notas admiten facturas básicas PPD
en MXN con tratamiento de IVA uniforme, hasta el saldo no pagado. Los pagos nuevos
descuentan los egresos activos y se revalidan los borradores antes de reservar
folio o timbre. Se bloquean sustituciones con notas activas y cancelaciones de
notas con pagos vigentes para conservar el historial. No hay migración en esta
fase. Comprobaciones y límites en `FASE_NOTAS_CREDITO_PPD_20261105.md`.

Decimotercera actualización (5 de noviembre): las notas PPD admiten mezclas de
IVA, tasa cero, exentos y conceptos sin desglose antes del primer pago vigente.
Los pagos calculan sus impuestos sobre el remanente de cada concepto, sin cambiar
la factura original. Se bloquean pagos cuando una nota relacionada está emitiéndose
o cancelándose y se comprueba que el redondeo del IVA restante cuadre con el
original. Sin migración en esta fase. Evidencia y límites en
`FASE_NOTAS_PPD_IMPUESTOS_MIXTOS_20261105.md`.

Decimocuarta actualización (5 de noviembre): se habilitó sustituir el último pago
de facturas PPD en MXN con notas cuando el saldo anterior y el desglose fiscal
original siguen vigentes. Se comprobó emisión, XML/PDF, cancelación con motivo 01
y sustitución encadenada sin duplicar abonos ni descontar notas dos veces. Se
rechazan cambios posteriores del saldo o de sus impuestos. No hay migración en
esta fase. Alcance y evidencia en `FASE_SUSTITUCION_PAGOS_CON_NOTAS_20261105.md`.

Decimoquinta actualización (5 de noviembre): las notas mixtas PPD en MXN pueden
aplicarse después de pagos vigentes, hasta las bases e IVA no cobrados de cada
grupo fiscal. Los siguientes pagos usan el desglose restante y se conservan
los pagos históricos. Se rechazan grupos agotados e historiales que no puedan
reconstruirse con precisión. No hay migración en esta fase. Evidencia y límites
en `FASE_NOTAS_MIXTAS_POSTERIORES_20261105.md`.

Decimosexta actualización (5 de noviembre): se agregaron pedimentos, cuentas
prediales y operaciones a cuenta de terceros por concepto, además del complemento
opcional de servicios parciales de construcción de casa habitación en Obra.
Se conservan al reabrir y se generan en XML/PDF. Se añadieron dos columnas
opcionales y un incremental compatible con SQL 2014. Alcance, pruebas y pendientes
en `FASE_DATOS_FISCALES_Y_VIVIENDA_20261105.md`.
