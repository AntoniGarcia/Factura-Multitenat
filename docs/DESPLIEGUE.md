# Despliegue

Cómo poner el sistema en un servidor. Para levantarlo en una máquina de desarrollo, ver
`docs/ARRANQUE-LOCAL.md`, que es otro documento y otro procedimiento.

**Aquí no hay ningún secreto, y no debe haberlo nunca.** Todo lo que sea llave, contraseña o
cadena de conexión se nombra; su valor se provee por variable de entorno.

---

## 1. Lo que cambia respecto a desarrollo

| | Desarrollo | Despliegue |
|---|---|---|
| Configuración | `appsettings.Development.json` | variables de entorno |
| Usuario inicial | lo crea el sembrado | **no hay sembrado**; ver §7 |
| Correo | se escribe en la consola si falta `Correo:Servidor` | obligatorio; sin él no arranca |
| Dobles de prueba | permitidos | **tumban el arranque** (§6) |
| HSTS | apagado | encendido |

`appsettings.Development.json` **no se publica**: está excluido en el `.csproj`. Contenía la
llave maestra en claro y viajaba dentro del artefacto; se corrigió en el repaso de la fase 9
(`docs/REPASO-SEGURIDAD.md` §2.1). Si alguien lo vuelve a incluir, está regalando las llaves
privadas de todos los CSD.

---

## 2. Variables de configuración

El separador de nivel es **doble guion bajo** (`__`), no dos puntos: `Jwt:ClaveDeFirma` se
escribe `Jwt__ClaveDeFirma`. Los nombres de abajo están verificados levantando el binario
publicado, no deducidos.

### Obligatorias — sin ellas no arranca

| Variable | Qué es |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production`. Cualquier valor distinto de `Development` apaga el sembrado y enciende las comprobaciones estrictas. |
| `ConnectionStrings__BaseDeDatos` | Cadena de conexión a SQL Server. |
| `Jwt__Emisor` | Emisor de los tokens. |
| `Jwt__Audiencia` | Audiencia de los tokens. |
| `Jwt__ClaveDeFirma` | **Mínimo 32 caracteres.** Falla al arrancar si falta o es corta, a propósito: no es algo que se descubra cuando alguien intenta iniciar sesión. |
| `Almacen__LlaveMaestraPfx` | Certificado en base 64 que protege el llavero de Data Protection. Ver §3. |
| `Correo__Servidor` | Servidor SMTP del SaaS. |
| `Correo__RemitenteCorreo` | Remitente de las invitaciones. |

### Recomendadas

| Variable | Qué es |
|---|---|
| `Almacen__Raiz` | Carpeta de logos y CSD cifrados. **Fuera de `wwwroot`** y en almacenamiento persistente. |
| `Almacen__RutaLlavero` | Carpeta del llavero de Data Protection. Persistente: si se pierde, los CSD ya cargados quedan ilegibles. |
| `Correo__Puerto`, `Correo__Usuario`, `Correo__Contrasena`, `Correo__UsarTls` | Credenciales SMTP. |
| `Soporte__Correo`, `Soporte__Telefono` | Se muestran cuando una empresa llega al máximo de usuarios. |

### Que no deben existir en un servidor

`Sembrado__Correo` y `Sembrado__Contrasena`. El sembrado solo corre en `Development`, pero
si alguien fuerza el entorno, estas dos claves son lo único que le falta para crear un
usuario con los seis permisos.

---

## 3. La clave maestra de Data Protection

Protege el llavero, y con él las llaves privadas de los CSD de todas las empresas. Es el
secreto más valioso del sistema.

Se genera una vez, con un comando que **no necesita base de datos ni configuración** —justo
lo que hace falta cuando todavía no hay clave:

```bash
dotnet run --project Facturacion.Server -- --generar-llave-maestra
```

Imprime un certificado en base 64. Ese valor va en `Almacen__LlaveMaestraPfx`.

**Respáldala fuera del servidor y fuera del repositorio.** Si se pierde:

- los `.cer` y `.key` ya cargados quedan **ilegibles para siempre**;
- cada empresa tiene que volver a cargar su CSD con su contraseña;
- los comprobantes ya timbrados no se ven afectados —el XML sellado no depende de esto—,
  pero no se puede timbrar nada nuevo hasta recargarlos.

Si se filtra, **rótala**: genera una nueva, y haz que cada empresa vuelva a cargar su
certificado. No hay migración automática entre llaves maestras, a propósito: una utilidad
capaz de descifrar el llavero viejo y volver a cifrarlo es exactamente la herramienta que no
conviene que exista.

`Almacen__RutaLlavero` tiene que apuntar a almacenamiento **persistente**. En un contenedor
sin volumen, el llavero se pierde en cada reinicio y el efecto es el mismo que perder la
clave maestra.

---

## 4. Base de datos

Un solo comando. Crea las tablas, los procedimientos almacenados —incluida la reserva de
folios con `UPDLOCK`—, los índices de texto completo y los catálogos de plataforma
(permisos y paquetes de timbres):

```bash
dotnet ef database update --project Facturacion.Server
```

No hay migración automática al arrancar, y es deliberado: dos instancias arrancando a la vez
aplicarían migraciones en paralelo sobre la misma base. La migración es un paso del
despliegue, antes de levantar la aplicación.

El usuario de SQL Server necesita permisos de DDL para migrar. Para operar basta lectura y
escritura; si la política lo permite, conviene usar dos cuentas distintas.

---

## 5. Catálogos del SAT

**Sin esto la aplicación arranca pero rechaza toda alta de cliente y de producto**, porque
las claves se validan contra estas tablas. El síntoma no apunta al catálogo faltante, así que
vale la pena hacerlo antes de abrir el servicio.

El archivo pesa unos 48 MB y **no está en el repositorio**: es un insumo del SAT, que además
lo republica con correcciones.

1. Descarga el `.xls` de catálogos CFDI 4.0 del portal del SAT (`catCFDI_V_4_AAAAMMDD.xls`).
2. Cárgalo:

```bash
dotnet run --project Facturacion.Server -- --cargar-catalogos ruta/al/catCFDI_V_4_AAAAMMDD.xls
```

Tarda varios minutos: unas 52 000 claves de producto, 95 000 códigos postales y 145 000
colonias, más los índices de texto completo. El comando **no levanta el servidor**: carga y
termina.

Es idempotente. Para una corrección puntual del SAT se puede recargar un solo catálogo sin
reprocesar los 300 000 renglones:

```bash
dotnet run --project Facturacion.Server -- --cargar-catalogos ruta/al/archivo.xls c_TasaOCuota
```

Los catálogos se versionan y el Client valida la versión contra el servidor al arrancar.

---

## 5.1 Esquemas y XSLT del SAT (obligatorio para timbrar)

Aparte de los catálogos, el timbrado necesita los artefactos con los que se **valida** y se
**sella** el CFDI. Sin ellos el servidor arranca y todo lo demás funciona, pero generar un
XML falla con `esquemas-sat-incompletos`.

Van en la misma carpeta que los catálogos (`EsquemasSat:Ruta`, por omisión `CatalogosSAT`):

| Archivo | Dónde va | Para qué |
|---|---|---|
| `cfdv40.xsd` | raíz | esquema del CFDI 4.0 |
| `tdCFDI.xsd` | raíz | tipos que importa el anterior |
| `catCFDI.xsd` | raíz | enumeraciones de catálogo (~6 MB) |
| `cadenaoriginal_4_0.xslt` | raíz | cadena original |
| los 33 XSLT de complemento | subcarpeta `xslt/` | los incluye el anterior |

**Los 33 no son opcionales.** `cadenaoriginal_4_0.xslt` los incluye por URL absoluta —Carta
Porte, Comercio Exterior, Nómina, INE y los demás—, y sin todos presentes la transformación
no compila, aunque el MVP no emita ninguno de esos complementos.

El sistema **no sale a internet** a buscarlos: un resolutor local los mapea desde el disco.
Es deliberado — una llamada de red dentro del sellado pondría al portal del SAT en el camino
crítico de cada timbrado.

Si falta uno, el error dice cuál y de dónde sale. Nunca se sustituye por un documento vacío:
eso produciría cadenas originales incompletas y, con ellas, sellos inválidos que solo se
descubren cuando el PAC rechaza.

---

## 6. Publicar y arrancar

```bash
dotnet publish Facturacion.Server -c Release -o ./publicado
```

Al arrancar, el servidor comprueba la frontera entre las dos mitades y lo deja en el log:

```
Contratos verificados: 9 en total, 1 sin implementación, 0 servidos por un doble.
```

- **Un doble de prueba registrado fuera de `Development` tumba el arranque.** Es intencional:
  un doble en producción responde con datos inventados sin que nada se vea raro, y no se nota
  hasta que el SAT rechaza un comprobante. Vale más un servicio que no levanta.
- **Un contrato sin implementación solo se registra como advertencia.** Hoy es
  `IResumenDocumentos`, de la mitad B: la mitad A tiene que poder desplegarse antes de que la
  otra exista.

---

## 7. El primer usuario

**No hay sembrado en producción**, y el alta de cuentas todavía no existe como pantalla: es
trabajo posterior al MVP de la mitad A. Hasta que exista, la primera cuenta, su primera
empresa y su primer usuario se crean directamente en la base de datos, y a partir de ahí el
resto entra por invitación desde la pantalla de usuarios.

Cuando eso se automatice, este apartado se reemplaza. Mientras tanto conviene dejarlo dicho
en vez de fingir que hay un procedimiento.

---

## 8. Verificar que Brotli funciona

Es el error más común del despliegue (ARQUITECTURA.md §9): si los `.br` no se sirven, la descarga
inicial se triplica. **Que los archivos existan no prueba que se sirvan.**

Primero, que se generaran:

```bash
find ./publicado/wwwroot -name "*.br" | wc -l
```

Deben ser decenas. Después, lo que importa — que el servidor los entregue:

```bash
curl -s -o /dev/null -D - -H "Accept-Encoding: br" https://tu-dominio/_framework/blazor.webassembly.js
```

En la respuesta tiene que venir **`content-encoding: br`**. Si no viene, se está sirviendo
sin comprimir, aunque el `.br` esté en disco.

Referencia medida en la fase 9 sobre el binario publicado:

| Recurso | Sin comprimir | Con `br` | Ahorro |
|---|---:|---:|---:|
| `dotnet.native.*.wasm` | 3 002 094 B | 976 842 B | 67 % |
| `blazor.webassembly.*.js` | 60 682 B | 16 754 B | 72 % |

Si un proxy inverso está delante, revisa que no reescriba `Accept-Encoding` ni recomprima:
lo habitual es que baste con `proxy_pass` sin tocar esa cabecera.

---

## 9. Proxy inverso — léelo antes de poner uno

Hoy **no hay configuración de encabezados reenviados** en el proyecto, y eso tiene
consecuencias concretas (`docs/REPASO-SEGURIDAD.md` §5.1).

Detrás de un proxy que termina TLS, la aplicación ve las peticiones como `http://` aunque el
usuario esté en HTTPS. Con eso:

- **HSTS nunca se emite**, porque `UseHsts()` cree estar en texto claro;
- **`UseHttpsRedirection()` puede ciclar**: responde 307 a HTTPS, el proxy vuelve a entregar
  en HTTP, y otra vez;
- la IP que ve el control de intentos es la del proxy, no la del cliente, así que el límite
  por IP de la fase 1 pasa a contar a todo el mundo junto.

La corrección es `UseForwardedHeaders`, pero **no se activa a la ligera**: si se aceptan
`X-Forwarded-*` de cualquier origen, un cliente puede falsificar su IP y su esquema, y con
eso envenena tanto el control de intentos como la bitácora. Hay que acotarlo con
`KnownProxies` o `KnownNetworks` al proxy real, nunca abierto.

Por eso quedó pendiente en vez de resuelto: depende de una topología que todavía no está
decidida.

---

## 10. Lista de comprobación

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Todas las variables obligatorias de §2 puestas
- [ ] `Sembrado__*` **no** existe
- [ ] Clave maestra generada, respaldada fuera del servidor, y llavero en disco persistente
- [ ] `dotnet ef database update` aplicado
- [ ] Catálogos del SAT cargados y visibles con su versión
- [ ] Esquemas y XSLT del SAT en su carpeta, incluidos los 33 XSLT de complemento (§5.1)
- [ ] El log dice `0 servidos por un doble`
- [ ] `content-encoding: br` comprobado con `curl` contra el dominio real
- [ ] Si hay proxy inverso: §9 leído y decidido
- [ ] `appsettings.Development.json` **no** está en el artefacto publicado
