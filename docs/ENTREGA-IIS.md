# Entrega para revisión en Windows / IIS

La alternativa vigente sin instalador está en
`entregas/Facturacion-IIS-Cnf-20261024-v2.zip`. Contiene directamente la
publicación, sin scripts SQL, CMD ni PowerShell. Sus pasos están en
[la guía de entrega sin instalador](../herramientas/entrega-archivos/LEEME.md).
Guarda la conexión legible, los temporales y las llaves internas en `cnf/`;
requiere Modificar de la identidad IIS solo en `cnf/`, lectura y ejecución en
la carpeta principal y ajustar `AllowedHosts`. El perfil del grupo debe estar
cargado para la protección de las llaves de Windows. No distribuir un `cnf/`
generado en otra computadora; conservar el del servidor al actualizar.
El paquete con instalador descrito abajo se conserva como alternativa anterior.

La guía vigente de instalación se mantiene junto a los archivos de entrega:
[Configuracion del servidor.md](<../herramientas/entrega-iis/Configuracion del servidor.md>).

La entrega actualizada está en `entregas/Facturacion-IIS-Actualizado-20261022.zip`.
Para instalaciones existentes, seguir [Actualizar el sistema.md](<../herramientas/entrega-iis/Actualizar el sistema.md>)
y conservar la configuración privada y el `web.config` del servidor.
Contiene `sitio/`, el script de estructura en `base-datos/`, una guía de permisos
SQL y la comprobación de archivos `VERIFICAR.ps1`. No incluye configuración privada.
Incluye `INSTALAR.cmd`, `INSTALAR.ps1`, `PREPARAR-IIS.ps1` y `Pasos de instalacion.md`.
Con el sitio IIS ya creado y su carpeta vacia, ejecutar INSTALAR.cmd como administrador,
seleccionar el sitio y confirmar: copia la publicacion y prepara rutas y permisos.
El paquete debe extraerse fuera de cualquier sitio publico. No copiar los archivos al
destino antes de usar el instalador. Rechaza destinos ocupados y grupos compartidos;
no actualiza ni migra instalaciones. No cambia SQL, certificados, enlaces ni firewall.
La base y su usuario ya deben estar preparados; sus datos se capturan en el wizard.

El sitio se publica como raíz de IIS con Hosting Bundle de .NET 10 y un grupo de
64 bits sin código administrado, perfil cargado y modelo `outofprocess`. El
`web.config` de revisión usa Staging, PAC deshabilitado y arranque automático.
El administrador debe ajustar AllowedHosts, HTTPS y la ruta privada absoluta fuera
del sitio antes de arrancar. No se utiliza INICIAR.cmd ni se necesitan SDK o Visual Studio.

El asistente prueba y guarda la conexión SQL y permite crear al primer operador.
La siguiente entrega de revisión activa `Instalacion__ConexionSinCifrarRevision=true`:
la conexión se guarda legible fuera del sitio, solo en Staging. No modifica ni convierte
conexiones guardadas anteriormente y no elimina el cifrado del resto de secretos.
Desde el 22 de octubre se eliminó la clave de instalación del formulario, servidor
y archivos nuevos. En Staging se admite configurar por hostname; fuera de Staging
solo se admite el acceso local. Completar la instalación antes de compartir el sitio.
Se conservan el cifrado y las llaves internas.
El esquema se instala por separado; los catálogos faltantes se cargan automáticamente.
El SMTP del sistema se configura después desde el operador antes de probar correos.
No copiar llaves ni secretos desde la PC de desarrollo al servidor.

La prueba local y su alcance están en
[PRUEBA-IIS-LOCAL.md](PRUEBA-IIS-LOCAL.md).
Luis confirmó acceso, conservación al reciclar IIS y la secuencia de
catálogos/borrador/PDF. La entrega aún debe probarse en el servidor de destino;
no es una certificación fiscal ni incluye PAC o pasarela de pagos.

El FTP es solo el medio de transferencia: únicamente el contenido de `sitio/`
va a la raíz física acordada, nunca los scripts SQL o esta documentación.
No transferir credenciales por FTP sin cifrado.
