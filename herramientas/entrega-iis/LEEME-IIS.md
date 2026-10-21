# Entrega de revisión para Windows / IIS

Para una instalacion nueva en un sitio IIS ya creado y con carpeta vacia, empezar
por **EMPEZAR-AQUI.md** y ejecutar **INSTALAR.cmd como administrador**. Permite elegir
el sitio, copia la publicacion y prepara rutas y permisos sin modificar SQL ni enlaces.
Rechaza carpetas ocupadas, llaves previas y grupos compartidos. No actualiza instalaciones.
`PREPARAR-IIS.ps1` queda como alternativa manual. No repetir los pasos manuales
de carpetas sobre una instalacion ya preparada.

Publicación conjunta de Facturacion.Server y Facturacion.Client, preparada el 21 de
octubre de 2026. No es producción: utiliza Staging, PAC deshabilitado y no incorpora
pasarela de pagos. El complemento de pagos CFDI sí forma parte de la aplicación.

## Qué entregar y dónde va

- `sitio/`: publicación completa que IIS ejecuta. Se debe copiar TODO su contenido
  a la carpeta física de la aplicación. Incluye `web.config`, DLL, dependencias,
  `CatalogosSAT/` y `wwwroot/` (cliente). No colocar el backend dentro de `wwwroot`.
- `base-datos/Facturacion.Instalacion.sql`: estructura de SQL Server. Ejecutar desde
  SSMS; no colocar el script dentro del sitio ni de `wwwroot`.
- `base-datos/Permisos-Aplicacion.sql.ejemplo`: permisos acotados para el usuario
  de ejecución, después de crear la estructura. Requiere revisión del administrador.
- `VERIFICAR.ps1`: comprobación local de archivos y configuración no secreta.
  Ejecutar desde PowerShell: `.\VERIFICAR.ps1`. No cambia permisos ni instala servicios.
- `SHA256SUMS.txt`: huellas de los archivos entregados, para comprobar integridad.
- `PREPARAR-IIS.ps1` y `EMPEZAR-AQUI.md`: preparacion automatica y pasos breves.
- `INSTALAR.cmd` y `INSTALAR.ps1`: instalador interactivo para un sitio IIS existente
  y una carpeta vacia. Ejecutar fuera del sitio publico, con permisos de administrador.
- `LEEME-IIS.md`: estas instrucciones, fuera de la raíz pública.

No contiene bases con datos, usuarios, contraseñas, certificados privados ni `cnf`.
El ZIP por si solo no instala IIS, SQL Server ni certificados. Ejecutar el instalador
(o el preparador manual) es necesario para preparar la carpeta privada y sus permisos;
no se hace al extraerlo.
No ejecutar `INICIAR.cmd`: IIS administra el proceso del backend en esta entrega.

## 1. Requisitos del servidor

1. Windows x64 con IIS, consola de administración, contenido estático y autenticación
   anónima habilitados para este sitio. La autenticación de la aplicación es propia.
2. .NET 10 Hosting Bundle, no solo ASP.NET Core Runtime. Confirmar en IIS → Módulos
   que existe `AspNetCoreModuleV2`. Si el bundle se instaló antes que IIS, repararlo.
   No se necesita el SDK ni Visual Studio para ejecutar este paquete.
3. SQL Server con Full-Text Search instalado. El esquema se instala por separado
   con una cuenta administradora; la cuenta de la aplicación no necesita alterar tablas.
4. Un nombre DNS o acceso local y un certificado HTTPS válido y confiable para ese nombre.
   El ingeniero define enlaces, DNS y acceso de red. El FTP solo transfiere archivos.

Referencia: https://learn.microsoft.com/aspnet/core/host-and-deploy/iis/hosting-bundle?view=aspnetcore-10.0

## 2. Preparar SQL Server

Si ya existe una base preparada con este mismo esquema, no crear otra por obligación.
Si es una instalación nueva, crear una base vacía para revisión y ejecutar el script
completo en SSMS, seleccionando esa base antes de ejecutar. El script no crea la base.
No ejecutar contra una base de producción ni restaurar datos personales de la PC de Luis.

El asistente solo prueba y guarda la conexión: NO ejecuta el script SQL. La carga de
los catálogos SAT faltantes sí será automática después de completar la instalación.

Elegir una identidad de acceso SQL limitada a esa base:

- Autenticación Windows con SQL en la MISMA máquina: si el grupo se llama
  `FacturacionRevision`, el inicio de sesión es `IIS APPPOOL\FacturacionRevision`.
  Primero crear el grupo IIS, luego crear el login Windows en SQL y mapearlo a la base.
- SQL en otro servidor: configurar una cuenta de dominio apropiada o un login SQL.
  Una identidad virtual de grupo IIS no se traslada tal cual al SQL remoto.
- Autenticación SQL: el administrador crea el login y usuario de aplicación,
  habilita el método si corresponde y entrega las credenciales por un medio privado.
  Se capturan después en el asistente. Nunca incluirlas en el ZIP ni en `wwwroot`.

Usar `Permisos-Aplicacion.sql.ejemplo` como guía: permite leer/escribir datos y ejecutar
los cinco procedimientos del sistema. No otorga sysadmin, db_owner ni ALTER.
Los scripts de esquema/migraciones se ejecutan con otra cuenta administrativa.

## 3. Crear el grupo y las carpetas

En Administrador de IIS crear un grupo exclusivo, por ejemplo `FacturacionRevision`:

- Versión de .NET CLR: Sin código administrado (No Managed Code).
- Modo de canalización: Integrado.
- Configuración avanzada → Habilitar aplicaciones de 32 bits: False.
- Identidad: ApplicationPoolIdentity.
- Cargar perfil de usuario (Load User Profile): True.
- Set Profile Environment: True.

Extraer el ZIP y dejar `sitio` en su ubicación definitiva. Ejemplo:
`C:\Sitios\FacturacionRevision\sitio`. Dar a `IIS APPPOOL\FacturacionRevision`
permiso de lectura y ejecución sobre esa carpeta y sus archivos.

Crear una carpeta NUEVA y privada, fuera de cualquier sitio, por ejemplo
`C:\FacturacionDatos\Revision`. Dar control total a SYSTEM y Administradores y
permiso Modificar a `IIS APPPOOL\FacturacionRevision`, con herencia para sus archivos
y subcarpetas. Retirar acceso de usuarios generales a esta carpeta. No aplicar estos
cambios sobre carpetas de otras aplicaciones ni reutilizar una carpeta con llaves ajenas.
Crear también `C:\FacturacionDatos\Revision\logs` con los mismos permisos.

## 4. Ajustar el web.config ANTES de arrancar

La publicación trae un `web.config` de revisión SIN secretos. Ajustar:

- `Instalacion__Directorio`: ruta absoluta privada, por defecto
  `C:\FacturacionDatos\Revision\cnf`. No puede quedar dentro de la carpeta del sitio.
- `stdoutLogFile`: ruta privada de diagnóstico, por defecto
  `C:\FacturacionDatos\Revision\logs\stdout`. El log de arranque está deshabilitado.
- `AllowedHosts`: sustituir la lista local por el nombre DNS real. Escribir solo
  nombres, separados por `;`, sin protocolo, rutas ni puerto. No usar `*`.

Conservar `hostingModel="outofprocess"`, `ASPNETCORE_ENVIRONMENT=Staging`,
`Instalacion__ArranqueAutomatico=true` y `Pac__Modo=Deshabilitado`.
La entrega también activa `Instalacion__PermitirSinClaveLocal=true`: el asistente
no pide clave cuando se abre desde el servidor mediante localhost o una IP de
loopback. Desde otra PC sigue exigiendo la clave. No funciona como excepción en
Production y no acepta invocaciones de una página de otro origen.
No establecer `Instalacion__PrepararRevision=true`: es un comando de consola que
prepara archivos y termina el proceso, no el modo de arranque para IIS.
No configurar `ASPNETCORE_URLS`: los enlaces HTTPS se configuran en IIS.
No heredar variables globales de otra instalación (SQL, JWT, almacén o SMTP).

## 5. Crear el sitio IIS

Crear un sitio NUEVO, no sobrescribir sitios existentes:

1. Ruta física: la carpeta `sitio`, no `sitio\wwwroot` ni la raíz del ZIP.
2. Grupo de aplicaciones: `FacturacionRevision` (o el nombre elegido).
3. Enlace HTTPS: IP/puerto/nombre definidos por el administrador y certificado
   correspondiente. Para distintos nombres en 443 usar SNI cuando corresponda.
4. Instalar como sitio raíz: rutas como `/operador` y `/api` deben partir de ese origen.
   No montarlo bajo `/cfdiAdmon` como subaplicación sin una adaptación adicional.
5. No abrir el asistente por HTTP desde la red ni ignorar errores de certificado.

Configurar acceso de red solo a quienes revisarán el sistema. Durante la instalación,
restringir el sitio al responsable; abrirlo a revisores después de crear al operador.

## 6. Completar el asistente

Abrir `https://NOMBRE_DEL_SITIO/operador`. En el primer acceso IIS arrancará el backend
y este creará su configuración cifrada bajo la identidad del grupo.

Para evitar la clave durante esta revisión, completar el asistente desde el
navegador del propio servidor usando `https://localhost:PUERTO/operador`, con un
enlace y certificado HTTPS apropiados para localhost. No hace falta abrir archivos
ni copiar la clave. Si solo hay un enlace por dominio o el acceso es remoto,
la clave sigue siendo necesaria. Como administrador del servidor, se encuentra en:

```text
C:\FacturacionDatos\Revision\cnf\clave-instalacion.txt
```

Si se eligió otra ruta, usar esa. Para el acceso que requiera clave, copiarla sin enviarla por correo,
FTP sin cifrado o capturas. La clave no es la contraseña SQL ni la del operador.

Capturar servidor, base y método de autenticación; probar y guardar. Windows usa la
identidad del grupo IIS, NO la cuenta del administrador que abre el navegador.
Para SQL local con certificado autofirmado puede autorizarse explícitamente confiar
en el certificado; para el servidor real es preferible validar un certificado confiable.

Si no hay ningún operador, crear el principal con correo y contraseña elegidos por el
responsable. Si ya existe un operador, no se habilita el alta inicial; usar su acceso.
Al finalizar, la aplicación pasa al sistema y prepara los catálogos SAT que falten.
La pantalla muestra el avance; no interrumpir el primer arranque. Después entrar como operador.

## 7. Qué se conserva

En la carpeta privada, `cnf` contiene:

- `appsettings.Instalacion.json`: conexión SQL cifrada.
- `appsettings.ArranqueRevision.json`: JWT y clave maestra cifrados.
- `clave-instalacion.txt`: autorización del asistente, legible solo para las identidades autorizadas.
- `llavero-instalacion`, `llavero-aplicacion`: claves persistentes.
- `datos`: almacenamiento de archivos de la aplicación.

La identidad Windows y el equipo forman parte de la protección DPAPI. No copiar `cnf`
desde otra PC, no cambiar la identidad del grupo y no borrar el perfil/llavero para
actualizar el sistema. No basta con copiar estos archivos para migrar a otra identidad
o máquina: esa operación requiere un procedimiento específico de migración de claves.
Las tablas y documentos estructurados permanecen en SQL Server.

## 8. Correo y comprobaciones

Configurar el SMTP del sistema desde el operador antes de probar registros y códigos.
No se incluyen credenciales de Gmail ni SMTP de otra instalación. Los comprobantes
pueden usar SMTP por empresa según su configuración. Si la base se copió desde otra
instalación con secretos cifrados, no se garantiza su descifrado con las llaves nuevas;
no reutilizarla sin un plan de migración y reconfiguración de esos secretos.

Comprobar en el servidor de destino:

1. Acceso al operador y a `/api/version`.
2. Reciclar SOLO el grupo de esta aplicación; volver a entrar sin repetir el asistente.
3. Crear/seleccionar empresa, consultar catálogos, guardar y reabrir borrador y generar PDF.
4. Confirmar que los recursos `_framework` responden comprimidos con Brotli cuando
   el navegador envía `Accept-Encoding: br` y que no quedan marcadores `#[` en index.html.
5. Configurar SMTP y probar envío por separado. No probar timbrado real ni cobros.

En la PC de desarrollo: se verificó el asistente por HTTPS, el arranque cifrado y su
persistencia al reciclar IIS, los 28 catálogos preparados y respuesta HTTP 200 con
`Content-Encoding: br` para el cargador Blazor. La revisión del 21 de octubre añadió
la excepción local sin clave, comprobada en navegador y por HTTP. Pasaron 32 pruebas
de acceso al asistente, conexión, arranque y catálogos.
Luis confirmó conexión, creación del operador, acceso
posterior al reciclado y la secuencia de catálogos/borrador/PDF. Esto no sustituye la
prueba en el servidor del ingeniero ni certifica todas las variantes fiscales.

## 9. Diagnóstico y actualizaciones

- 500.19 / módulo desconocido: revisar Hosting Bundle, `AspNetCoreModuleV2` y web.config.
- 502.5 / proceso no inicia: revisar runtime x64, rutas, permisos y el Visor de eventos.
  Para diagnóstico temporal activar `stdoutLogEnabled="true"`, revisar el log privado
  y volver a desactivarlo. No publicar logs ni mantenerlos sin control de tamaño.
- Error SQL por usuario: revisar acceso de la identidad efectiva de IIS a ESA base.
- No abre tras crear operador: conservar consola/log y configuración. Reciclar solo
  el grupo propio y revisar el error; no borrar operadores o llaves como reparación.
- No abre desde otra PC: localhost significa esa PC. Usar el nombre DNS configurado
  por el ingeniero y su certificado; revisar DNS, enlace y firewall.

Antes de actualizar, respaldar la base y conservar el directorio privado y la identidad.
Detener solo el grupo propio, actualizar la publicación completa conservando el
web.config del servidor, aplicar migraciones revisadas si hay y arrancar de nuevo.
No copiar un web.config de ejemplo encima de la configuración de una instalación existente.

No subir este ZIP, las instrucciones ni los scripts SQL al contenido público. Si se
usa FTP, transferir únicamente el contenido de `sitio` a la ruta física acordada;
configurar credenciales y llaves directamente en el servidor por un medio privado.
Si se utiliza INSTALAR.cmd, entregar TODO el paquete en una carpeta de trabajo NO
publica del servidor y dejar vacia la carpeta del sitio: el instalador realiza la copia.
No ejecutar este instalador sobre el publish existente; esta fase no incluye actualizaciones.
