# Cómo actualizar el sistema

No ejecutar `INSTALAR.cmd` ni `PREPARAR-IIS.ps1` sobre una instalación ya configurada.
Estos preparan instalaciones nuevas, no actualizaciones.

1. Respaldar la base de datos, la publicación actual y su carpeta privada. Proteger
   los respaldos: contienen información y llaves de la instalación.
2. Extraer el ZIP nuevo en una carpeta de trabajo fuera de los sitios públicos.
3. Detener solo el grupo de aplicaciones de este sistema en IIS.
4. Conservar el `web.config` del servidor y cualquier configuración específica del
   destino. No reemplazarlos por los ejemplos del paquete.
   No se necesita ninguna opción para omitir la clave del asistente: se eliminó
   del sistema. Se pueden quitar las opciones antiguas `PermitirSinClaveLocal`
   y `PermitirSinClaveRevision` si aparecen. Mantener Staging para esta revisión
   y no compartir el sitio antes de completar la instalación.
5. Reemplazar la publicación completa con el contenido de `sitio/`, manteniendo
   el `web.config` y la configuración del destino. No copiar la carpeta `sitio`
   dentro de `wwwroot`. No dejar archivos antiguos de la aplicación mezclados con
   los nuevos; conservar la versión anterior únicamente en el respaldo externo.
6. No borrar, mover ni sustituir la carpeta privada, sus llaveros o archivos cifrados.
   Mantener la misma identidad del grupo IIS y sus permisos.
7. Esta entrega del 22 de octubre contiene cambios de interfaz, consulta del
   estado fiscal y eliminación de la clave del asistente; no añade migraciones respecto al paquete del
   21 de octubre. No ejecutar de nuevo el script SQL para estos cambios.
8. Iniciar el grupo y abrir el sistema. Si aparece el asistente de instalación en
   un sitio que ya estaba configurado, detenerse y revisar rutas, identidad y
   permisos. No crear otro operador ni borrar llaves para resolverlo.
9. Comprobar acceso, configuración, catálogos y apertura de un borrador existente.
   Si el navegador avisa de una versión nueva, recargar. No probar cobros ni
   timbrado real con este paquete de revisión.

Si falla la actualización, detener el grupo y restaurar la publicación anterior
con su configuración. No modificar la base ni las llaves como parte de ese retorno.

Solo el contenido de `sitio/` pertenece a la raíz física del sitio. El ZIP,
los scripts SQL, los respaldos y estas instrucciones deben quedar fuera.
