# Cómo instalar el sistema

Estos pasos son para instalar por primera vez. El sitio IIS y la base de datos ya deben existir.

1. El sitio debe tener una carpeta fisica vacia, un grupo exclusivo con identidad
   ApplicationPoolIdentity y un enlace HTTPS con certificado valido.
2. Extraer TODO el ZIP en una carpeta de trabajo fuera de cualquier sitio IIS.
   No copiar manualmente `sitio/` al destino antes de ejecutar el instalador.
3. Clic derecho en **INSTALAR.cmd** -> **Ejecutar como administrador**.
4. Elegir el sitio de la lista, revisar rutas y confirmar. El instalador copia la
   publicacion, prepara la carpeta privada y permisos, y arranca ese sitio.
5. Abrir la direccion que muestra y completar el wizard con los datos de la base
   existente. Si falta el operador principal, crearlo desde el formulario.

No crea ni modifica bases, logins o permisos SQL; no ejecuta el script de estructura.
No crea sitios, certificados, enlaces ni reglas de firewall. No cambia otros sitios.
No reemplaza una publicacion o carpeta privada existente: esta fase NO actualiza.
Si el destino ya tiene archivos, detenerse y conservarlos; no vaciarlo para forzar una instalacion nueva.

La carpeta privada queda en `%ProgramData%\FacturacionRevision\NOMBRE_DEL_GRUPO\cnf`.
Esta entrega de revisión no pide clave de instalación, incluso al entrar por hostname.
Completar el asistente antes de compartir el acceso; quien pueda abrirlo podrá configurarlo.
El certificado debe corresponder a la direccion y ser confiable en el navegador.
Si Windows bloquea los scripts, revisar el origen y la politica del servidor;
el lanzador no desactiva ni evita la politica de ejecucion.

Para comprobar el plan sin modificar el servidor, desde PowerShell como administrador:

```powershell
.\INSTALAR.ps1 -Sitio 'NOMBRE_REAL_DEL_SITIO' -WhatIf
```

## Preparacion manual alternativa

Requiere IIS con su consola y herramientas de administración, Hosting Bundle .NET 10
y una base con el esquema del sistema y su usuario. Este paquete es de revisión, sin PAC
ni pasarela. No ejecuta scripts SQL automáticamente ni instala servicios.

1. Extraer TODO el ZIP y dejarlo en su ubicación definitiva. No ejecutar dentro del ZIP.
2. Abrir **Windows PowerShell como administrador**, entrar a la carpeta extraída donde
   están este archivo y `PREPARAR-IIS.ps1` y ejecutar:

```powershell
.\PREPARAR-IIS.ps1 -Grupo 'Marpa' -NombreHost 'localhost'
```

`Marpa` será el grupo exclusivo de esta aplicación. Para un dominio, sustituir
`localhost` por el nombre DNS real, sin https:// ni puerto. Se conserva localhost
como alternativa para completar el asistente sin clave desde el servidor.
Puede consultar previamente el plan, sin modificar nada, añadiendo `-WhatIf`.

3. En IIS crear el sitio (o seleccionar el que apunta a ESTA publicación), elegir
   el grupo `Marpa`, indicar la ruta física de `sitio` que imprime el comando y
   configurar su enlace **HTTPS con certificado válido**. No apuntar a `wwwroot`.
4. Abrir `/operador` desde el navegador del propio servidor mediante localhost y
   el puerto HTTPS configurado. Capturar servidor SQL, base y usuario/contraseña;
   guardar y crear al primer operador si no existe. Los catálogos faltantes se cargan solos.

El asistente ya no genera ni solicita una clave de instalación. En Staging admite
otras PC; fuera de Staging solo permite configurar desde el servidor.
Las contraseñas SQL y del operador se siguen utilizando.

## Qué hace el comando

- Crea el grupo si falta, sin código administrado, de 64 bits y con perfil habilitado.
- Crea `%ProgramData%\FacturacionRevision\Marpa` y restringe su acceso a SYSTEM,
  Administradores y la identidad `IIS APPPOOL\Marpa`.
- Da a esa identidad lectura/ejecución sobre `sitio`.
- Ajusta el `web.config` de revisión, AllowedHosts y rutas; guarda un respaldo privado.
- No reinicia otros sitios, no configura SQL, no crea certificados ni abre puertos.

Puede elegir otra carpeta privada con `-DirectorioPrivado 'D:\FacturacionDatos\Marpa'`.
No puede quedar dentro del sitio ni contener archivos de otra instalación. La preparación
es repetible para el mismo grupo, publicación y carpeta identificada; no regenera llaves.
No es un migrador: rechaza cambiar la ruta privada de una aplicación ya configurada.

No ejecutar el preparador de una instalación NUEVA sobre la instalación anterior de Luis
sin revisar sus rutas. Para actualizar una instalación existente conservar web.config,
identidad y carpeta privada; seguir Configuracion del servidor.md.

Si el usuario SQL ya está creado, capturarlo en el asistente. Si se usa autenticación
Windows, SQL debe reconocer a `IIS APPPOOL\Marpa` en la misma máquina, o a la cuenta
de dominio apropiada si SQL es remoto. Dar acceso a SQL sigue siendo tarea de su administrador.

Si Windows bloquea un script descargado, revisar primero su origen y contenido y seguir
la política de ejecución del servidor. No desactivar globalmente esa política.

## Abrir el sistema desde otras computadoras

Sí se puede usar un hostname, por ejemplo `facturacion.empresa.com`. Primero debe
estar en el enlace HTTPS del sitio IIS y tener un certificado para ese nombre.
El instalador toma los nombres de los enlaces del sitio y los permite en la aplicación.
No crea el dominio ni configura DNS.

En la misma red, el nombre debe resolver a la IP del servidor desde las otras PC.
`localhost` solo funciona en la computadora donde se abre el navegador.

Para entrar desde Internet, el encargado de la red debe configurar el DNS público,
la entrada HTTPS y el firewall. Si el servidor está detrás de un router, también
debe resolver el acceso externo; con CGNAT puede requerirse otra solución.
No se debe abrir el puerto de SQL Server al público. El instalador no hace estos cambios.
Antes de publicar, completar la instalación y revisar seguridad y permisos.
Este paquete sigue siendo de revisión, sin timbrado real ni pasarela de pagos.

## Comprobación de archivos

`Comprobacion de archivos.txt` contiene una firma SHA256 por archivo. Sirve para
revisar que la copia esté completa y no haya cambiado. No es una clave y no se
ejecuta. La revisión es opcional para instalar; el archivo no garantiza por sí
solo quién creó el paquete.

Para más detalles: `Configuracion del servidor.md`. Si ya está instalado, leer
`Actualizar el sistema.md` y no ejecutar otra vez el instalador.
