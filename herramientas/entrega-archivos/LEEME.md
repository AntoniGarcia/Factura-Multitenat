# Entrega sin instalador

El ZIP contiene solo la publicación. Copiar todo su contenido a la carpeta física
del sitio IIS, no dentro de `wwwroot`. No ejecutar instaladores ni scripts.
IIS, Hosting Bundle .NET 10, el certificado y la base deben estar preparados.

Antes de iniciar, en `web.config` buscar `AllowedHosts` y sustituir
`facturacion-prueba.local` por el hostname real. No escribir protocolo, puerto ni
rutas. Ejemplo: `localhost;127.0.0.1;[::1];facturacion.empresa.com`.
El hostname y su certificado también deben estar configurados en el enlace IIS.

El grupo debe tener perfil cargado, ejecutar en 64 bits y usar Sin código
administrado. Su identidad necesita lectura y ejecución en la carpeta de la
aplicación y Modificar únicamente en `cnf/`, sus subcarpetas y archivos. El
administrador puede crear `cnf/` vacía y asignar esos permisos antes de iniciar.
Estos permisos los prepara el administrador; copiar archivos no los concede.

Abrir `https://HOSTNAME/operador`. Si el enlace utiliza otro puerto HTTPS,
incluirlo en la dirección. Capturar los parámetros SQL y crear al primer operador.
No se pide clave de instalación. Los catálogos faltantes se preparan al arrancar.
El asistente no crea ni instala el esquema SQL.

La conexión queda legible en `cnf/appsettings.Instalacion.json`.
El archivo temporal también se crea en `cnf/`; no requiere modificar la carpeta
principal. La conexión se vuelve a leer desde allí al reiniciar.
Si contiene contraseña SQL, cualquier persona con acceso al archivo puede leerla.
`cnf/` conserva llaves, configuración interna y archivos privados fuera de `wwwroot`.
No borrar ni reemplazar esos archivos al actualizar. No trasladarlos a otra PC
como si fueran parte de la publicación: algunas llaves dependen de Windows.

Esta modalidad solo funciona en Staging para revisión. No incluye PAC ni pasarela.
Completar la configuración antes de dar acceso a revisores. Si la base se creó
solo con `FacturacionBD.sql`, revisar los datos iniciales y permisos de ejecución
pendientes antes de probar todos los módulos.

Para actualizar una instalación que ya tiene datos: respaldar, detener solo su
grupo, conservar los ajustes del servidor en `web.config` y toda la carpeta `cnf/`, sustituir
la publicación y volver a iniciar. Si usaba una carpeta externa, no activar este
modo como una migración automática: mantener sus rutas y llaves hasta planear
el cambio. Esta entrega está preparada para una instalación nueva.

Si una versión anterior ya guardó `appsettings.Instalacion.json` junto a las DLL,
se sigue leyendo sin moverlo ni reemplazarlo. Conservarlo también al actualizar;
no trasladarlo a mano si está cifrado. Las instalaciones nuevas guardan en `cnf/`.
No copiar un `cnf/` generado en otra computadora: crear las llaves en el servidor.
