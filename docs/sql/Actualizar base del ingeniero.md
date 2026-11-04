# Actualizar la base del servidor

La entrega completa está en **Facturacion.Actualizar-Completo-Sql2014.sql**.
Consultar [Actualizar base completa](Actualizar%20base%20completa.md) para la base
creada con el esquema original entregado al ingeniero. Los scripts de este documento
corresponden a la actualización anterior.

Para la base creada con el `script.sql` compartido el 31 de octubre, usar
**Facturacion.Actualizar-Desde-Script-Sql2014.sql** y las instrucciones de
[Actualizar base desde script](Actualizar%20base%20desde%20script.md).
El procedimiento de este documento requiere un historial de migraciones completo.

Archivo para entregar: **Facturacion.Actualizar-Zip20261024-Sql2014.sql**.

Se tomó como referencia `Facturacion1024.2.zip`, que coincide con la entrega
`Facturacion-IIS-Cnf-20261024-v2.zip`. Se revisaron los atributos de migración de
`Facturacion.Server.dll`: contiene 59 migraciones y termina en
`20261009161413_20261009_A_PermisosPorSeccionYTitular`.

El script lleva esa base a las 67 migraciones de la versión actual. Termina en
`20261029220708_20261029_B_MonedasDeEmpresa`. No crea otra base, no copia los datos
locales y no contiene contraseñas. Usa el historial para omitir lo ya aplicado,
como los scripts idempotentes de [EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#idempotent-sql-scripts).

## Pasos en SQL Server Management Studio

1. Respaldar la base del servidor y comprobar que se pueda restaurar. Conservar
   también la publicación anterior y su configuración fuera de la carpeta pública.
2. Probar primero el script en una copia restaurada de esa base.
3. Detener el grupo de aplicaciones de este sistema en IIS para evitar que entren
   operaciones durante la actualización.
4. Abrir el archivo SQL en SSMS y seleccionar arriba la base del sistema. No usar
   `master`. El nombre real de la base lo elige el ingeniero; no hay que cambiarlo
   dentro del archivo.
5. Conectarse con una cuenta que tenga permiso para modificar tablas, índices,
   procedimientos, permisos SQL y registros de esa base. El usuario limitado que
   utiliza la aplicación puede no tenerlos; no hace falta ampliar sus permisos.
6. Ejecutar el archivo completo. Al terminar debe aparecer
   **Actualización terminada. La base tiene las 67 migraciones de esta versión.**
7. Reemplazar la aplicación por una publicación nueva del proyecto actual,
   conservando el `web.config` del servidor, `cnf` y los demás datos de su instalación.
   El ZIP anterior no es compatible con todos los cambios del esquema nuevo.
8. Iniciar el grupo de IIS. Volver a iniciar sesión y comprobar acceso a empresas,
   permisos, clientes, productos, bolsa de timbres, monedas y documentos existentes.

Si el script detecta una base anterior, un historial fuera de orden o migraciones
que no reconoce, se detiene. No borrar filas de `__EFMigrationsHistory` ni ejecutar
solo fragmentos para saltar esa comprobación. Solicitar el resultado de:

```sql
SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;
```

Un error durante la actualización revierte sus cambios mediante una transacción.
Se puede ejecutar de nuevo después de resolver la causa; las migraciones completas
se omiten. Si la actualización terminó y después hay que regresar a la versión
anterior, restaurar tanto el respaldo de la base como la publicación anterior: no
basta con volver a copiar las DLL antiguas.

## Qué cambia

- Permisos por acción y por tipo de documento. Al final quedan 66 permisos.
- Límites de usuarios activos y de empresas por cuenta. Los límites iniciales se
  ajustan al uso existente para no dejar cuentas excedidas.
- Un mismo usuario recibe la unión de sus permisos en todas sus empresas asignadas.
  Revisar esas casillas después de actualizar, porque algunos accesos se amplían.
- Bolsas, compras, movimientos y paquetes exclusivos de timbres pasan a la cuenta.
  Se suman los saldos de sus empresas y se conservan disponibles y reservados.
- Clientes y productos se comparten entre empresas de la misma cuenta. Los clientes
  con RFC no genérico repetido se fusionan en el más antiguo. Se actualizan sus
  referencias y se renumeran claves y códigos en conflicto; queda bitácora. Los RFC
  genéricos se conservan por separado y no se mezclan cuentas distintas.
- Se agrega la empresa de alta de clientes y productos, cuando se puede identificar.
- Productos incorpora `IdInterno` y `Detalle` opcionales.
- Se crea `MonedasDeEmpresa` y el permiso `configuracion_monedas`. Se conservan las
  monedas utilizadas en documentos existentes, incluidos los borradores.

Los comprobantes, sus conceptos y sus copias fiscales no se sustituyen por datos de
los catálogos. Se mantienen las series y los folios de cada empresa. El script no
configura PAC ni pasarela y no actualiza los archivos de la aplicación.

## Alcance de la comprobación

El 30 de octubre se revisaron 108 bloques con `TSql120Parser`, incluido el SQL
dinámico, sin errores de sintaxis. Se ejecutaron las ocho migraciones en una base
temporal con dos cuentas, tres empresas y datos ficticios. Se comprobó la separación
entre cuentas, la fusión de duplicados, la renumeración, los saldos disponibles y
reservados, los permisos, la moneda de un borrador y su receptor histórico. También
se verificó que un fallo al final revierta la actualización y que repetirla no
duplique registros. La base temporal se eliminó al terminar; no se modificó la base
local de trabajo ni la del ingeniero.

La prueba funcional fue en SQL Server 17 con nivel de compatibilidad 120; esto no
equivale a ejecutar el motor SQL Server 2014. El ingeniero debe hacer la prueba final
en una copia de su base en ese servidor.

El resultado iguala el esquema y las conversiones de esta versión según el historial;
no garantiza igualar cambios manuales que se hayan hecho fuera de las migraciones.
Los catálogos SAT y los datos particulares del servidor se conservan.
