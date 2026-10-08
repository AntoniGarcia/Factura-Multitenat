# Estado actual del sistema de facturación

**Corte documental: 8 de octubre de 2026.** Este archivo sustituye el reporte del
24 de agosto, que describía la mitad B como un módulo vacío. Para comprobar el estado de
un entorno concreto, se deben revisar su versión desplegada, sus migraciones y sus pruebas:
un cambio en `develop` no demuestra por sí solo que Azure ya lo esté ejecutando.

## Alcance y estado verificable

El repositorio contiene una aplicación CFDI 4.0 multiempresa con cliente Blazor
WebAssembly, API ASP.NET Core y SQL Server. Están implementados la identidad, las
empresas emisoras, los usuarios y permisos, los catálogos SAT, los clientes, los
productos, las series y folios, y la bolsa de timbres. El panel del operador permite
gestionar cuentas, paquetes y compras; las compras pueden quedar pendientes y un
operador puede acreditar el pago manualmente.

La mitad de Documentos ya no está vacía. Tiene captura de borradores, cálculo de
impuestos, generación de salidas, flujo de timbrado, cancelación y complemento de pagos.
También hay pantallas y servicios para Carta Porte, Comercio Exterior, Notaría y
Constructoras. Estos módulos se agregaron después del reparto original: que el código
exista no equivale a haber validado todos sus casos fiscales de punta a punta.

La integración con un PAC está preparada en el código, pero **no se ha acreditado aquí
un timbrado real ni una cancelación real con las credenciales del ingeniero**. Para
revisión sin PAC se utiliza `Staging` con `Pac__Modo=Deshabilitado`: se pueden trabajar
catálogos y borradores, pero la emisión, los complementos y las llamadas al PAC quedan
bloqueados. `Production` no admite ese modo. No debe presentarse un PDF o XML preliminar
como CFDI timbrado.

La pasarela de pagos y el carrusel de pagos siguen fuera de esta fase, por decisión del
proyecto. Una solicitud de compra de timbres no acredita saldo automáticamente: la
acreditación manual es la operación vigente hasta que se decida e implemente una
integración de pago.

## Evidencia de la última entrega de código

Al cierre previo a este documento, `develop` estaba sincronizada con `origin/develop`.
Los últimos cambios publicados fueron `46305d6`, `8796b99`, `f7e64f4` y `ebadbca`:
impuestos y salidas de obra; Carta Porte; reimportación de productos y tasas; avisos
de interfaz. La compilación Release pasó sin errores ni advertencias nuevas; la suite
registró 115 pruebas aprobadas y una omitida. Esa verificación es de código local:
**no confirma** migraciones aplicadas, configuración ni funcionamiento de Azure.

## Pendientes antes de llamar al sistema completo

### Revisión manual local del 7 de octubre de 2026

- Comercio Exterior: se comprobó captura, guardado, reapertura, modificación, PDF y XML
  del complemento 2.0. Se corrigió la selección del país extranjero y se incorporaron
  residencia fiscal y registro tributario a las copias del receptor del comprobante.
  El CP del receptor genérico se toma del lugar de expedición según CFDI 4.0.
- Carta Porte: se cargaron las 48,757 claves vigentes de `c_ClaveProdServCP` del libro
  disponible. En LLANTERA DEL CENTRO se creó y modificó un borrador de prueba: cantidad
  5, peso unitario 10 kg y distancia 12.50 km; después se agregó un segundo renglón de
  2 unidades de 3.5 kg, con total 57 kg y cantidad agrupada 7 H87. También se buscó
  DEMO-1 de esa empresa y se guardó su UUID con relación 05. Se verificaron reapertura,
  rechazo de dimensiones incorrectas, previsualización y descarga del PDF con ambos
  renglones y el CFDI relacionado.
- Se corrigió la captura inmediata de cantidades y domicilios de Carta Porte. El
  traslado exige una figura 01 — Operador y valida su RFC, nombre y licencia. Los
  horarios de captura los calcula el servidor con el huso de la empresa: guardar y
  reabrir ya no transforma 09:00 en 15:00.
- El PDF de prueba se revisó visualmente y conserva el aviso de borrador sin validez
  fiscal. El saldo de la empresa permaneció en 600 timbres durante estas operaciones.
- La compilación Release de esta revisión terminó con cero advertencias y errores;
  las pruebas automatizadas registraron 115 aprobadas, una omitida y ninguna fallida.
- La validación y descarga del CFDI XML completo quedaron detenidas por falta de un
  CSD vigente en las empresas de prueba. Esto es independiente de las credenciales del
  PAC: no se ha acreditado todavía el flujo fiscal completo de estos módulos.
- Se observó un timeout SQL al consultar el catálogo de bienes después de importarlo.
  La consulta posterior tardó 40 ms, no había bloqueos activos y el reintento guardó el
  borrador. No se considera acreditada una corrección de rendimiento por ese reintento.

La revisión anterior cubre los casos descritos, no todos los escenarios de los módulos.
Quedan pendientes escenarios con datos de transporte incompletos o antiguos y pruebas
de aislamiento con varias cuentas. La prueba con DEMO-1 usa datos de demostración del
entorno local, no acredita la existencia de un CFDI certificado por el SAT.

### Complemento de pagos — revisión de captura del 8 de octubre de 2026

- Se corrigió la marca de descarte automático: un pago guardado deja de estar marcado
  para eliminación al abandonar el formulario. La interfaz conserva el cliente al
  reabrir y permite editar el abono en su renglón; cambiar de cliente retira las
  facturas e importes de la captura anterior.
- El guardado valida facturas de ingreso timbradas, método PPD, cliente y RFC del
  receptor, documentos duplicados, moneda vigente, forma de pago distinta de 99,
  precisión monetaria, cuadre exacto e importes positivos. Mientras no se capture
  `EquivalenciaDR`, se rechazan monedas distintas en lugar de asumir equivalencia 1.
- Se retiró la obligatoriedad general de cuentas bancarias heredada del sistema
  anterior. El SAT indica expresamente que para transferencias, incluido SPEI, no es
  necesario incorporar esos datos. Referencia:
  [Complemento de pagos del SAT](https://wwwmat.sat.gob.mx/consultas/92764/comprobante-de-recepcion-de-pagos).
- En la interfaz local de CEMENTERA DEL BAJIO se comprobó captura inmediata:
  escribir 50 y aumentar da 51; seleccionar el cliente muestra su RFC y nombre;
  consultar DEMO-1 rechaza su método de pago ausente y guardar sin documentos
  devuelve un mensaje de validación con referencia de soporte. La pestaña Bancos
  ya muestra las cuentas como opcionales.
- La compilación Release final terminó sin advertencias ni errores. La suite
  existente registró 115 pruebas aprobadas, una omitida y ninguna fallida; no incluye
  una prueba positiva de guardado del complemento de pagos.
- No existe en la base local una factura de ingreso timbrada PPD con cliente
  asociado. Por eso siguen pendientes las pruebas positivas de guardar, salir,
  reabrir y modificar el pago con datos válidos. Los documentos DEMO no acreditan
  ese flujo; no se modificaron para hacerlos pasar como CFDI reales.
- Al cierre de la captura, la revisión del código detectó pendientes fiscales:
  desglose `ImpuestosP`, conversión de `Totales` a MXN, precisión de impuestos,
  fecha y hora locales del pago en captura y salidas, y revalidación del saldo antes
  de emitir ante pagos concurrentes. PDF/XML del pago no se consideran verificados.
  SPEI y relaciones para sustitución también deben contrastarse con el inventario
  funcional y las reglas actuales del SAT.

### Complemento de pagos — XML del 8 de octubre de 2026

- El XML ahora incluye `ImpuestosP`: retenciones agrupadas por impuesto y traslados
  agrupados por impuesto, factor y tasa. `ImpuestosDR` e `ImpuestosP` conservan seis
  decimales; `Totales` se convierte a MXN con `TipoCambioP` y se presenta con dos.
  La tasa cero declara su impuesto en cero; los exentos omiten tasa e importe.
- `FechaPagoUtc` se convierte al huso de la empresa al escribir `FechaPago`. La prueba
  de 15:00 UTC produjo 09:00 en el huso central de México. La captura de fecha/hora
  y su presentación en PDF siguen pendientes de revisión.
- Se descargaron del SAT los archivos locales faltantes `Pagos20.xsd` y `catPagos.xsd`.
  Se verificaron 11 documentos sintéticos contra CFDI 4.0 + Pagos 2.0 y se calculó su
  cadena original con el XSLT oficial: IVA 16 %, 8 %, 0 %, exento, sin impuestos,
  retenciones, USD, dos documentos agrupados, fracciones, mezcla de tasas y un centavo.
- Ejemplos revisados: 116 USD con cambio 20 produce monto total 2,320 MXN, base IVA
  2,000 MXN e impuesto 320 MXN; dos documentos con base 100 e IVA 16 se agregan en
  un traslado de base 200 e IVA 32. Un abono de 50 sobre 116 se repartió en base
  43.103448 e IVA 6.896552 con el cálculo usado por el servidor.
- Se comprobaron nueve rechazos: cambio ausente, saldos inconsistentes, monto que no
  cuadra, precisión excesiva, monedas distintas, desglose ausente, documentos
  duplicados, forma 99 y desglose antiguo incompatible con el cálculo actual. El
  servicio devuelve errores de negocio antes de obtener el CSD; antes de reservar
  folio o timbre también se valida el XML completo.
- La herramienta manual `tests/Facturacion.Server.PreviewTool` genera y valida XML
  de prueba desde JSON; no consulta ni modifica la base, no firma ni llama al PAC.
  Sus marcadores de certificado y sello son sintéticos y cada XML lleva el aviso
  explícito de prueba sin validez fiscal. Los casos usados quedan en `tmp/pagos-xml/`,
  fuera del control de versiones; los artefactos oficiales permanecen en `CatalogosSAT/`.
- Esta evidencia comprueba estructura y los cálculos descritos, no una certificación
  fiscal integral. Siguen pendientes el guardado positivo con una factura PPD válida,
  CSD vigente, PDF, captura de hora, concurrencia, sustitución y escenarios bancarios
  avanzados. No se modificaron datos de empresa ni se consumieron timbres.
- La solución y el verificador manual compilaron en Release sin advertencias ni
  errores. La suite existente terminó con 115 pruebas aprobadas, una omitida y
  ninguna fallida. La aplicación quedó ejecutándose en el entorno local.

### Trabajo pendiente

1. Validar manualmente, con datos representativos y por empresa, los flujos completos
   de factura estándar, Carta Porte, Comercio Exterior, Notaría y Constructoras;
   verificar borrador, impuestos, XML/PDF y errores de validación.
2. Completar la preparación de entrega local: configuración obligatoria, almacenamiento
   persistente, migraciones y versiones de los catálogos SAT. Azure está descartado
   para el trabajo actual; la guía de despliegue queda como referencia histórica.
3. Retomar la auditoría de aislamiento entre inquilinos y sus correcciones pendientes
   que se pospusieron expresamente. No declarar cerrada la seguridad multiempresa
   basándose solo en los filtros o pruebas existentes.
4. Cuando el ingeniero proporcione las credenciales del PAC, configurar `Pac__Modo=Real`
   y probar timbrado, consulta, cancelación y recuperación de fallos en un entorno
   autorizado antes de pasar a producción.
5. Decidir la pasarela de pagos y diseñar la acreditación automática por separado.
   No simular un cobro ni liberar timbres por pulsar «comprar».

El objetivo de entrega indicado en [AGENTS.md](AGENTS.md) es el **5 de diciembre de
2026**. Los planes originales de [PROMPT-FASES-A.md](PROMPT-FASES-A.md),
[PROMPT-FASES-B.md](PROMPT-FASES-B.md) y [REPARTO-EQUIPO.md](REPARTO-EQUIPO.md) son
referencias históricas de alcance y secuencia, no un indicador del progreso actual.

### Complemento de pagos — protección concurrente del 8 de octubre de 2026

- Guardar y descartar pagos usa una transacción y un bloqueo SQL por empresa.
  La reserva para emisión toma el mismo bloqueo antes de leer el comprobante;
  la transacción termina antes de enviar al PAC. Empresas diferentes usan recursos
  distintos. El bloqueo también serializa la reserva de otros tipos de documento
  de esa empresa, no las llamadas externas ni toda la duración de la emisión.
- Antes de reservar folio o timbre, un pago vuelve a comprobar facturas PPD,
  receptor, moneda, saldo y parcialidad. Si otro pago cambió la información,
  se rechaza el borrador desactualizado y se pide guardarlo y revisarlo otra vez.
- Timbrando, timbrado y en_cancelacion consumen/apartan saldo. Cancelado libera
  importe; su número de parcialidad no se reutiliza. Los borradores no apartan saldo.
- Comprobación manual con dos conexiones SQL reales: el mismo recurso bloquea
  la segunda sesión, otro recurso no se bloquea y finalizar la transacción libera
  el recurso. Sin insertar, modificar o borrar datos fiscales ni consumir timbres.
- Compilación Release sin advertencias ni errores. Sin migraciones ni cambios en
  contratos congelados. Todavía no se probó la reserva completa con dos pagos PPD:
  falta una factura PPD válida y el CSD; el PAC sigue fuera de esta revisión.

## Para continuar

### Entorno QA entre cuentas — 8 de octubre de 2026

- Por autorización de Luis se creó en la base local la cuenta `QA - aislamiento entre
  cuentas`, con empresa `QA AISLAMIENTO NO EMITIR CFDI` y usuario
  `qa-aislamiento@example.invalid`, mediante ASP.NET Core Identity y una transacción.
- El usuario tiene únicamente permiso timbrar en esa empresa. No tiene contraseña:
  debe asignarse por un flujo autorizado antes del inicio de sesión interactivo.
  No se guardaron credenciales en código, archivos ni logs y no se enviaron correos.
- La empresa usa un RFC genérico para la identificación QA; no representa una empresa
  fiscal real. No tiene CSD, timbres, documentos, licencias especiales ni SMTP propio.
- Se registró el alta QA en bitácora. La herramienta `preparar-cuenta-qa` no modifica
  una cuenta preexistente ni restablece sus credenciales al repetirse.
- No se cambiaron cuentas o empresas anteriores. Este entorno habilita la siguiente
  comprobación HTTP autenticada; crearlo no significa que dicha prueba esté terminada.
- Tras el alta: 40 consultas EF de aislamiento correctas, tres empresas y dos cuentas;
  25 relaciones comprobadas sin cruces. La contraseña puede asignarse desde Operador,
  listado de usuarios de plataforma, acción Restablecer contraseña; exige permiso de
  soporte y confirmación con la contraseña del operador.

### Aislamiento — comprobaciones de datos del 8 de octubre de 2026

- Herramienta manual `relaciones-empresa`: comprueba las FK entre entidades de empresa
  del modelo y referencias de snapshots (cliente, serie, producto y cliente destino).
  Resultado local: 25 relaciones revisadas, cero cruces de EmpresaId. También comprueba
  presencia del filtro global en las entidades obligatorias. No modifica ni elimina datos.
- Herramienta manual `aislamiento-documentos`: consultas EF reales a comprobantes,
  conceptos, impuestos, pagos, documentos pagados, clientes, productos y series en cada
  empresa, sin empresa activa y con una empresa inexistente. Resultado: 32 consultas
  correctas, dos empresas de una sola cuenta. Solo proyecta EmpresaId, no secretos.
- Estos resultados describen los datos actuales: no prueban que SQL impida cualquier
  relación cruzada futura, ni sustituyen la prueba HTTP autenticada con dos cuentas.
- La base solo tiene una cuenta con empresas; la prueba cruzada entre cuentas necesita
  una segunda cuenta de prueba con empresa y usuario habilitados. No se crearon cuentas
  ni se ampliaron permisos durante esta subfase. Las pruebas de correo no envían mensajes.
- La herramienta manual compiló en Release sin advertencias ni errores. La aplicación
  sigue ejecutándose. Pagos completos con PPD y pruebas HTTP entre cuentas siguen pendientes.
- GET anónimos al documento, vista previa PDF y propuesta de correo devolvieron 401.
  No se enviaron credenciales, adjuntos ni mensajes. Esto prueba autenticación obligatoria,
  no la denegación de permisos de un usuario autenticado.

### Documentos — edición y reserva del 8 de octubre de 2026

- Guardar una factura de ingreso ahora rechaza identificadores de pagos y traslados;
  evita modificar sus snapshots mediante la petición de factura estándar.
- Guardado y descarte de facturas toman el mismo bloqueo transaccional por empresa
  que la reserva para emisión. Se lee el estado después de tomar el bloqueo, evitando
  guardar desde un estado observado antes de que otra petición empezara la emisión.
- Revisión de código: descargas y correo requieren permiso timbrar y consultan el
  comprobante bajo el filtro de empresa. Esto no sustituye la prueba HTTP con dos cuentas.
- Se detectó que falta validación anticipada de SerieId en el guardado. El contrato
  IServicioFolios no ofrece consulta/validación; se pidió autorización para ampliarlo
  con commit dedicado, sin cruzar directamente la frontera de Plataforma desde Documentos.
  Luis autorizó la ampliación; el commit de contrato `0fcd6df` agrega ValidarSerieAsync.
  ServicioDeFolios la implementa con el filtro global. Guardar factura y reservar emisión
  la consumen para rechazar series ajenas, inactivas o de tipo incompatible.
  Un documento con folio reservado tampoco puede cambiar de serie al editar.
  Se verificaron serie válida, ajena, inexistente y tipo incompatible usando servicios
  reales y consultas de solo lectura. No hay series inactivas locales para verificar
  ese caso con datos reales. La implementación aún está pendiente de commit.
- Compilación Release: cero advertencias y errores. Sin migraciones ni cambios en
  contratos congelados. Siguen pendientes CSV, alcance de reportes, pruebas completas
  por variante, accesos cruzados y Pagos con una factura PPD válida. No se declara la
  revisión final del sistema terminada.

### Aislamiento — refuerzo de escrituras del 8 de octubre de 2026

- La base local contiene cero facturas de ingreso timbradas PPD con cliente y UUID;
  no se alteraron registros DEMO para completar artificialmente las pruebas de Pagos.
- SelladoDeEmpresaInterceptor ahora rechaza modificar o eliminar una entidad cuya
  empresa actual u original difiera de la empresa activa. Conserva la prohibición
  de cambiar EmpresaId y el sellado de altas.
- Se revisaron los usos globales de operador, consola y barridos antes del cambio.
  Los ámbitos sin empresa activa conservan su comportamiento previo; este refuerzo
  no sustituye la autorización de dichos procesos ni valida relaciones entre tablas.
- Comprobación manual con entidades no persistidas: edición y eliminación de empresa
  ajena rechazadas por el interceptor antes de enviar SQL. Sin cambios en datos reales.
- Compilación Release sin advertencias ni errores. No hay migraciones ni cambios en
  contratos congelados. La auditoría completa con dos cuentas y varias empresas sigue
  pendiente, incluyendo endpoints, archivos, correo y claves foráneas entre empresas.
- La primera ejecución de la suite se abortó por un fallo nativo de QuestPDF/Skia
  (0xC0000005 al disponer el texto), no por una aserción de aislamiento.
  La repetición terminó con 115 pruebas aprobadas, una omitida y cero fallidas.
  El fallo nativo queda registrado como intermitente; no se afirma haberlo corregido.

### Complemento de pagos — PDF del 8 de octubre de 2026

- El formulario ofrece vista previa PDF después de guardar, reutilizando el visor
  existente y su descarga. No reserva folio ni consume timbres.
- La consulta del borrador incluye pagos, documentos e impuestos y valida el pago;
  ya no exige conceptos de factura para un comprobante tipo P.
- El PDF convierte la fecha UTC al huso de la empresa y presenta totales en MXN.
  Comparte con el XML la agrupación de impuestos, equivalencias y precisión de seis
  decimales, evitando discrepancias al convertir moneda extranjera.
- Se generaron pruebas sintéticas USD, dos documentos agrupados y exento, sin
  escribir en la base ni llamar al PAC. El PDF USD se revisó visualmente: pago local
  09:00, 116 USD a cambio 20, total 2,320 MXN e IVA 320 MXN.
- Compilación Release sin advertencias ni errores; aplicación local reiniciada.
  La prueba completa desde la pantalla requiere una factura PPD válida disponible.
- Al cierre de esta subfase seguía pendiente la captura de fecha y hora, atendida
  en la siguiente sección; permanecen concurrencia y guardar/reabrir con datos reales.

### Complemento de pagos — captura de fecha y hora del 8 de octubre de 2026

- La pantalla captura fecha y hora por separado y muestra el huso de la empresa.
  No usa la zona horaria del navegador ni el fallback UTC de CampoFecha.
- El API devuelve hora local sin desplazamiento y su huso. Al guardar, el servidor
  interpreta esa hora con HusoDeEmpresa y almacena UTC; conserva compatibilidad con
  solicitudes anteriores que solo enviaban FechaPagoUtc.
- Rechaza fechas locales con desplazamiento, horas ambiguas o inexistentes por
  cambio de horario y rangos inválidos. No hay migraciones ni cambios en Contratos.
- Verificación manual de conversión: 09:35:42 local central de México corresponde
  a 15:35:42 UTC y regresa sin perder hora ni segundos; se comprobaron detección de
  hora inexistente y ambigua en un huso fronterizo.
- Compilación Release: cero advertencias y errores. La aplicación local arranca con
  nueve contratos reales. El selector se verificó en el navegador local: selección
  de 09:35 y cambio al 07/10/2026 sin perder la hora; huso de empresa visible.
  No se guardó el borrador incompleto. Sigue pendiente guardar/reabrir un pago
  completo, que requiere una factura PPD válida.

- [README.md](README.md): entrada al repositorio y mapa de documentación.
- [docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md): preparación del entorno local.
- [docs/DESPLIEGUE.md](docs/DESPLIEGUE.md): configuración y comprobaciones del servidor.
- [docs/UI_Funcional.md](docs/UI_Funcional.md): inventario de campos del sistema anterior,
  no guía visual ni garantía de vigencia fiscal.
- [AGENTS.md](AGENTS.md) y [ARQUITECTURA.md](ARQUITECTURA.md): reglas del proyecto.

Actualizar este reporte después de cada fase que cambie el estado o sus pendientes.
