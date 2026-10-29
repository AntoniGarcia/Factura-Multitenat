# AGENTS.md — Contexto permanente del proyecto

Lo lees al inicio de **cada** sesión, completo, antes de escribir una línea.
No lo modifiques salvo que te lo pida explícitamente.

---

## 1. Qué es esto

SaaS de facturación electrónica **CFDI 4.0** para México. Migración de un sistema de
escritorio existente (GenCFDI V4.2).

La especificación funcional del sistema viejo está en `docs/UI_Funcional.md`. Se usa
como **inventario de campos y de reglas de negocio**, nunca como referencia visual:
el diseño del sistema viejo se descarta por completo.

Multiempresa: una **cuenta** contrata el servicio y puede tener varias **empresas
emisoras** (por ejemplo una llantera y una cementera del mismo dueño). Cada empresa
aísla por completo sus clientes, productos, series de folios y comprobantes.

Usuarios reales: contadores y capturistas en jornadas largas. Uso principal en
escritorio; la interfaz debe ser responsiva porque en móvil se consulta, se reenvía
y se emite una factura rápida.

**Fecha límite del MVP: 5 de diciembre de 2026.**

---

## 2. Mi mitad del proyecto

**Desde el 17 de agosto de 2026 trabajo las dos mitades.** La mitad A —plataforma,
identidad y catálogos— quedó terminada en la fase 9, y decidí absorber también la
**mitad B** —emisión, motor de impuestos, timbrado, PAC, XML, PDF, correo, listado de
documentos, cancelación y complemento de pagos—. Las dos están definidas en
`REPARTO-EQUIPO.md` §3.

El plan de la mitad B está en `PROMPT-FASES-B.md`, y su sección «Si Luis absorbe esta
mitad» da el orden mínimo. Ojo: ese orden y sus recortes están calculados para absorber
el **15 de octubre** con siete semanas. La absorción se adelantó, así que hay cerca de
quince semanas hasta el 5 de diciembre y esos recortes no están decididos todavía.

Lo que **sigue** vigente de la frontera, aunque ya no separe a dos personas:

- `Facturacion.Shared/Contratos/` **está congelada**. Cambiar una firma o un DTO de ahí
  exige acuerdo explícito y un commit dedicado que no traiga ningún otro cambio
  (`Contratos/LEEME.md`). Que ahora las dos mitades sean mías no es motivo para
  aflojarla: es lo que mantiene el diseño desacoplado y lo que permitiría repartir otra
  vez el trabajo.
- La mitad B **consume** lo que produce la A, nunca al revés. Si algo de Documentos
  necesita un cambio en Plataforma, es señal de que el contrato quedó corto: **dímelo**
  en vez de resolverlo cruzando la frontera por dentro.
- Nada de dobles de prueba de los contratos de la mitad A: ya tienen implementación
  real. `VerificacionDeContratos` tumba el arranque si un doble llega a producción.

---

## 3. Stack y estructura

- .NET 10, C# 14
- **Blazor WebAssembly configurado como PWA**
- ASP.NET Core 10 Web API
- SQL Server, EF Core 10
- MudBlazor, siempre sobre variables CSS propias
- Serilog + Sentry, con depuración obligatoria de datos sensibles

Tres proyectos:

```
Facturacion.Client    Blazor WebAssembly (PWA)
Facturacion.Server    ASP.NET Core Web API — además sirve los estáticos del Client
Facturacion.Shared    biblioteca de clases: DTOs y contratos
```

La plantilla `blazorwasm --hosted` **ya no existe** desde .NET 8. La estructura se arma
a mano:

```bash
dotnet new blazorwasm --pwa -o Facturacion.Client
dotnet new webapi              -o Facturacion.Server
dotnet new classlib            -o Facturacion.Shared
```

En el `Server`: paquete `Microsoft.AspNetCore.Components.WebAssembly.Server`,
referencia de proyecto al `Client`, y en el pipeline `UseBlazorFrameworkFiles()`,
`UseStaticFiles()` y `MapFallbackToFile("index.html")`. Un solo origen para todo, lo
que además permite que la cookie de refresh sea `SameSite=Strict` sin fricción.

**Verifica con `dotnet new list` antes de suponer qué plantillas hay.** Si algo no
coincide con lo aquí descrito, dímelo en vez de inventar una alternativa.

### Consecuencia central de WebAssembly

El código del `Client` viaja al navegador y **es legible**. Ninguna validación fiscal
seria, ningún precio y ningún secreto puede vivir solo ahí. La validación en el `Client`
existe para dar retroalimentación rápida al capturista; la que cuenta es la del `Server`,
y se ejecuta siempre, sin excepción.

---

## 4. Seguridad — reglas no negociables

**Identidad**

- El access token JWT vive **solo en memoria**. Nunca `localStorage` ni `sessionStorage`.
  Duración 15 minutos.
- El refresh token va en cookie `HttpOnly`, `Secure`, `SameSite=Strict`, con **rotación
  en cada uso**: el token usado se invalida y se emite uno nuevo. Si llega un refresh
  token ya consumido, se invalida **toda la familia** de esa sesión y se registra el
  evento — es la señal de que alguien robó la cookie.
- El checkbox de "mantener sesión iniciada" se implementa **solo** con la duración de
  esa cookie: 12 horas sin marcar, 30 días marcado. No se guarda nada más en el navegador.
- Al arrancar la aplicación hay un *gate*: se intenta `/auth/refresh` contra la cookie
  y no se renderiza nada hasta que resuelva. Sin esto, cada F5 muestra interfaz de
  invitado por un instante.
- Almacén de usuarios: **ASP.NET Core Identity** con `Guid` como clave, usado solo como
  almacén y como motor de hash y de bloqueo por intentos. La emisión de JWT es propia;
  no se usan los endpoints de Identity API.

**Tenencia**

- La **empresa activa es un claim del token**, no un parámetro. Cambiarla pide un token
  nuevo a `/auth/cambiar-empresa`, que verifica que el usuario tenga acceso a esa empresa
  dentro de su cuenta.
- El `Client` **nunca** envía un identificador de empresa en ninguna otra petición.
  Si ves un endpoint que recibe `empresaId` en la ruta, la query o el cuerpo, está mal
  diseñado: dímelo.

**Autorización**

- Por **permiso**, nunca por rol. Los permisos son exactamente estos:
  `timbrar`, `cancelar`, `administrar_usuarios`, `comprar_timbres`, `ver_reportes`,
  `configurar_empresa`.
- Cada permiso es una política de ASP.NET Core. Los endpoints se anotan con la política.
- **Ocultar un botón no es proteger.** El `Server` rechaza igual la operación, siempre.

**Datos y transporte**

- Los precios de los paquetes de timbres **los devuelve el servidor**. El `Client` solo
  manda el id del paquete. Un precio que viaja desde el navegador es un precio que el
  usuario puede editar.
- Archivos (logo, XML, PDF, CSD) fuera de `wwwroot`, servidos por endpoint autorizado
  que valida la empresa del solicitante contra la del archivo.
- Errores en **Problem Details (RFC 7807)** con `traceId`, visible en pantalla para
  soporte. Nunca se filtra al cliente el detalle de una excepción.
- Encabezado `Idempotency-Key` obligatorio en todo `POST` que cobre o timbre. El servidor
  guarda la clave con su respuesta durante 24 horas y devuelve la misma respuesta ante
  una repetición.
- Cabeceras de seguridad en todas las respuestas: `Content-Security-Policy`,
  `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, HSTS.
  La CSP de una PWA con WebAssembly necesita `wasm-unsafe-eval`; no aflojes más que eso.

**Secretos y certificados**

- Los archivos `.cer` y `.key` del CSD y la contraseña de la llave privada se guardan
  **cifrados**, con ASP.NET Core Data Protection y una clave maestra fuera del
  repositorio. Nunca en claro, nunca en la base sin cifrar, nunca en un log.
- El SMTP es del SaaS, no del cliente (ver §6). No se guardan contraseñas de correo
  de terceros.

**Service worker**

- **Jamás** cachea respuestas de `/api/`. Solo el app shell.
- Existe `/api/version`. Si la versión del servidor no coincide con la compilada en el
  cliente, se muestra un aviso no descartable y se fuerza la recarga. Un catálogo del
  SAT cacheado y viejo produce comprobantes mal emitidos: esto no es una comodidad, es
  una salvaguarda.

---

## 5. Dominio — reglas no negociables

- **Aislamiento:** toda tabla con datos de empresa lleva `EmpresaId` y se filtra con
  *query filter* global en EF Core, alimentado desde el claim. Nunca se filtra en el
  componente ni en el controlador. Las tablas de Identity y los catálogos del SAT son
  la excepción: no llevan `EmpresaId`.
- **Inmutabilidad:** al timbrar se copian dentro del comprobante los datos fiscales del
  emisor, del receptor y de cada concepto. Los reportes leen esas copias, no los
  catálogos. Que un cliente cambie de domicilio no puede alterar una factura de hace
  dos años.
- **Nada se borra:** solo `Activo = false`. Única excepción: un borrador nunca timbrado.
- **Folios:** se asignan con `UPDATE ... WITH (UPDLOCK)` dentro de un procedimiento
  almacenado. Nunca `SELECT MAX(Folio)+1`. No se muestran antes de timbrar. Si el
  timbrado falla después de tomar folio, el comprobante queda en `error` con ese folio
  apartado; no se recicla.
- **Nunca una llamada HTTP dentro de una transacción de base de datos.**
- **Estatus del comprobante, solo estos:** `borrador`, `timbrando`, `timbrado`, `error`,
  `cancelado`, `en_cancelacion`.
- **Índices únicos con filtro:** el RFC de cliente es único **por empresa**, no en toda
  la base, y se excluyen los genéricos `XAXX010101000` y `XEXX010101000`, que pueden
  repetirse.
- **Dinero con `decimal(18,6)`.** Cálculo con 6 decimales, presentación con 2.
  Nunca `double`, nunca `float`.
- **Fechas en UTC** en la base; se convierten al mostrar según el huso del lugar de
  expedición de la empresa.
- **Bitácora:** toda operación que cambie datos fiscales o de facturación deja registro
  con usuario, empresa, momento y valores anterior y nuevo.

---

## 6. Decisiones de alcance ya cerradas

**Dentro del MVP:** multiempresa, catálogos del SAT, clientes, productos, emisión de
factura estándar, timbrado, cancelación con motivos 01–04, PDF con QR y cadena original,
envío por correo, complemento de pagos, bolsa de timbres y membresías.

**También dentro de la entrega, agregados después:** Carta Porte 3.1 y sus catálogos
(§10, §11, §26 del documento funcional), Comercio Exterior 2.0 (§16–§19), Notaría
(§20–§25) y Constructoras (§15). La decisión original los dejaba en fase 2 —cada
complemento del SAT tiene su propio esquema, validación y catálogo—; después se pidió
implementarlos antes del 5 de diciembre. Que el código exista no significa que estén
validados: sus flujos fiscales completos siguen pendientes de prueba manual
(`REPORTE_ESTADO_SISTEMA.md`).

**Fuera del MVP, fase 2:** Cotizaciones, Addendas, complemento INE, inventario.

Las licencias por empresa (`LicNotarios`, `LicObras`, `LicComercio`) habilitan por
empresa las variantes de Notaría, Constructoras y Comercio Exterior. `LicINE` sigue
modelada para que el complemento INE sea agregar un módulo y no rehacer el esquema.

**Correo:** todo sale de un remitente propio del SaaS con dominio verificado (SPF, DKIM,
DMARC), con `Reply-To` apuntando al correo de la empresa emisora. **No** se guarda la
configuración SMTP del cliente. El sistema viejo la pedía (§3 del documento funcional);
en un SaaS multiempresa eso significa custodiar contraseñas de correo reutilizables de
decenas de empresas, y no vale el riesgo. SMTP propio por empresa queda como fase 2
opcional, con cifrado por empresa.

**Validación de RFC:** formato del anexo 20 más dígito verificador, calculado en el
servidor. La consulta al servicio de validación del SAT queda para fase 2.

**Pruebas:** hay pruebas automatizadas obligatorias en tres puntos, y solo en tres:
validación de RFC y dígito verificador, reserva concurrente de folios, y aritmética de
la bolsa de timbres. El resto se prueba a mano. No se invierten horas en cobertura
decorativa.

---

## 7. Catálogos del SAT

`c_ClaveProdServ` tiene decenas de miles de filas y `c_CodigoPostal` cientos de miles.
En WebAssembly **no viajan al cliente**. Se resuelven con autocompletado contra el
servidor: mínimo 3 caracteres, *debounce* de 300 ms, tope de 50 resultados, índice de
texto completo en SQL Server.

Solo los catálogos chicos y estables se precargan y se cachean en el `Client`:
`c_UsoCFDI`, `c_RegimenFiscal`, `c_MetodoPago`, `c_FormaPago`, `c_Moneda`,
`c_ObjetoImp`, `c_TipoRelacion`, `c_Periodicidad`, `c_Meses`, `c_Exportacion`,
`c_TipoDeComprobante`, `c_Impuesto`, `c_TipoFactor`.

Los catálogos se versionan: cada carga registra la versión publicada por el SAT y su
fecha. El `Client` valida la versión contra el servidor al arrancar.

El sistema viejo obliga a elegir claves desde un modal de búsqueda (§31 del documento
funcional). En el rediseño eso se convierte en un **autocompletado en línea** que muestra
`clave — descripción`, con el modal disponible como alternativa para quien prefiere
teclado y rejilla. La regla de fondo se conserva: **una clave de catálogo nunca se
escribe a mano libre.**

### Reglas de CFDI 4.0 que rompen timbrados y hay que validar en el alta de cliente

Estas son la causa más común de rechazo del PAC y viven en mi mitad:

- `Nombre` del receptor debe coincidir **exactamente** con la Constancia de Situación
  Fiscal: en mayúsculas, sin acentos y **sin el régimen de capital** (sin "S.A. de C.V.",
  "S. de R.L.", etc.). Normaliza al guardar y avisa al usuario de lo que se quitó.
- `DomicilioFiscalReceptor` (código postal) es **obligatorio** y debe ser el de la
  constancia, no el de entrega.
- `RegimenFiscalReceptor` es **obligatorio**.
- `UsoCFDI` debe ser compatible con el régimen fiscal del receptor y con su tipo de
  persona. El catálogo `c_UsoCFDI` trae esa matriz de compatibilidad: cárgala y valídala,
  no la ignores.
- Para el RFC genérico `XAXX010101000` el nombre debe ser exactamente
  `PUBLICO EN GENERAL`, el régimen `616` y el uso `S01`.

---

## 8. Sistema de diseño

Herramienta de trabajo densa en datos. Sin ilustraciones, sin degradados, sin sombras
salvo en menús flotantes. La pantalla se optimiza para ver muchos renglones, no para
verse bonita en una captura.

Todos los colores se definen como **variables CSS semánticas**. Ningún componente
menciona un color literal, nunca. Tres temas: claro, oscuro ergonómico (grises y azules
profundos, jamás negro puro) y cálido/sepia.

Tema claro: fondo `#F7F8FA`, superficie `#FFFFFF`, borde `#E3E6EB`, texto `#1A1D23`
y `#5A6270`, acento azul tinta `#1B3A6B`. Estados: timbrada `#1E7A45`, borrador
`#6B7280`, cancelada `#B42318`, advertencia `#B54708`.

Tipografía: **Inter** para interfaz (pesos 400 y 500); **IBM Plex Mono** con cifras
tabulares para RFC, UUID, folios e importes — sin cifras tabulares las columnas de
dinero no alinean y el contador no puede leerlas de un vistazo.

Radio de esquinas 6px en todo. Filas de tabla 40px, controles 36px.

Textos en **español de México**, en *sentence case* (no Title Case, no MAYÚSCULAS).

Un solo `MainLayout`. Las páginas aportan **solo** el contenido del área principal —
nunca su propio encabezado, su propia barra ni su propio esqueleto. El selector de
empresa activa vive en la barra superior y en ningún otro lugar. En móvil el menú lateral
se convierte en barra inferior y las tablas en tarjetas apiladas.

Accesibilidad mínima obligatoria: todo se puede operar con teclado, el foco es visible,
las tablas tienen encabezados asociados, y el contraste cumple AA en los tres temas.
Un capturista que factura ocho horas usa teclado, no ratón.

---

## 9. Rendimiento de WebAssembly

La primera carga de una PWA con WASM y MudBlazor es de varios MB. Es aceptable para
alguien que abre la aplicación una vez al día, pero es una decisión consciente, no un
accidente. Se mitiga así:

- `PublishTrimmed=true` y compresión Brotli en el `Server`. **Verifica que los archivos
  `.br` se estén sirviendo de verdad**: es el error más común del despliegue, y si no
  se sirven, la descarga se triplica.
- Carga diferida de ensamblados (`BlazorWebAssemblyLazyLoad`) para las áreas pesadas.
- `Virtualize` en toda tabla que pueda pasar de 100 renglones.
- Nunca traer un catálogo completo al cliente.

---

## 10. Cómo quiero trabajar contigo

- **Por fases, con parada y revisión entre cada una.** Al terminar una fase te detienes,
  me dices qué construiste y qué quedó pendiente, y esperas. No empiezas la siguiente
  sin que te lo pida.
- **No generes código de fases futuras.** Ni "de una vez", ni "para dejarlo listo".
- **Si algo de lo que te pido tiene un problema, dímelo antes de construirlo.**
  Prefiero una objeción de tres líneas que una corrección de tres horas.
- Nada de `TODO` ni de métodos que devuelven datos falsos en código que se sube. Si algo
  no se puede terminar, se queda fuera y me lo dices.
- Comentarios en español, solo donde una decisión no sea obvia. Nada de comentarios que
  repiten lo que dice la línea de abajo.
- Antes de crear un componente, revisa `Client/Componentes/Comunes/`. Si algo parecido
  ya existe, se extiende; no se duplica.
- Al terminar cada fase, `dotnet build` debe pasar sin advertencias nuevas y la
  aplicación debe correr. Una fase que no compila no está terminada.

---

## 11. Cambios de funcionalidad posteriores

Lo que está en esta sección **prevalece** sobre las secciones anteriores cuando las
contradice. Las reglas originales se conservan arriba como historia de la decisión.

**5 de octubre de 2026 — SMTP propio por empresa** (modifica §4 «Secretos y
certificados» y §6 «Correo»).

- Una empresa puede configurar su propio servidor SMTP para enviar **sus comprobantes**.
  Los correos del sistema —registro, contraseñas, invitaciones— siempre salen del SaaS.
- La contraseña se guarda cifrada con Data Protection, con un propósito propio por empresa
  y distinto al del CSD. Nunca vuelve al `Client`, nunca se registra y nunca entra a la
  bitácora.
- El servidor capturado se valida en el `Server` antes de conectar: solo puertos 465 y
  587, nunca una dirección privada, de loopback o local. El nombre se resuelve en el
  servidor y la conexión va a la IP ya validada.
- Si el SMTP de la empresa falla, el envío falla con un error claro; **no** se reenvía en
  silencio por el SMTP del SaaS.

**9 de octubre de 2026 — Permisos por sección y titular de la cuenta** (modifica §4
«Autorización»).

- Los permisos asignables son dieciséis, agrupados por sección:
  - Documentos: `ver_documentos` (listado y descarga, solo consulta).
  - Emitir: `emitir_factura`, `emitir_notaria`, `emitir_carta_porte`,
    `emitir_comercio_exterior`, `emitir_obra`, `emitir_pago`, `cancelar` y `enviar_correo`.
    Reemplazan a `timbrar`.
  - Catálogos: `administrar_clientes`, `administrar_productos`, `administrar_vehiculos` y
    `administrar_figuras`.
  - Tienda: `comprar_timbres`.
  - Empresa: `mi_empresa` (datos y certificados) y `configuracion` (general, correo,
    impuestos, logo, perfil de Notaría y series y folios). Reemplazan a `configurar_empresa`.
- `ver_reportes` se retira mientras no exista ningún reporte.
- No hay dependencias entre casillas. Cualquier permiso de Emitir abre también el listado y
  la descarga, y la consulta de los catálogos que necesita para emitir. Emitir una variante
  con licencia (Notaría, Comercio Exterior, Obra) exige además la licencia de la empresa.
- Cada cuenta tiene un **titular**: el usuario que la registró o el del sembrado. Lleva en el
  token todos los permisos de cualquier empresa de su cuenta, sin guardarlos, y es el único
  que administra usuarios y da de alta empresas. `administrar_usuarios` deja de ser una
  casilla. Nadie modifica al titular, y los usuarios que él crea nunca administran usuarios.
- Jerarquía: operador principal (1) → operadores (1.1) → titular (2) → usuarios internos (3).
  Si el operador llega a dar de alta cuentas, el usuario que cree será un titular (2).

**27 de octubre de 2026 — Subpermisos por tipo de documento y por catálogo** (modifica §4
«Autorización», §5 «Nada se borra» y la entrada del 9 de octubre).

- Los permisos asignables son 65, en cinco pestañas:
  - Documentos: `documentos_listado`, `documentos_descarga` y `documentos_eliminar`.
  - Emitir: por cada tipo (factura, notaría, carta porte, comercio exterior, obra y pago),
    crear, modificar, cancelar, timbrar, enviar y eliminar (`factura_crear`, `pago_cancelar`…).
    Reemplazan a `emitir_*`, `cancelar` y `enviar_correo`.
  - Catálogos: por cada catálogo (clientes, productos, series, vehículos y figuras), crear,
    modificar, desactivar y eliminar. Reemplazan a `administrar_*`. Series y folios sale de
    Configuración.
  - Tienda: `comprar_timbres`.
  - Empresa: Mi empresa (`empresa_datos`, `empresa_certificados`) y Configuración
    (`configuracion_general`, `configuracion_correo`, `configuracion_impuestos`).
- Crear permite editar los borradores propios; modificar, también los de otros usuarios.
- Los implícitos no se guardan; se agregan al armar el token. Cualquier casilla de Emitir da
  el listado y la descarga; la descarga da el listado; `documentos_eliminar` da eliminar en
  todos los tipos. No se guarda la descarga sin el listado.
- Eliminar en catálogos es un borrado físico y solo procede si ningún comprobante, ni
  siquiera un borrador, usa el registro; lo usado se desactiva. Eliminar en documentos sigue
  siendo solo de borradores nunca timbrados. Todo borrado queda en la bitácora con los
  valores anteriores.
- Crear empresas y administrar usuarios siguen siendo exclusivos del titular.

**28 de octubre de 2026 — La cuenta como unidad de trabajo** (modifica §1 «Multiempresa»,
§4 «Tenencia», §5 «Aislamiento», «Nada se borra» e «Índices únicos con filtro», y la entrada
del 9 de octubre).

- Cada cuenta tiene un límite de usuarios activos, que incluye al titular, y uno de empresas.
  Los fija el operador; por omisión son 3 y 1.
- Un usuario interno tiene un solo juego de permisos, el mismo en todas las empresas a las que
  entra. El titular elige a cuáles en «Acceso a empresas». Desactivar a un usuario lo saca de
  toda la cuenta. El titular puede eliminar, con su contraseña, a un usuario interno sin
  actividad registrada.
- La bolsa de timbres, las compras, los movimientos y los paquetes exclusivos son de la cuenta.
  Movimientos y compras conservan la empresa que los originó.
- Clientes y productos son de la cuenta y los comparten sus empresas. El RFC de cliente, la
  clave de cliente y el código de producto son únicos por cuenta. Comprobantes, series y folios,
  vehículos, figuras y configuración siguen siendo de cada empresa. Al unir los catálogos, los
  clientes con el mismo RFC se fusionaron en el más antiguo y se renumeraron las claves que
  chocaban; las dos cosas quedaron en la bitácora.
- Lo que es de la cuenta lleva `CuentaId`, con filtro global y sellado alimentados por el claim
  de cuenta, igual que lo de empresa.
- Excepciones a «el Client nunca envía un identificador de empresa»:
  - El alta y los permisos de un usuario reciben la lista de empresas a las que entrará. No
    cambian la empresa activa, y el servidor rechaza cualquiera que no sea una empresa activa
    de la cuenta del titular.
  - El filtro «Dado de alta en» de clientes y productos recibe una empresa. Solo filtra datos
    que ya son de la cuenta: una empresa ajena no devuelve nada.

**28 de octubre de 2026 — Público en general, productos y conceptos** (modifica §7 «Reglas de
CFDI 4.0 que rompen timbrados»).

- Con el RFC `XAXX010101000`, el régimen 616 y el uso S01 siguen fijos, pero el nombre puede
  ser otro para ventas sueltas. El formulario lo precarga como `PUBLICO EN GENERAL`. La factura
  con información global sí exige ese nombre, y el servidor rechaza otro.
- Un producto tiene nombre, que es lo que va al CFDI; un ID interno opcional (hasta 100
  caracteres, sin «|»), que precarga el `NoIdentificacion` del concepto; y una descripción
  opcional.
- La descripción del concepto se precarga con «nombre — descripción» del producto y se puede
  editar en cada factura. El servidor guarda la del renglón, con hasta 1000 caracteres y sin «|».

**29 de octubre de 2026 — Conceptos, público en general y monedas** (modifica la entrada del
28 de octubre «Público en general, productos y conceptos» y la del 27 de octubre).

- El ID interno del producto solo precarga el `NoIdentificacion` en Comercio Exterior. En los
  demás tipos el concepto no lo captura y lo envía vacío.
- Con el RFC `XAXX010101000`, el nombre del receptor se captura en cada factura y se precarga con
  el del cliente; el catálogo no cambia. El servidor lo normaliza como en el alta de clientes y,
  con información global, exige `PUBLICO EN GENERAL`.
- Solo el peso mexicano es moneda fija. Cada empresa agrega en Configuración, pestaña Monedas,
  las de `c_Moneda` que usa. Facturas y pagos solo se capturan en esas, y el servidor lo valida
  al guardar. Quitar una la desactiva; los documentos guardados conservan su moneda. Al migrar,
  cada empresa recibió las monedas que ya tenía en comprobantes o pagos, borradores incluidos.
  El tipo de cambio se sigue capturando en cada documento.
- Los permisos asignables son 66: Configuración suma `configuracion_monedas`. Al migrar lo
  recibió quien tenía `configuracion_general`.
