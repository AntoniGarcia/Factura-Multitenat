# Auditoría del sistema — 19 de agosto de 2026

Revisión del código en `develop` contra las 31 capturas de `Sistema anterior/` y contra el
alcance acordado en `PROMPT-FASES-B.md`.

Acompaña a `docs/ESTADO-Y-CONTINUACION.md`: ese dice dónde estamos, este dice qué está mal.

**Cómo usar este documento.** Cada hallazgo tiene un identificador (`A1`, `B3`, `C7`…).
Cítalo en el prompt de la tarea en lugar de repetir la descripción. Las capturas ya están
resumidas aquí: **abrir las imágenes es innecesario y caro.**

---

## A. Defectos — arreglar antes de seguir construyendo

### A1 · La serie del borrador se pierde al reabrirlo · **datos** · `FormularioDeEmision.razor:352`

```csharp
_serie = _seriesActivas.FirstOrDefault();
```

Se asigna la primera serie activa sin mirar la que el borrador tenía guardada. Si la empresa
tiene `FAC` y `HON`, un borrador de honorarios se convierte en factura al recargar la página,
en silencio, y así se timbra.

La causa de fondo es que `ComprobanteDto` no expone `SerieId` — solo `Serie` (texto) y `Folio`,
y ambos van nulos mientras no se timbre, así que la pantalla no tiene de dónde recuperarla.

**Arreglo:** agregar `SerieId` a `ComprobanteDto`. Es tocar `Facturacion.Shared/`, que está
congelada: **commit dedicado, sin ningún otro cambio** (`CLAUDE.md` §10).

### A2 · El importe del renglón no es el importe del CFDI · **datos** · `FormularioDeEmision.razor:540`

```csharp
public decimal Importe => Math.Round(Cantidad * ValorUnitario - Descuento, 2);
```

Dos problemas:

1. En CFDI 4.0, `Importe = Cantidad × ValorUnitario`. El `Descuento` es un **atributo aparte**,
   no se resta del importe. El servidor lo hace bien (`MotorDeImpuestos` calcula
   `baseGravable = importe − descuento`), así que la pantalla muestra una cifra y el XML lleva
   otra. Con descuento distinto de cero, la columna «Importe» no cuadra con el PDF.
2. Redondea a 2 decimales; la regla del proyecto es calcular a 6 y presentar a 2.

**Arreglo:** `Importe = Redondear(Cantidad × ValorUnitario, 6)`, mostrado a 2, y el descuento
en su propia columna sin restarse.

### A3 · Renglones descartados sin avisar · `FormularioDeEmision.razor:482`

```csharp
_renglones.Where(r => r.ProductoId is not null)
```

Al guardar, todo renglón que no venga del catálogo de productos desaparece sin mensaje. Hoy
el efecto práctico es limitado (la rejilla solo deja capturar tras elegir producto), pero
significa que **no se puede facturar un concepto que no esté dado de alta**, y el día que se
permita, el filtro se los comerá callado.

**Arreglo:** o se valida y se avisa («el renglón 3 no tiene producto»), o se permite el
concepto libre. Ver `C3`.

### A4 · Borradores huérfanos que nadie recoge · `FormularioDeEmision.razor:338`

`OnParametersSetAsync` crea un borrador en el servidor **cada vez** que se abre
`/facturas/nueva`. Abrir la pantalla y cerrarla deja un comprobante en la base. No hay nada
que los purgue.

No consume folio ni timbre —eso está bien resuelto—, pero en un año de uso son miles de
renglones basura en la tabla que más se consulta.

**Arreglo:** un barrido que borre borradores vacíos con más de N días, al estilo de
`PurgaDeClavesIdempotencia` y `BarridoDeReservasDeTimbre`, que ya existen y sirven de molde.

### A5 · Cuatro fases sin una sola prueba

Las 88 pruebas no tocan `Emision`, `Listado`, `Cancelacion` ni `Pagos`. Justo donde está el
dinero: cálculo de saldos, parcialidades, motivos de cancelación, devolución del timbre.

**Mínimo antes de commitear:** saldo y parcialidad de pagos contra abonos concurrentes;
motivo 01 sin UUID sustituto rechazado; devolución del timbre al cancelar; el filtro global
de empresa aplicado a documentos y pagos.

---

## B. Huecos de campo — contra las capturas del sistema anterior

Numeración de las capturas de `Sistema anterior/`. **La tabla sustituye a la imagen.**

### B.1 · Emisión (§12, §13, §14) — `12`, `13`, `14`, `31`

| Campo del sistema viejo | En el nuevo | Nota |
|---|---|---|
| Tipo de Documento (desplegable) | **NO** — está fijo en `"I"` (ingreso) | **Hueco serio.** Sin tipo `E` no hay notas de crédito, que es la forma normal de corregir un importe. Ver `B.7`. |
| Folio | Sí, «Se asigna al timbrar» | Correcto: el folio no se muestra antes de timbrar. |
| Moneda / Tipo de Cambio | Sí | Tipo de cambio obligatorio si ≠ MXN. |
| **Tasa IVA (cabecera)** | **NO** | El viejo aplica una tasa global; el nuevo la toma de cada producto. Es mejor diseño, pero `ConfiguracionEmpresa.TasaIvaPorDefecto` **existe, se guarda, se valida y nadie la lee.** Ver `B.5`. |
| Clave / RFC / Nombre / Dirección del cliente | Sí | El nuevo muestra CP en vez de dirección completa: correcto, es lo que pide CFDI 4.0. |
| Botón «Cotización» | NO | Fuera del MVP por acuerdo. |
| Método de pago / Forma de pago / Uso CFDI | Sí, y se precargan del cliente | Mejor que el viejo. |
| Rejilla: Código, Cantidad, Unidad, Descripción, P. Unit., Importe, Descuento | Sí | Pero P. Unit. y Descripción son **solo lectura**. Ver `C2`. |
| Obj. Imp. por renglón | Sí (viene del producto) | No editable en el renglón. |
| **Cuenta Predial por renglón** | **NO** | Aparece impresa en el PDF de la captura `1`. Obligatoria para arrendamiento de inmuebles. |
| Observaciones / Condiciones de Pago | Sí | |
| Totales: Descuento, Subtotal, IVA, Total | Sí | Pero **no se recalculan en vivo**: «Se recalculan al guardar». Ver `C1`. |
| **Totales: ISH %** | **NO** | Impuesto local de hospedaje. Necesita el complemento `ImpLocal`, que está fuera del alcance. **Decisión pendiente:** ¿hay clientes hoteleros? Si sí, entra; si no, se documenta como fuera. |
| **Totales: IEPS %** | Parcial | El motor lo soporta (es un impuesto de tasa como cualquier otro), pero solo si el producto lo trae configurado. No hay tasa por defecto ni forma de agregarlo en el renglón. |
| **IVA Retenido % / ISR Retenido % (cabecera)** | **NO** | El viejo los aplica globalmente. El nuevo solo por producto. Honorarios y arrendamiento retienen siempre; hoy hay que configurarlo producto por producto. Ver `C5`. |
| Pestaña CFDI Relacionados: Tipo Relación, Serie, Folio, tabla con UUID | Sí | Completa. |
| Pestaña Información Global: Periodicidad, Mes, Año | Sí, habilitada solo con `XAXX010101000` | Correcto. |
| Buscador emergente al elegir clave de catálogo (`31`) | Sí | `BuscadorDeCliente`, `BuscadorDeProducto`, `BuscadorCatalogo`. |

### B.2 · Complemento de pagos (§27, §28, §29) — `27`, `28`, `29`

| Campo | En el nuevo | Nota |
|---|---|---|
| Pestaña **General**: Folio, No. de Operación, Fecha, Forma de Pago, Moneda, T.C., Importe, cliente | Sí | Completa. |
| Pestaña **Bancos**: RFC y cuenta del ordenante, RFC y cuenta del beneficiario | Sí, más `NomBancoOrdExt` | Mejor que el viejo. |
| **Pestaña SPEI**: TipoCadPago, CertPago, CadPago, SelloPago | **NO EXISTE** | La pestaña no está y los cuatro campos faltan en `PagoDto` y en la entidad `Pago`. Son opcionales en el estándar, pero el sistema viejo los captura, así que alguien los usa. **Preguntar antes de descartar.** |
| Rejilla: UUID, Serie, Folio, Moneda, TC, Método, No. Parcialidad, Saldo Anterior, Importe Pagado, Saldo Insoluto, Obj. Impuesto | Sí | El servidor recalcula saldo y parcialidad: mejor que el viejo, que confía en la pantalla. |
| **EquivalenciaDR** | **NO** | Obligatorio cuando la moneda del documento ≠ moneda del pago. Sin él, el PAC rechaza el pago en dólares de una factura en pesos. |
| Botón «Consulta» por Serie+Folio | Sí (`GET /api/pagos/por-pagar`) | |
| **Pestaña CFDI Relacionados del pago** | **NO** | Aparece en la captura. Poco usada; se puede posponer con nota. |

### B.3 · Cancelación (§30) — `30`

| Elemento | En el nuevo | Nota |
|---|---|---|
| Motivos 01 a 04, UUID sustituto obligatorio en el 01 | Sí | |
| Consulta de estatus ante el SAT | Sí (`POST /api/documentos/{id}/estatus-sat`) | |
| **Pantalla dedicada «Solicitudes de Cancelación»** | **NO** | Hoy solo hay una acción por renglón en el listado. La carpeta `Paginas/Cancelaciones/` sigue vacía. Falta la vista con Serie, Folio, Fecha Solicitud, UUID, Total, Estatus, Es cancelable, Estatus Cancelación, y el botón «Verificar Estatus SAT» que revisa **todas** de una pasada. Sin ella no hay forma de saber qué peticiones siguen esperando la aceptación del receptor. |

### B.4 · Listado de documentos — `1`

| Elemento | En el nuevo | Nota |
|---|---|---|
| Rango de fechas | Sí | |
| Búsqueda por RFC, nombre o folio | Sí | Mejor que el viejo. |
| Filtro por estatus | Sí | |
| **Filtro por cliente** | **NO** | El viejo tiene rango «Cliente Inicial / Final». |
| **Filtro por tipo de documento** | **NO** | El viejo tiene el desplegable «TODOS». |
| **Descarga de XML y PDF** | **NO HAY ENDPOINT** | Es el hueco más grave del sistema hoy: **una factura timbrada no se le puede entregar al cliente.** Alcance de B7. |
| **Exportación a CSV** | **NO** | Alcance de B7. Los listados de clientes, productos y movimientos de timbres ya la tienen: hay patrón que copiar. |
| **Vista previa del PDF** | **NO** | El viejo enseña la representación impresa dentro de la ventana principal. |

### B.5 · Configuración de empresa — `3`

`ConfiguracionEmpresa` guarda `TasaIvaPorDefecto`, `TasaRetencionIvaPorDefecto` y
`TasaRetencionIsrPorDefecto`. Se validan en `ServicioDeEmpresa` (líneas 175-184), se muestran
y se editan en `ConfiguracionDeLaEmpresa.razor` (líneas 68-70)… y **ningún otro archivo las
lee**. Son tres campos que el usuario configura y que no producen ningún efecto.

`DiasAvisoCaducidadCertificado` sí se usa: `ServicioDeCsd` y el tablero avisan del CSD por
vencer. Bien resuelto.

Del viejo faltan además los defaults de **ISH %** e **IEPS %** (ver `B.1`).

Lo que el viejo tiene y el nuevo **descarta a propósito**, correctamente: la configuración SMTP
del cliente (el SaaS envía con remitente propio y `Reply-To` a la empresa) y el usuario/clave
de Web Services (los sustituye la integración con el PAC).

### B.6 · Envío por correo (B6, sin empezar) — `1`

La captura `1` da los campos exactos que hay que reproducir, en el panel «Parámetros de envío
CFD»:

- **Para** — precargado con el correo del cliente, editable, admite varios.
- **Asunto** — precargado con «Documento CFDI», editable.
- **Mensaje** — cuerpo libre.
- **Casilla «Incluir archivo XML»** — el PDF va siempre, el XML es opcional.
- **Botón «Enviar a cuenta única»** — copia a la casilla contable de la empresa.
- **Lista de adjuntos**, con el nombre generado:
  `HON_13_URE180429TM6_3062025_163556.xml` → `SERIE_FOLIO_RFCRECEPTOR_DDMMAAAA_HHMMSS`.
  **Conviene conservar esa convención de nombres**: los contadores de los clientes ya tienen
  sus carpetas y sus reglas de archivo montadas sobre ella.

Falta además, y no está en el viejo: **registro de envíos y reenvío desde el listado**, que
sí está en el alcance acordado de B6.

### B.7 · Decisiones que hay que tomar, no suponer

1. **Notas de crédito (tipo `E`).** Sin ellas no hay forma de corregir un importe salvo
   cancelar y reexpedir. ¿Entran al MVP?
2. **ISH / impuestos locales.** Exigen el complemento `ImpLocal`. ¿Hay clientes hoteleros?
3. **Pestaña SPEI del pago.** ¿La usan de verdad o quedó de una versión anterior?
4. **Cuenta predial por renglón.** ¿Hay clientes de arrendamiento de inmuebles?
5. **Tanda de 50 timbrados en sandbox.** La regla del plan es explícita y no consta que se
   haya corrido.

---

## C. Usabilidad — lo que haría el sistema cómodo de usar

Ordenado por relación valor/esfuerzo. `C1` a `C4` estaban en el alcance de B2 y no se
construyeron.

### C1 · Totales en vivo · *estaba en el alcance de B2*
Hoy dice «Se recalculan al guardar». El capturista no ve el total hasta que hace un viaje al
servidor. El motor de impuestos es una clase pura sin dependencias de EF ni HTTP: **compila a
WebAssembly tal cual**. Se referencia desde el `Client`, se recalcula en cada tecla, y el
servidor sigue siendo la verdad al guardar. Es la mejora de mayor efecto y de las más baratas.

### C2 · Precio unitario y descripción editables en el renglón
El sistema viejo los deja editar. El nuevo los pone de solo lectura, tomados del producto. Un
precio negociado o una descripción con el detalle del mes («Arrendamiento octubre 2026») obligan
hoy a modificar el catálogo. El producto debe ser el **valor inicial**, no una atadura.

### C3 · Concepto libre, sin producto de catálogo
Cobros de una sola vez que no merecen alta en el catálogo. Necesita clave ProdServ, clave de
unidad, descripción, precio y objeto de impuesto capturados a mano, con el `BuscadorCatalogo`
que ya existe. Resuelve además `A3`.

### C4 · Captura por teclado · *estaba en el alcance de B2, textual*
> «Optimizado para teclado: un capturista de diez renglones no debe tocar el ratón.»

No hay una sola tecla implementada. Lo mínimo:

| Tecla | Acción |
|---|---|
| `Enter` | Siguiente campo del renglón; en el último, crea renglón nuevo y enfoca el buscador |
| `Tab` / `Shift+Tab` | Navegación normal, con orden de tabulación explícito |
| `Esc` | Cierra el buscador sin elegir |
| `Ctrl+G` | Guardar borrador |
| `Ctrl+Enter` | Generar factura (con la confirmación de `C11`) |
| `Alt+↓` | Abre el buscador del campo enfocado |
| `Ctrl+Supr` | Borra el renglón actual |

En los buscadores, `Enter` sobre el texto escrito debe aceptar la primera coincidencia sin
abrir el modal.

### C5 · Retenciones desde la cabecera
Los tres valores por defecto ya están en `ConfiguracionEmpresa` sin usarse (`B.5`). Un control
«Aplicar retención de IVA e ISR a todos los renglones», precargado con esos valores, cierra el
hueco y le da sentido a la configuración. Es lo que hace el sistema viejo y es el caso normal
de honorarios y arrendamiento.

### C6 · Autocompletado en línea en vez de modal
`BuscadorDeCliente` y `BuscadorDeProducto` abren una ventana. Un `MudAutocomplete` con
`DebounceInterval` que busque por clave, RFC y nombre a la vez, y muestre `clave — nombre — RFC`,
ahorra dos clics por renglón. La ventana se conserva como salida secundaria (`Alt+↓`), que es lo
que pide la captura `31`.

### C7 · Duplicar factura
El caso más frecuente de este negocio es facturar lo mismo cada mes al mismo cliente
(«ARRENDAMIENTO OCTUBRE» en las capturas). Un botón «Nueva a partir de esta» en el listado
convierte diez campos en uno. **No existe en el sistema viejo: es una ventaja competitiva
directa, y cuesta poco.**

### C8 · Autoguardado del borrador
Cada 30 segundos y al cambiar de pestaña. Hoy, cerrar la pestaña pierde la captura. El endpoint
`PUT /api/documentos/{id}` ya hace exactamente eso.

### C9 · Validar antes de gastar el timbre
Un timbre cuesta dinero y el PAC rechaza por cosas que se pueden ver antes de llamarlo: RFC con
dígito verificador malo, CP que no existe en `c_CodigoPostal`, combinación régimen ↔ uso CFDI
incompatible, tipo de cambio faltante. `Rfc` y `CompatibilidadUsoCfdi` ya tienen la lógica: falta
enseñarla en la pantalla, en el momento de capturar, en vez de después del rechazo.

### C10 · Traducir los errores del PAC
«CFDI40147» no le dice nada a nadie. Una tabla con los veinte códigos más frecuentes,
traducidos a lenguaje llano y con el campo que hay que corregir, evita la llamada a soporte.
`MapeoDeErrores` ya es el lugar.

### C11 · Confirmación antes de timbrar
Timbrar cuesta dinero y es irreversible. Un diálogo con receptor, total, serie y timbres
restantes. `IServicioDeConfirmacion` ya existe y se usa en otras cuatro pantallas.

### C12 · Timbres restantes visibles al emitir
El indicador está en el tablero. Enterarse de que no quedan timbres al dar clic en «Generar
factura» es la peor manera de enterarse.

### C13 · Qué hacer justo después de timbrar
Hoy la pantalla se pone de solo lectura y ofrece «Nueva factura» o «Ir al inicio». Debería
ofrecer, en ese orden: **descargar PDF**, **descargar XML**, **enviar por correo**, y luego
nueva factura. Depende de `B.4` y `B.6`.

### C14 · Búsqueda global (`Ctrl+K`)
Pegar un UUID, un folio o un RFC y llegar al documento. En un sistema con listados en cinco
pantallas distintas, es el atajo que más se usa.

### C15 · Estados vacíos con acción
«No hay documentos que coincidan» debería ofrecer «Crear la primera factura» cuando la empresa
no tiene ninguna. Vale para clientes, productos y series.

---

## D. Refactorizaciones

Ninguna es urgente. Ordenadas por lo que devuelven.

### D1 · Tres buscadores casi iguales
`BuscadorCatalogo`, `BuscadorDeCliente` y `BuscadorDeProducto` repiten estructura, estado y
teclado. Un genérico `Buscador<T>` con una fuente de datos inyectada deja uno. Además, hacer
`C6` tres veces en vez de una es la forma segura de que las tres se comporten distinto.

### D2 · `ImportadorCatalogosSat.cs` — 915 líneas
El archivo más largo del proyecto. Es una secuencia de importadores por catálogo con la misma
forma: leer hoja, mapear filas, conciliar contra la base. Un `ImportadorDeHoja<T>` con la
diferencia en un delegado deja unas 200 líneas.

### D3 · Validación de la mitad B fuera de los servicios
La mitad A tiene `ValidadorDeCliente` y `ValidadorDeProducto`, separados del servicio. La mitad
B no siguió el patrón: `ServicioDePagos` (477 líneas) y `ServicioDeEmision` (432) llevan la
validación mezclada con la persistencia. **El molde ya está en el repositorio; solo hay que
aplicarlo.** Y una vez separada, se puede probar sin base de datos, que es lo que falta en `A5`.

### D4 · `ProveedorPacSwSapien.cs` — 566 líneas
Tres cosas en un archivo: cliente HTTP con reintentos, mapeo de la respuesta del PAC, y
traducción de sus errores. Separarlas facilita cambiar de PAC —que es exactamente para lo que
se creó `IProveedorPac`— y probar el mapeo sin red.

### D5 · Formularios grandes
`FormularioDeEmision.razor` (542) y `FormularioDePago.razor` (449) mezclan marcado y ~280
líneas de `@code`. Mover el código a `.razor.cs` y extraer la rejilla a un componente propio
(`RejillaDeConceptos`, `RejillaDeDocumentosPagados`) los deja legibles y hace `C4` posible sin
tocar el marcado.

### D6 · `ServicioDeInvitaciones.cs` — 587 líneas
Lleva dentro las plantillas de correo. Sacarlas a su propio archivo es media hora y le sirve a
B6, que va a necesitar plantillas igual.

### D7 · Metadatos del PDF
`GeneradorDePdfCfdi` no fija `DocumentMetadata`, así que el PDF sale con el productor por
defecto de QuestPDF y sin título. Conviene poner título = `SERIE-FOLIO`, autor = razón social
del emisor, asunto = UUID, y limpiar productor y creador. **No hay marca de agua**: la licencia
Community está declarada en `DocumentosModule.cs:39`, que es justamente lo que la quita.

---

## E. Limpieza — archivos y metadatos

### E1 · Borrar

| Qué | Tamaño | Por qué |
|---|---|---|
| `Sistema de Facturacion/` | 12 KB | Bóveda de Obsidian recién creada que se coló en la carpeta. Solo tiene las dos notas de bienvenida del programa. **No es código, no es documentación, no es nada.** |
| 34 archivos `.gitkeep` | — | De 37, solo 3 siguen haciendo falta: `Client/Componentes/Documentos/`, `Client/Paginas/Cancelaciones/` y `Client/Theme/`, que son las únicas carpetas todavía vacías. Los otros 34 están en carpetas ya pobladas y solo estorban en los listados. |
| `.vs/` | 2.9 MB | Caché de Visual Studio. Ya está gitignoreado; borrarlo del disco no rompe nada, se regenera. |

```bash
# desde la raíz, en Windows (Git Bash o WSL)
rm -rf "Sistema de Facturacion" .vs
find . -name .gitkeep -not -path "./.git/*" | while read f; do
  d=$(dirname "$f")
  [ "$(ls -A "$d" | grep -v '^.gitkeep$' | wc -l)" -gt 0 ] && git rm --cached "$f" && rm "$f"
done
```

### E2 · `AGENTS.md` — 17 KB de duplicado exacto

Es `CLAUDE.md` con la primera línea cambiada (verificado con `diff`: una sola línea distinta).
Dos archivos de 17 KB que hay que mantener sincronizados a mano, y que un asistente puede
acabar leyendo los dos.

Lo mejor es reducirlo a un puntero:

```markdown
# AGENTS.md

El contexto del proyecto vive en `CLAUDE.md`. Este archivo existe solo para las
herramientas que buscan `AGENTS.md` por convención. No lo edites: edita `CLAUDE.md`.
```

### E3 · Agregar al `.gitignore`

Ninguna de estas dos carpetas está ignorada, y ambas aparecen como sin seguimiento:

```gitignore
# Capturas de referencia del sistema de escritorio anterior. No son código y pesan 2.2 MB;
# su inventario de campos está en docs/AUDITORIA-2026-08-19.md §B.
/Sistema anterior/

# Bóveda de Obsidian que se coló en la carpeta del proyecto. Marcada para borrarse.
/Sistema de Facturacion/
```

### E4 · Lo que NO hay que borrar, aunque lo parezca

| Qué | Por qué se queda |
|---|---|
| `CatalogosSAT/` — 56 MB | Ya está gitignoreada: no entra al repositorio y no gasta un token. Borrarla no ahorra nada y rompe el arranque local. |
| `CatalogosSAT/xslt/` — 26 XSLT de complementos fuera de alcance | **Trampa.** `cadenaoriginal_4_0.xslt` los importa **todos** por URL; falta uno y la cadena original no compila. Se quedan los 26, incluidos los de nómina. |
| `CatalogosSAT/*Nomina*` (`.xls`, `.pdf`, `.xsd`) — 2.6 MB | Nómina está fuera del MVP, pero son datos gitignoreados: no cuestan nada en el repositorio, y `catNomina.xsd` cuelga de la cadena de importaciones. Riesgo mayor que el beneficio. |
| `Facturacion.Server/almacenamiento/` | **Nunca borrar en la máquina de desarrollo.** Tiene la llave maestra de Data Protection y el CSD cifrado. Sin la llave, el CSD cargado queda ilegible y hay que volver a subirlo. Ya está gitignoreada, correctamente. |
| `Shared/Comun/DobleDePrueba.cs` y `Infra/Integracion/VerificacionDeContratos.cs` | Ya no hay dobles de prueba, así que parecen muertos. Son la guardia que impide que vuelva a haberlos: `VerificacionDeContratos` tumba el arranque si un doble llega a producción. Cuestan 80 líneas. |

### E5 · Metadatos y marcas de agua — nada que limpiar, salvo un detalle

Revisado a fondo, porque era una de las preocupaciones:

- **Las 31 capturas no tienen marca de agua ni EXIF.** Solo llevan cabecera JFIF y un perfil
  ICC de ~600 bytes cada una, que es lo normal de una captura de pantalla de Windows. Nada que
  quitar. Lo que sí conviene es sacarlas del repositorio (`E3`) y **no abrirlas** (ver la tabla
  de «qué NO leer» del documento de estado): el costo no está en los metadatos, está en que son
  imágenes.
- **El PDF no lleva marca de agua.** `DocumentosModule.cs:39` declara la licencia Community de
  QuestPDF, que es precisamente lo que la evita. Confirmado en el código.
- **El PDF sí sale sin metadatos propios**, con el productor por defecto de la librería. Ver
  `D7`. Es cosmético, pero es un documento fiscal que se guarda cinco años.
- **El XML no lleva nada de más.** `GeneradorDeXmlCfdi` solo escribe los nodos del estándar.

---

## Resumen ejecutivo

**Lo que está bien y no hay que tocar:** la arquitectura de tenencia y seguridad, el motor de
impuestos, la generación y validación del XML, el timbrado en tres pasos con conciliación, y
los catálogos de clientes y productos —que además son **más completos que los del sistema
viejo** (residencia fiscal y registro tributario extranjero, que la versión de escritorio no
tenía).

**Lo que bloquea la entrega, en orden:**

1. **No se puede descargar el XML ni el PDF** (`B.4`). Se emiten facturas que no se le pueden
   dar al cliente. Es el hueco más grave del sistema hoy.
2. **No hay envío por correo** (`B.6`, fase B6 sin empezar).
3. **Dos defectos de datos** en el formulario de emisión (`A1`, `A2`).
4. **Cuatro fases construidas sin pruebas** (`A5`) y sin commitear.

**Lo que hay que decidir antes de seguir:** notas de crédito, ISH, pestaña SPEI, cuenta predial
y la tanda de 50 timbrados (`B.7`). Son cinco preguntas al cliente, no cinco decisiones
técnicas.

**Lo que convierte «funciona» en «se usa a gusto»:** `C1` totales en vivo, `C4` captura por
teclado y `C7` duplicar factura. Los tres juntos son menos trabajo que cualquiera de las fases
que faltan, y son la diferencia que va a notar el capturista.
