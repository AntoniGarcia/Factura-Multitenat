# CLAUDE.md — Contexto permanente del proyecto

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

Trabajo la **mitad A — Plataforma, identidad y catálogos**, definida en
`REPARTO-EQUIPO.md` §3.

**Nunca escribas código de la mitad B** (emisión, motor de impuestos, timbrado, PAC,
XML, PDF, correo, listado de documentos, cancelación, complemento de pagos). Puedes
leer sus archivos para entender el contrato; no los editas. Si una tarea que te pido
cruza esa frontera, **detente y dímelo** en vez de improvisar.

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

**Fuera del MVP, fase 2:** Carta Porte y sus catálogos (§10, §11, §26 del documento
funcional), Comercio Exterior (§16–§19), Notaría (§20–§25), Constructoras (§15),
Cotizaciones, Addendas, complemento INE, inventario.

Sí se modela desde ahora el campo de licencias por empresa (`LicNotarios`, `LicObras`,
`LicComercio`, `LicINE`) para que la fase 2 sea agregar módulos y no rehacer el esquema.

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
