# Repaso de seguridad — cierre de la mitad A

Fase 9. Repaso de los ocho puntos que pide `PROMPT-FASES-A.md`, más lo que apareció por el
camino. Cortado al **17 de agosto de 2026**, sobre el commit de la fase 8 (`4d45eb0`).

Alcance: **mitad A** (plataforma, identidad, catálogos). La mitad B no existe todavía, así
que nada de lo que aquí se afirma cubre emisión, timbrado ni salidas.

> Este documento no sirve si dice que todo está bien. Lo que sigue incluye tres cosas que
> estaban mal, dos que no pude verificar y una que sigue pendiente por decisión.

---

## 1. Resumen

| # | Punto | Veredicto |
|---|---|---|
| 1 | Ningún endpoint recibe `empresaId` del cliente | ✅ correcto |
| 2 | Query filter cubre toda entidad de empresa; `IgnoreQueryFilters` justificado | ⚠️ **2 corregidos** |
| 3 | Todo endpoint que muta tiene su política de permiso | ✅ correcto |
| 4 | Logs sin RFC completos, contraseñas, tokens ni certificados | ✅ correcto |
| 5 | Archivos inaccesibles sin autorización y sin adivinar la ruta | ✅ correcto |
| 6 | El service worker no cachea `/api/` | ✅ correcto |
| 7 | CSP sin `unsafe-*` más allá de `wasm-unsafe-eval` | ⚠️ **una excepción acotada; HSTS no verificable aquí** |
| 8 | Los `.br` se sirven de verdad | ✅ **verificado midiendo** |

**Fuera de la lista de ocho, encontrado al publicar de verdad:**

| Hallazgo | Gravedad | Estado |
|---|---|---|
| El artefacto de publicación se llevaba la llave maestra de Data Protection | **Alta** | ✅ corregido |
| `ProductoImpuesto` sin filtro de empresa — lo dio por bueno este mismo repaso (§2.3) | Media | ✅ corregido |
| Sin configuración de proxy inverso, HSTS no se emite y la redirección a HTTPS puede ciclar | Media | ⛔ pendiente, necesita decisión |
| El plazo de aviso de membresía está duplicado entre servidor y Client | Baja | ⚠️ parcial |

---

## 2. Lo que estaba mal

### 2.1 · La llave maestra viajaba dentro del artefacto de despliegue — **corregido**

**Qué pasaba.** `dotnet publish` copia al publicado todos los `appsettings.*.json` del
proyecto. Entre ellos `appsettings.Development.json`, que está en `.gitignore` justamente
porque contiene en claro:

- `Almacen:LlaveMaestraPfx` — 4340 caracteres; es la clave que protege el llavero de Data
  Protection y, con él, **las llaves privadas de todos los CSD cargados**;
- `Jwt:ClaveDeFirma` — con ella se puede firmar un token de cualquier usuario y cualquier
  empresa;
- `Sembrado:Contrasena`.

Que ASP.NET Core solo *lea* ese archivo cuando el entorno es `Development` no ayuda en nada:
el secreto igual está dentro del paquete que se sube al servidor, y cualquiera con acceso al
artefacto se lleva las llaves. El `.gitignore` protegía el repositorio y daba una falsa
sensación de estar cubierto; el artefacto no estaba cubierto por nada.

**Cómo apareció.** No leyendo el `.csproj`, sino publicando de verdad y abriendo el
resultado. En el árbol de fuentes no se ve nada raro.

**Corrección.** En `Facturacion.Server.csproj`:

```xml
<Content Update="appsettings.Development.json" CopyToPublishDirectory="Never" />
```

**Verificado:** publicación limpia posterior — en el publicado queda solo `appsettings.json`,
y los 84 archivos `.br` siguen ahí.

### 2.2 · `IgnoreQueryFilters` sin justificación por escrito — **corregido**

Seis usos en total. Cinco ya traían su comentario explicando por qué se salta el filtro
(purga de idempotencia, barrido de reservas, acreditación por consola ×2, bitácora de
alcance de cuenta). El sexto, `ServicioDeInvitaciones.BuscarPorTokenAsync`, no.

El uso **es correcto** —aceptar una invitación es anónimo, no hay sesión ni empresa activa y
el filtro global no devolvería nada—, pero sin decirlo, el siguiente que lo lea no puede
distinguirlo de un descuido. Se documentó, incluyendo lo que de verdad sostiene la seguridad
ahí: la autorización es el token mismo, se busca por el SHA-256 de un valor aleatorio de 256
bits y no se acepta ningún otro criterio de búsqueda, así que saltarse el filtro no abre la
tenencia.

### 2.3 · `ProductoImpuesto` fuera del filtro global — **corregido, y este repaso lo había dado por bueno**

**Cómo apareció.** No en este repaso: en el log de arranque, *después* de cerrar la fase 9,
al verificar por qué no se podía levantar el servidor. EF lo venía avisando en cada arranque:

> `Entity 'Producto' has a global query filter defined and is the required end of a`
> `relationship with the entity 'ProductoImpuesto'.`

**Qué falló en el método.** Para el punto 2 enumeré las entidades que *implementan*
`IEntidadDeEmpresa` y comprobé que el filtro se les aplicara. Nunca pregunté lo contrario:
qué tablas con datos de empresa **no** implementan la interfaz. Verificar lo que está en la
lista no dice nada sobre lo que nunca entró en ella.

Cruzando los 31 `DbSet` sin filtro contra sus exclusiones documentadas, 30 estaban
justificados —catálogos del SAT, `Cuenta`, `Empresa`, `UsuarioEmpresa`,
`UsuarioEmpresaPermiso`, `RefreshToken`, `Membresia`—. `ProductoImpuesto` era el único con
datos de empresa, sin filtro y sin exclusión escrita.

**No hubo fuga.** Los tres accesos del código eran seguros: las lecturas entran por
`Include(p => p.Impuestos)` desde `Productos`, que sí filtra, y los dos usos directos del
`DbSet` son escrituras sobre entidades ya cargadas por una consulta filtrada.

Lo que sí había era una puerta abierta: `AppDbContext.ProductosImpuestos` es público y sin
filtro, así que `baseDeDatos.ProductosImpuestos.Where(...)` devolvía renglones de todas las
empresas en silencio. Y la mitad B va a leer impuestos de producto para calcular
comprobantes: es exactamente el uso que caía en la trampa.

**Corrección.** `ProductoImpuesto` implementa `IEntidadDeEmpresa` y gana columna `EmpresaId`
(migración `A_EmpresaEnProductoImpuesto`). Con eso lo cubren solos el filtro global y el
sellado del interceptor, sin que nadie tenga que acordarse de nada — que es la regla de
ARQUITECTURA.md §5.

La migración va en tres pasos, y el orden importa: la columna se agrega **nullable**, se
rellena desde el producto padre y solo entonces se vuelve obligatoria. Agregarla directamente
como `NOT NULL` habría dejado todos los renglones existentes en `Guid.Empty`, y el filtro
global los habría escondido: los impuestos de cada producto ya capturado habrían desaparecido
de la aplicación **sin un solo error**.

**Verificado**, y no solo aplicando la migración sobre una tabla vacía —que no prueba nada—:
creé productos con impuestos en las dos empresas, revertí la migración, la volví a aplicar y
comprobé que los tres renglones tomaran el `EmpresaId` de su producto padre, ninguno en
`Guid.Empty`. Después, por la API: la Llantera ve su único impuesto y la Cementera los dos
suyos. El aviso de EF desapareció del arranque.

### 2.4 · Umbral de aviso duplicado — **parcial**

`ServicioDeCompras.EstaPorVencer` fija el aviso de membresía en 30 días, y
`Paginas/Timbres/BolsaDeTimbres.razor` tiene **su propia copia** de esa regla en el
navegador. Dos definiciones de la misma política de negocio que pueden separarse sin que
nada avise.

Al construir el tablero iba a añadir una tercera. En vez de eso, `TableroDto` trae
`MembresiaEnAviso` ya decidido por el servidor, igual que `UmbralAvisoTimbres`.

**Pendiente:** `BolsaDeTimbres.razor` sigue con su copia. No lo toqué porque es pantalla de
la fase 7 y el cambio no es de seguridad; queda anotado abajo.

---

## 3. Lo que está bien, y por qué lo está

### Punto 1 · `empresaId` desde el cliente

Un solo endpoint lo recibe: `POST /api/auth/cambiar-empresa`. Es **la excepción prevista**
por ARQUITECTURA.md §4, y está bien resuelta: antes de emitir el token nuevo comprueba que el
acceso exista, esté activo, la empresa esté activa **y** pertenezca a la cuenta del usuario.

Detalle que vale la pena registrar: cuando no hay acceso devuelve el mismo error que si la
empresa no existiera, así que el endpoint no funciona como oráculo para averiguar qué
identificadores de empresa son reales.

Ningún otro endpoint acepta empresa por ruta, query ni cuerpo. La empresa sale siempre del
claim vía `IContextoEmpresa`.

### Punto 2 · Cobertura del query filter

> Este apartado decía que el punto estaba limpio. **Estaba incompleto**: se le escapó
> `ProductoImpuesto`, que apareció después en el log de arranque. Ver §2.3, incluido por qué
> el método de revisión no podía encontrarlo.

`FiltroDeEmpresa` recorre el modelo y aplica el filtro a toda entidad que implemente
`IEntidadDeEmpresa` (14 tras la corrección de §2.3) o `IEntidadDeEmpresaOpcional` (1:
`RegistroBitacora`). Nadie tiene que acordarse de filtrar: basta implementar la interfaz.
**Falla cerrado** — sin empresa activa el parámetro va nulo, la comparación en SQL Server
queda en desconocido y no se devuelve ningún renglón.

La comprobación que sirve no es recorrer las entidades que implementan la interfaz, sino
**cruzar todos los `DbSet` contra las exclusiones documentadas** y exigir que cada uno sin
filtro tenga su motivo por escrito. Hecho así, quedan 30 sin filtro y los 30 justificados.

Las exclusiones están decididas y escritas: tablas de Identity, `Cuenta`, `Empresa`,
`UsuarioEmpresa`, `UsuarioEmpresaPermiso`, `RefreshToken`, `Permiso` y los catálogos del SAT.
Comprobé que `Empresa` y `UsuarioEmpresa` **no** implementan la interfaz: si la implementaran,
el selector de empresas de la barra superior no podría listar más que la activa.

`Membresia` queda fuera a propósito porque es de la **cuenta**, no de la empresa. Eso la deja
sin red: hay que acotarla a mano. Verifiqué el único punto que la consulta
(`ServicioDeCompras.MembresiaAsync`) y filtra por `contexto.CuentaActual`, que sale del
claim, no de nada que mande el cliente.

### Punto 3 · Políticas en endpoints que mutan

Todos cubiertos. Tres grupos la declaran a nivel de grupo y por eso no se ve en cada ruta:
`/api/series` y `/api/usuarios` exigen `configurar_empresa` y `administrar_usuarios`
respectivamente.

Dos casos que parecen huecos y no lo son:

- **`/api/perfil`** solo pide sesión, sin permiso. Es correcto: cambiar tu propio nombre o
  contraseña no puede exigir `administrar_usuarios`. Revisé los tres handlers que mutan y
  los tres acotan por el id del token; ninguno acepta un id de usuario del cuerpo.
- **`/api/auth/cerrar-sesion`** es anónimo aun siendo mutante. Es lo correcto: un access
  token vencido no debe impedirte cerrar sesión.

### Punto 4 · Logs

`EnmascaradorDeTextoSensible` recorta los RFC a los tres primeros caracteres y omite el
valor de las claves `password`, `pwd`, `contrasena`, `contraseña`, `token`, `secret` y
`secreto`. Hay prueba automatizada del enmascarado en la suite.

### Punto 5 · Archivos

Cuatro capas, y hacen falta las cuatro:

1. Viven fuera de `wwwroot`, en `almacenamiento/` bajo el content root — el servidor de
   estáticos no los alcanza.
2. El endpoint de lectura (`GET /api/empresa/logo`) **no recibe ruta ni empresa**: las saca
   del claim. No hay parámetro que manipular.
3. El nombre en disco es `{empresaId}/{categoria}/{guid:N}.bin`; no se expone nunca y no se
   adivina.
4. `RutaSegura` normaliza la ruta y comprueba que no salga del almacén. La raíz se compara
   **con separador final**, detalle que evita que `…/almacen2` pase por empezar igual que
   `…/almacen`.

Encima, el contenido se cifra con un propósito de Data Protection que incluye el `empresaId`:
un archivo de otra empresa, aunque se filtrara, no se descifra con el propósito equivocado.

### Punto 6 · Service worker

La exclusión de `/api/` es **la primera línea** de `onFetch`, antes de cualquier otra
decisión, y está comentada como salvaguarda y no como comodidad. Un catálogo del SAT
cacheado y viejo produce comprobantes mal emitidos.

### Punto 8 · Brotli — verificado midiendo, no leyendo

ARQUITECTURA.md §9 lo llama el error más común del despliegue, así que no bastaba con ver que se
generaran los `.br`. Publiqué en Release, levanté el binario en `Production` y pedí los
activos con `Accept-Encoding: br`:

| Recurso | Sin comprimir | Con `br` | `Content-Encoding` | Ahorro |
|---|---:|---:|---|---:|
| `dotnet.native.*.wasm` | 3 002 094 B | 976 842 B | `br` | 67 % |
| `blazor.webassembly.*.js` | 60 682 B | 16 754 B | `br` | 72 % |
| `app.css` | 4 842 B | 1 315 B | `br` | 73 % |

Se sirven de verdad. 84 archivos `.br` en el publicado.

---

## 4. Lo que no pude verificar

### 4.1 · HSTS

`UseHsts()` está en la tubería y se aplica fuera de `Development` (correcto: activarlo en
desarrollo fijaría `localhost` a HTTPS en el navegador del equipo durante meses).

En la prueba **la cabecera no apareció**, y no es un defecto: `UseHsts` no emite
`Strict-Transport-Security` sobre HTTP —lo prohíbe el RFC 6797— y además excluye *loopback*
por omisión. Mi prueba fue `http://localhost`, así que cumple las dos condiciones para no
emitirla.

**No verificado:** que se emita sobre HTTPS y host real. Requiere un despliegue con
certificado y nombre de dominio, que hoy no existe. Ver 5.1, que es la razón por la que esto
importa más de lo que parece.

### 4.2 · `style-src-attr 'unsafe-inline'`

Es la única excepción a «nada de `unsafe-*`» además de `wasm-unsafe-eval`. Está ahí porque
MudBlazor escribe atributos `style=""` en línea (posición de menús flotantes, ancho de barras
de progreso) y con solo `style-src 'self'` el navegador los descarta.

Es la forma **más acotada** que permite la especificación: al ser directiva aparte y no
`'unsafe-inline'` dentro de `style-src`, un bloque `<style>` inyectado sigue bloqueado. Un
atributo de estilo no ejecuta script; el riesgo residual sería exfiltrar con `url()`, y para
eso haría falta inyección de HTML, que Razor escapa por omisión.

**No verificado:** que MudBlazor siga necesitándola. No probé a quitarla y recorrer la
aplicación. Quedó anotado en el propio `PoliticaDeContenido.cs`.

---

## 5. Pendiente

### 5.1 · Proxy inverso: sin `UseForwardedHeaders`, HSTS no se emite y la redirección puede ciclar

No hay ninguna configuración de encabezados reenviados en el proyecto.

Detrás de un proxy que termina TLS —nginx, IIS, Azure App Service, casi cualquier
despliegue real— la aplicación ve las peticiones como `http://`, aunque el usuario esté en
HTTPS. Con eso:

- `UseHsts()` **nunca** emite `Strict-Transport-Security`, porque cree estar en texto claro;
- `UseHttpsRedirection()` responde 307 a HTTPS, el proxy vuelve a entregar en HTTP, y se
  cicla.

**Por qué no lo corregí.** Configurar `UseForwardedHeaders` sin saber la topología es peor
que no configurarlo: si se aceptan `X-Forwarded-*` de cualquier origen, un cliente puede
falsificar su IP y su esquema, y con eso se envenena el control de intentos por IP —que es
justo la defensa contra fuerza bruta de la fase 1— además de la bitácora.

Cuando se decida dónde se despliega, hay que añadir `UseForwardedHeaders` **con
`KnownProxies` o `KnownNetworks` acotados al proxy real**, nunca abierto. Documentado en
`docs/DESPLIEGUE.md`.

### 5.2 · Copia del umbral de 30 días en `BolsaDeTimbres.razor`

Ver 2.4. El tablero ya no la duplica; la pantalla de la bolsa sí.

### 5.3 · Lo que este repaso no cubre

- **La mitad B no existe.** Cuando aparezca, todo endpoint suyo que mute vuelve a pasar por
  el punto 3, y sus entidades por el punto 2.
- **`IResumenDocumentos` sigue sin implementación.** `VerificacionDeContratos` lo reporta al
  arrancar. Fuera de `Development`, un doble registrado **tumba el arranque** a propósito: un
  doble en producción responde con datos inventados y no se nota hasta que el SAT rechaza.
- **No hay pruebas de penetración.** Esto es una revisión de código y unas cuantas
  comprobaciones a mano, no una auditoría.
