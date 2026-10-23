# Entrega sin instalador

El ZIP contiene solo la publicación. Copiar todo su contenido a la carpeta física
del sitio IIS, no dentro de `wwwroot`. No ejecutar instaladores ni scripts.
IIS, Hosting Bundle .NET 10, el certificado y la base deben estar preparados.

Antes de iniciar, en `web.config` buscar `AllowedHosts` y sustituir
`facturacion-prueba.local` por el hostname real. No escribir protocolo, puerto ni
rutas. Ejemplo: `localhost;127.0.0.1;[::1];facturacion.empresa.com`.
El hostname y su certificado también deben estar configurados en el enlace IIS.

El grupo debe tener perfil cargado, ejecutar en 64 bits y usar Sin código
administrado. Su identidad necesita escritura en la carpeta de la aplicación.
Estos permisos los prepara el administrador; copiar archivos no los concede.

Abrir `https://HOSTNAME/operador`. Si el enlace utiliza otro puerto HTTPS,
incluirlo en la dirección. Capturar los parámetros SQL y crear al primer operador.
No se pide clave de instalación. Los catálogos faltantes se preparan al arrancar.
El asistente no crea ni instala el esquema SQL.

La conexión queda legible en `appsettings.Instalacion.json`, junto a las DLL.
Si contiene contraseña SQL, cualquier persona con acceso al archivo puede leerla.
`cnf/` conserva llaves, configuración interna y archivos privados fuera de `wwwroot`.
No borrar ni reemplazar esos archivos al actualizar. No trasladarlos a otra PC
como si fueran parte de la publicación: algunas llaves dependen de Windows.

Esta modalidad solo funciona en Staging para revisión. No incluye PAC ni pasarela.
Completar la configuración antes de dar acceso a revisores. Si la base se creó
solo con `FacturacionBD.sql`, revisar los datos iniciales y permisos de ejecución
pendientes antes de probar todos los módulos.

Para actualizar una instalación que ya tiene datos: respaldar, detener solo su
grupo, conservar `web.config`, `appsettings.Instalacion.json` y `cnf/`, sustituir
la publicación y volver a iniciar. Si usaba una carpeta externa, no activar este
modo como una migración automática: mantener sus rutas y llaves hasta planear
el cambio. Esta entrega está preparada para una instalación nueva.
