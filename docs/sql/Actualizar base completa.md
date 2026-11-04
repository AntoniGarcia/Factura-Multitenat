# Actualizar la base del ingeniero

Archivo para entregar: **Facturacion.Actualizar-Completo-Sql2014.sql**.

Parte del esquema de `script.sql` de `Facturacion1.1` que se entregó con IIS. Incluye
los cambios de las 15 migraciones posteriores, hasta `20261103_B_IshEnFactura`.
No ejecutar primero los scripts anteriores: este archivo ya los incluye.

## Pasos

1. Respaldar la base y comprobar primero la actualización en una copia restaurada.
2. Detener el grupo de aplicaciones del sitio en IIS.
3. Abrir el archivo completo en SQL Server Management Studio. Seleccionar la base
   del sistema, sin cambiar su nombre ni usar `master`.
4. Ejecutarlo con una cuenta que pueda modificar tablas, procedimientos, permisos
   y catálogos de búsqueda. SQL Server debe tener búsqueda de texto completo con el
   idioma español 3082. La versión mínima es SQL Server 2014.
5. Esperar el resultado: **Actualización completa: cambios de las 15 migraciones aplicados.**
   Si aparece un error, conservarlo y revisar antes de iniciar la aplicación.
6. Copiar la publicación nueva. Conservar `cnf`, `web.config`, las llaves y los demás
   archivos privados del servidor.
7. Iniciar el grupo de IIS, volver a iniciar sesión y comprobar documentos,
   clientes, productos, usuarios, compras y correo.

El usuario SQL de la aplicación conserva sus permisos habituales. Necesita ejecutar
también `dbo.ReservarFolioProveedor` para emitir las facturas de compras. Si ya tiene
permiso de ejecución en la base o en el esquema, lo hereda. Si sus permisos se
concedieron por procedimiento, el administrador debe conceder este permiso al mismo
usuario de la aplicación:

```sql
GRANT EXECUTE ON OBJECT::dbo.ReservarFolioProveedor TO [usuario_de_la_aplicacion];
```

## Cambios incluidos

1. Subpermisos por tipo de documento y catálogo.
2. Límites de usuarios y empresas por cuenta.
3. Permisos del usuario iguales en sus empresas asignadas.
4. Bolsa y compras de timbres por cuenta.
5. Clientes y productos compartidos dentro de la cuenta.
6. Empresa de alta en clientes y productos.
7. ID interno y detalle de productos.
8. Monedas por empresa.
9. SMTP obligatorio de la empresa para sus comprobantes.
10. Datos fiscales y certificados del proveedor del sistema.
11. Borradores de facturas de compras de timbres.
12. Intentos de emisión, folios del proveedor y permisos del operador principal.
13. Claves SAT en paquetes y su copia en compras nuevas.
14. Valores predeterminados `81112106` y `E48` en los paquetes.
15. Campos de ISH en facturas y conceptos.

Se conservan las nueve columnas históricas de correo que faltaban en el esquema
entregado y se crean los tres índices de búsqueda SAT. El script no configura el
PAC, la pasarela, el correo, el CSD ni la conexión SQL. No copia datos de desarrollo.
La migración de SMTP retira las tres opciones obsoletas de `CorreosDeEmpresa`;
conserva servidor, usuario, contraseña cifrada y remitente de la empresa.

## Ejecución y recuperación

No crea otra base ni depende de `__EFMigrationsHistory`. No agrega registros falsos
a ese historial. La versión de esta entrega se registra en una propiedad de la base.
Para actualizar posteriormente esta instalación, seguir usando scripts que partan
de su estructura real.

Primero convierte la estructura de cuenta y prepara la búsqueda de texto completo.
Después aplica juntas las siete migraciones nuevas dentro de una transacción.
Si falla esa segunda parte, la revierte y permite volver a ejecutar el archivo
después de resolver la causa. La primera parte conserva su avance y no repite la
conversión de saldos o clientes. Repetir una ejecución terminada no vuelve a modificar
los registros ni duplica la bitácora.

Si hay que volver a la versión anterior, restaurar tanto el respaldo de la base como
la publicación anterior. Las DLL anteriores no corresponden al esquema actualizado.

## Revisión

Se comprobó la sintaxis con el analizador de SQL Server 2014, incluido el SQL dinámico.
La ejecución se prueba en bases temporales creadas desde el `script.sql` entregado,
con compatibilidad 120. Se compara el resultado con el modelo actual del proyecto:
columnas, tipos, campos obligatorios, índices y relaciones. Se verifican datos
históricos, saldos, permisos, SMTP, ejecución repetida y recuperación de una interrupción.
La comprobación final en el motor SQL Server 2014 del ingeniero sigue pendiente.

También se comparó directamente una base temporal actualizada desde el esquema original
con `FacturacionDev`, consultando la principal solo para lectura. Se revisaron las tablas,
columnas, tipos, índices, relaciones, valores predeterminados, restricciones,
procedimientos, búsqueda de texto completo y permisos del sistema. El historial de EF
y los objetos de diagramas de SSMS no se requieren en la instalación del ingeniero.
El resultado coincidió en las 87 tablas del sistema, 901 columnas, 176 índices,
53 relaciones, 23 valores predeterminados, dos restricciones, seis procedimientos,
tres índices de texto completo y los 66 permisos. No se detectaron diferencias de
estructura en esos objetos. La base temporal se eliminó al terminar.
