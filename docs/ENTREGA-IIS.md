# Entrega para revisión en Windows / IIS

La guía vigente de instalación se mantiene junto a los archivos de entrega:
[LEEME-IIS.md](../herramientas/entrega-iis/LEEME-IIS.md).

La entrega con instalador se genera en `entregas/Facturacion-Revision-IIS-Instalador-20261021.zip`.
Contiene `sitio/`, el script de estructura en `base-datos/`, una guía de permisos
SQL y la comprobación de archivos `VERIFICAR.ps1`. No incluye configuración privada.
Incluye `INSTALAR.cmd`, `INSTALAR.ps1`, `PREPARAR-IIS.ps1` y `EMPEZAR-AQUI.md`.
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

El asistente prueba y guarda SQL cifrado y permite crear al primer operador.
Desde el 21 de octubre, la entrega de revisión omite la clave al abrir el asistente
desde el propio servidor con localhost/loopback y la opción explícita
`Instalacion__PermitirSinClaveLocal=true`. El acceso remoto conserva la clave;
Production no permite esta excepción. No se elimina el cifrado ni las llaves internas.
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
