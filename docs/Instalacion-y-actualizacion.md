# Instalación y actualización

## Actualización temporal desde el perfil

En revisión puede habilitarse un acceso temporal para una sola cuenta de usuario.
El botón aparece en **Mi perfil → Actualizar base de datos**. La entrada directa es
`/mantenimiento-base`; permite usar las credenciales del mismo usuario cuando el
esquema anterior impide abrir las pantallas habituales o preparar los catálogos SAT.
El botón utiliza `/api/mantenimiento-base/interfaz` para evitar que un service worker
anterior sustituya el módulo por una copia del cliente guardada en caché. Esa misma
ruta sirve de entrada directa cuando el navegador todavía conserva la PWA anterior.
No utiliza la contraseña del operador ni concede permisos de operador al cliente.

La autorización se configura en `cnf/appsettings.Mantenimiento.json` (o en el
directorio privado configurado para la instalación). Este archivo se conserva
fuera de Git y no reemplaza la conexión ni las llaves existentes:

```json
{
  "MantenimientoBase": {
    "Habilitado": true,
    "Correo": "usuario-autorizado@example.invalid",
    "HastaUtc": "2026-11-16T23:59:59Z"
  }
}
```

El usuario y su cuenta deben estar activos, su correo confirmado y su contraseña
debe ser válida. La sesión de mantenimiento dura diez minutos y queda solo en
memoria del navegador. El acceso se desactiva al vencer la fecha, al eliminar el
archivo o al poner `Habilitado` en `false` y reiniciar la aplicación. En Production
queda deshabilitado aunque exista el archivo.

Procedimiento:

1. Respaldar la base desde SQL Server y conservar el respaldo fuera del sitio web.
2. Publicar la aplicación que incorpora el módulo y el registro del script aprobado.
3. Conservar `cnf` y agregar únicamente el archivo privado de autorización temporal.
4. Entrar al módulo, comprobar el servidor y la base que muestra y cargar el SQL.
5. Pulsar **Revisar script**, confirmar el respaldo y escribir de nuevo la contraseña.
6. Ejecutar fuera del horario de captura. Esperar **completada**, la preparación SAT
   y comprobar los documentos antes de permitir el uso habitual.

El módulo admite el acumulado que recupera los índices omitidos en el primer
esquema. No crea una base ni pide otra conexión: usa la que ya tiene el servidor.
El usuario SQL necesita permisos para modificar el esquema, ver su definición,
crear tablas/procedimientos y catálogos FULLTEXT; SQL Server debe tener instalado
el componente de búsqueda de texto completo. Los permisos de lectura/escritura
de datos por sí solos no bastan. El administrador SQL debe conceder los permisos
necesarios antes del primer uso. `cnf` necesita permiso de modificar para la
identidad del grupo de aplicaciones; la carpeta principal del sitio no lo necesita.

Se acepta únicamente el archivo cuyo SHA-256 esté registrado en la aplicación;
cambiar sus saltos de línea o contenido también cambia su huella. Para próximas
entregas, revisar y probar el SQL, registrarlo y volver a publicar:

```powershell
.\herramientas\entrega-iis\Registrar-ScriptDeActualizacion.ps1 `
  -Archivo '.\docs\sql\Facturacion.Actualizar-Completo-Sql2014.sql' `
  -Nombre 'Actualización de la base'
```

El registro `Facturacion.Server/Infra/ActualizacionBase/ScriptsPermitidos.json`
contiene solo nombres y huellas, no instrucciones SQL. Los SQL siguen siendo
archivos locales que se entregan por separado. Se admiten separadores `GO`, sin
repeticiones; el archivo debe usar la base actual, sin `USE` ni comandos SQLCMD.

Durante la ejecución se bloquean las API de trabajo y se espera a que terminen
las peticiones y tareas internas abiertas. El modo de revisión requiere **un solo
proceso de IIS y una sola instancia** contra esa base; no cubre escrituras hechas
desde otras aplicaciones o SSMS. No reiniciar ni reemplazar archivos durante la
operación. IIS no debe reciclarse ni finalizar por inactividad durante ese tiempo.

Los scripts administran sus transacciones: algunos cambios FULLTEXT no admiten
una transacción exterior. Ante un error puede haber bloques ya confirmados. El
módulo detiene el resto, mantiene la pausa y registra el resultado en
`cnf/actualizaciones-base/estado.json` e `historial.jsonl`, con usuario, base, huella
y fechas. Si el proceso se interrumpe, no vuelve a ejecutar el SQL automáticamente.
Revisar/restaurar el respaldo o volver a cargar el acumulado probado según el fallo.
Un resultado correcto compara las columnas, claves e índices requeridos por el
modelo actual; no supone que una base creada en SSMS tenga todo el historial de EF.
No ejecutar `dotnet ef database update` automáticamente sobre ella.

Los archivos cargados quedan privados en `cnf/actualizaciones-base`. No se registran
contraseñas ni el contenido del SQL en los mensajes del módulo. El respaldo no se
genera desde la aplicación: debe estar disponible antes de autorizar la ejecución.

Manual técnico · 6 de noviembre de 2026 · [Índice](../README.md)

## 1. Requisitos y responsabilidades

La entrega para revisión se instala en Windows x64 con IIS. Se necesita AspNetCoreModuleV2, incluido en el ASP.NET Core Hosting Bundle, una base SQL existente y una identidad de ejecución con acceso a la configuración privada.

La publicación autocontenida incluye .NET 10.0.14. El módulo IIS sigue siendo necesario. Si se utiliza publicación dependiente del framework, el servidor necesita el runtime compatible. No mezclar ejecutable, DLL, archivos de runtime y web.config de publicaciones diferentes.

El administrador del servidor configura IIS, SQL, DNS, certificado y permisos. La aplicación configura su conexión y el operador principal mediante el asistente. FTP únicamente copia los archivos.

SQL Server 2014 corresponde al motor versión 12 y a compatibilidad 120. Se comprobó la sintaxis del script y su ejecución con compatibilidad 120 en un motor posterior; falta validación en un motor 2014 nativo. No confundir esta compatibilidad SQL con los requisitos del sistema operativo para ejecutar .NET 10.

## 2. Preparar o actualizar la base

Antes de ejecutar SQL, identificar el esquema de partida y hacer un respaldo recuperable. El nombre de la base se elige en SSMS; los nombres de prueba del desarrollo no son obligatorios.

- **Base nueva vacía:** utilizar el esquema de instalación `docs/sql/Facturacion.Instalacion.sql`, conforme a sus instrucciones.
- **Base del primer paquete, creada desde el script original de SSMS:** utilizar `docs/sql/Facturacion.Actualizar-Completo-Sql2014.sql`. En la entrega se copia como `Actualizar-base.sql`.
- **Base con otra estructura:** comparar antes de ejecutar. El acumulado valida su punto de partida y rechaza estructuras desconocidas.

El acumulado agrega los cambios posteriores al esquema original, transforma los datos que cambiaron de propiedad y actualiza estructuras y procedimientos. Incluye el paso de catálogos y timbres a cuenta, permisos, facturas del proveedor y ampliaciones de documentos. No crea el servidor SQL, logins ni un respaldo.

La versión del 6 de noviembre también admite la exportación original que omitió sus índices adicionales. Los completa dentro de la primera transacción, conserva los compatibles y rechaza índices distintos o datos duplicados que impidan crear claves únicas. No eliminar registros ni quitar validaciones para evitar esos errores: revisar el caso señalado.

Conservar sus comprobaciones, bloqueos y mensajes aunque sean breves. Contiene varias etapas; no debe suponerse que todo el archivo se revierte como una única transacción. Si falla, guardar el error y revisar el estado antes de reintentar.

Que `__EFMigrationsHistory` esté vacío no significa que no haya tablas. No ejecutar `dotnet ef database update` sobre una base creada por SSMS sin reconciliar previamente su historial. El asistente de conexión no ejecuta este script ni crea tablas faltantes.

### Usuario SQL

El usuario de la aplicación necesita conexión y lectura/escritura sobre sus tablas, además de ejecución de los procedimientos operativos. El responsable debe ajustar los permisos al despliegue; no necesita conceder `sysadmin` o `db_owner` para el funcionamiento ordinario.

Los seis procedimientos operativos identificados son `ReservarFolio`, `ReservarTimbre`, `ConfirmarTimbre`, `DevolverTimbre`, `AcreditarCompra` y `ReservarFolioProveedor`. Tras actualizar, comprobar también el permiso sobre los nuevos procedimientos. Aplicar el esquema requiere una cuenta de mantenimiento con permisos DDL distintos a los del usuario ordinario.

Con autenticación SQL, el login del servidor debe estar vinculado con un usuario autorizado en esa base. Con autenticación Windows bajo IIS se conecta la identidad del proceso, no el usuario que abrió el navegador ni necesariamente quien entra a SSMS. Para SQL remoto se necesita una identidad reconocida por ese servidor o un login SQL; una identidad virtual local no se traslada por copiar la carpeta.

## 3. Preparar la publicación

Desde el código fuente, con SDK .NET 10, se puede generar una carpeta nueva:

```powershell
.\herramientas\entrega-iis\Publicar-Compatible.ps1 -Salida 'C:\Entregas\Facturacion' -NombreHost 'facturacion.ejemplo.com'
```

Escribir solo el nombre, sin protocolo ni puerto. El comando produce `sitio` y copia el script de actualización si está disponible en local; no configura DNS/IIS y no introduce parámetros SQL. Los archivos `.sql` no se incluyen en Git y se entregan por separado. El esquema de instalación puede regenerarse con `docs/sql/Generar-Script.ps1`; el acumulado para la base original se solicita aparte. Revisar siempre la carpeta resultante antes de transferirla.

En el servidor, la ruta física de IIS debe contener:

```text
sitio/
  Facturacion.Server.exe       Backend autocontenido
  Facturacion.Server.dll
  Facturacion.Server.runtimeconfig.json
  web.config
  appsettings*.json            Configuración general del paquete
  wwwroot/                    Cliente y recursos públicos
  ...                         Runtime, dependencias y recursos SAT publicados
  cnf/                        Configuración privada de esta instalación
```

No apuntar IIS a `wwwroot`. No copiar únicamente esa subcarpeta. Conservar todos los recursos de la publicación, incluidos catálogos, XSD y XSLT.

## 4. Configurar IIS y el nombre

1. Crear un grupo de aplicaciones de 64 bits, sin CLR administrado y con identidad definida.
2. Crear el sitio y seleccionar la carpeta `sitio`.
3. Configurar el binding con protocolo, IP, puerto y hostname.
4. Para HTTPS, elegir un certificado válido para el nombre utilizado.
5. Configurar DNS o la resolución local para que ese nombre apunte al servidor.
6. Permitir el puerto necesario en la red y firewall.
7. Revisar que `AllowedHosts` permita ese nombre.

Ejemplo de variable, conservando la estructura del web.config generado:

```xml
<environmentVariable name="AllowedHosts" value="localhost;127.0.0.1;[::1];facturacion.ejemplo.com" />
```

No escribir `https://` ni el puerto en ese valor. Esta variable no crea un binding ni un registro DNS. Que localhost abra no demuestra que el hostname resuelva a la misma máquina.

En publicación autocontenida, el nodo ASP.NET Core utiliza `processPath=".\Facturacion.Server.exe"`, argumentos vacíos y `hostingModel="outofprocess"`. En la dependiente utiliza `processPath="dotnet"` y `arguments=".\Facturacion.Server.dll"`.

## 5. Permisos de carpetas

La identidad del grupo necesita **lectura y ejecución** en la publicación y **modificación** en `cnf` y cualquier almacén privado configurado fuera de ella. La modificación permite crear, reemplazar y eliminar temporales; escribir en un archivo existente no es equivalente.

Ejemplo para un grupo llamado Facturacion, en PowerShell elevado:

```powershell
New-Item -ItemType Directory -Path 'C:\Aplicaciones\Facturacion\sitio\cnf' -Force
icacls 'C:\Aplicaciones\Facturacion\sitio' /grant 'IIS AppPool\Facturacion:(OI)(CI)(RX)' /T
icacls 'C:\Aplicaciones\Facturacion\sitio\cnf' /grant 'IIS AppPool\Facturacion:(OI)(CI)(M)' /T
```

Adaptar nombres y rutas reales. Las ACL y herencias existentes pueden requerir revisión del administrador. Estar situado en System32 no significa tener permisos elevados.

En la modalidad actual junto al servidor, los temporales del asistente se crean dentro de `cnf`; no necesita modificación en la raíz solo para guardar la conexión. Si se utiliza una ruta privada externa, conceder ahí los permisos. Por compatibilidad, una instalación antigua puede seguir leyendo appsettings.Instalacion.json desde la raíz si todavía no existe en cnf; revisar ese caso antes de asumir la ubicación o moverlo.

## 6. Asistente de primer arranque

Abrir el sitio y el acceso del operador. Si falta configuración, el asistente conduce por estas etapas:

1. Capturar servidor/instancia y nombre de la base.
2. Elegir Windows o usuario SQL y proporcionar sus datos.
3. Probar conexión. Esto verifica acceso; no actualiza el esquema.
4. Guardar parámetros. La aplicación escribe un temporal en la carpeta privada y lo renombra como archivo definitivo si aún no existe una conexión guardada.
5. Si no existe operador principal, crearlo desde el formulario.
6. Completar la configuración pendiente y finalizar. El host reinicia su arranque para cargar la configuración guardada.
7. Verificar entrada al panel y preparación de catálogos.

La modalidad de revisión Staging actual no exige clave de instalación. Mantiene comprobaciones de origen; no debe exponerse un asistente sin completar como configuración de producción. No se precargan nombres ni credenciales SQL de otra instalación.

La carga de catálogos usa los archivos SAT del paquete y la base configurada. Si falla, revisar registros, recursos publicados, esquema y permisos. No borrar `cnf` para intentar corregir una tabla ausente.

## 7. Dónde se guarda la configuración

En la modalidad de revisión junto al servidor:

- `cnf/appsettings.Instalacion.json`: cadena de conexión cifrada. En Staging, el primer arranque actualizado convierte el formato anterior de versión 2 antes de cargarlo.
- `cnf/appsettings.ArranqueRevision.json`: configuración de arranque protegida, incluidas referencias y secretos generados por la aplicación.
- `cnf/llavero-instalacion`: llaves de protección de la configuración de instalación.
- `cnf/llavero-aplicacion`: llaves de protección de datos de la aplicación.
- `cnf/datos`: almacén privado predeterminado de esta modalidad.

Son ubicaciones predeterminadas; una instalación existente puede indicar otra ruta. Revisar su configuración antes de mover archivos. Los parámetros del SMTP del SaaS se administran desde el operador y los del SMTP de cada empresa desde su configuración; sus secretos se protegen. No están todos en web.config.

`web.config` define cómo IIS inicia la aplicación y sus variables de entorno. `appsettings*.json` define opciones generales. Conserva el llavero de instalación y la identidad de Windows del grupo IIS para volver a leer la conexión cifrada.

## 8. Configurar correo y revisión

Configurar por separado:

- **SMTP del SaaS:** códigos de registro, recuperación e invitaciones.
- **SMTP de cada empresa:** sus documentos. Servidor, puerto 465/587, usuario, contraseña y remitente completos; usar «Probar conexión».

El servidor valida el destino SMTP y rechaza direcciones privadas/locales. Si falla el de la empresa no se utiliza el del operador. Una conexión correcta no garantiza recepción; enviar una muestra autorizada y confirmar sus adjuntos.

El web.config de la entrega establece ASPNETCORE_ENVIRONMENT=Staging, Instalacion__ArranqueAutomatico=true, Instalacion__JuntoAlServidorRevision=true, Pac__Modo=Deshabilitado y RevisionDocumentos__Habilitada=true. La revisión sin PAC depende de Staging y de esas opciones de documentos de revisión. No trasladar esas opciones a producción como sustituto de una integración fiscal.

## 9. Sustituir una versión existente

1. Respaldar SQL, `cnf`, almacén privado externo y configuración IIS. Comprobar que el respaldo puede recuperarse.
2. Registrar ruta física, identidad, modalidad de publicación, hostname y opciones de entorno.
3. Detener el sitio/grupo para evitar DLL bloqueadas y escrituras durante el cambio.
4. Aplicar el script SQL adecuado y revisar su resultado.
5. Sustituir los archivos publicados conservando configuración privada y archivos históricos.
6. Conservar/adaptar el web.config del servidor. Si cambia la modalidad de publicación, actualizar también processPath, arguments y hostingModel según el paquete.
7. Revisar ACL y recursos SAT.
8. Iniciar y comprobar sesión, catálogos, documentos, PDF/XML y correo de muestra.

No borrar `cnf` ni sus llaves para actualizar. No mezclar DLL nuevas con dependencias antiguas. Un rollback puede necesitar restaurar base y archivos compatibles: copiar el ejecutable anterior no revierte el esquema.

Las protecciones DPAPI de Windows pueden depender del equipo y la identidad originales. Una copia a otro servidor no garantiza lectura de llaves; planificar y probar ese traslado con el administrador.

## 10. Diagnóstico

- **500.31:** runtime ausente/incompatible o publicación mezclada. Revisar modalidad y Hosting Bundle.
- **500.19:** configuración IIS o permisos de lectura.
- **Hostname falla y localhost funciona:** revisar DNS, binding, certificado, puerto y AllowedHosts.
- **Conecta a SQL pero no guarda parámetros:** revisar modificación de la carpeta privada y sus temporales.
- **No abre la base:** revisar instancia, base, identidad efectiva y usuario autorizado en ella.
- **Falla preparación SAT:** revisar el primer error del servidor, esquema, recursos y permisos.
- **Error criptográfico:** revisar identidad y llaves originales; no eliminarlas si ya protegen datos.

Para diagnóstico puntual puede activarse temporalmente stdout hacia `.\cnf\logs\stdout`, con permiso en esa carpeta. Desactivarlo al terminar y revisar el contenido antes de compartirlo. No publicar contraseñas ni archivos privados.

Ver [Pruebas y pendientes](Pruebas-y-pendientes.md) para conocer qué se probó y qué debe comprobar el servidor receptor.
