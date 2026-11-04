# Actualizar la base creada con script.sql

Para la entrega completa del 4 de noviembre de 2026, usar
**Facturacion.Actualizar-Completo-Sql2014.sql** y las instrucciones de
[Actualizar base completa](Actualizar%20base%20completa.md). Incluye los cambios de
las 15 migraciones. El archivo descrito abajo conserva solo la actualización anterior.

Entregar **Facturacion.Actualizar-Desde-Script-Sql2014.sql**. Este es el archivo
para la base creada con el `script.sql` de `Facturacion1.1` que se compartió el
31 de octubre. Sustituye, para este caso, al script de actualización por historial.

No hay que ejecutar otra vez el archivo que crea la base. El nuevo archivo modifica
la base existente y conserva su nombre, usuarios SQL y datos. No crea bases ni
requiere instalar .NET para ejecutar el SQL desde SSMS.

## Cómo aplicarlo

1. Respaldar la base y comprobar primero el archivo en una copia restaurada.
2. Detener el grupo de aplicaciones del sistema en IIS.
3. Abrir `Facturacion.Actualizar-Desde-Script-Sql2014.sql` en SQL Server Management
   Studio y seleccionar arriba la base del sistema. No seleccionar `master`.
4. Usar una cuenta administrativa de esa base que pueda modificar tablas y registros,
   recrear procedimientos, restaurar sus permisos, registrar una propiedad extendida
   y crear catálogos e índices de texto completo. SQL Server debe tener instalada
   la característica de búsqueda de texto completo y el idioma español 3082.
   No hace falta ampliar los permisos del usuario con el que trabaja la aplicación.
5. Ejecutar el archivo completo. No requiere cambiar el nombre de la base dentro
   del archivo. Debe terminar sin errores de SQL; ya no imprime mensajes de avance.
6. Reemplazar la publicación de la aplicación por una nueva del proyecto actual.
   Conservar `web.config`, la carpeta `cnf` completa y sus llaves. También conservar
   los archivos privados de la aplicación si están en otra carpeta del servidor.
   La publicación antigua no corresponde al esquema actualizado.
7. Iniciar el grupo de IIS y volver a iniciar sesión. Revisar empresas, usuarios,
   permisos, clientes, productos, timbres, monedas y documentos existentes.

## Qué hace

- Cambia los permisos anteriores por los permisos por acción y tipo de documento.
  Agrega el permiso de monedas; el resultado son 66 permisos. Si el script original
  dejó vacía la tabla de permisos, también la completa.
- Agrega los límites de usuarios y empresas por cuenta, ajustados al uso existente.
- Unifica por usuario los permisos de sus empresas asignadas. La unión puede ampliar
  sus accesos; el titular debe revisar las casillas después de actualizar.
- Convierte la bolsa y los paquetes exclusivos de timbres a la cuenta. Suma los
  disponibles y reservados de sus empresas sin mezclar cuentas diferentes. Compras
  y movimientos conservan la empresa de origen.
- Comparte clientes y productos dentro de la cuenta. Fusiona clientes con el mismo
  RFC no genérico en el más antiguo y actualiza sus referencias. Conserva separados
  los RFC genéricos. Renumera claves y códigos en conflicto y registra las conversiones
  en bitácora.
- Agrega la empresa de alta de los catálogos y los campos opcionales `IdInterno`
  y `Detalle` de productos.
- Crea las monedas por empresa y conserva las monedas de los documentos guardados,
  incluidos los borradores.
- Completa las nueve columnas históricas de correo en `ConfiguracionesEmpresa`,
  con sus tipos y valores predeterminados, para igualar la estructura de la base
  principal `FacturacionDev`. Conserva los valores si las columnas ya existen.
- Crea el catálogo y los tres índices de texto completo de productos y servicios SAT,
  colonias y municipios. Usa español 3082, sensibilidad a acentos y seguimiento
  automático como la principal. No borra los catálogos ni sus registros.

Los comprobantes mantienen sus copias fiscales del receptor y sus conceptos. Los
folios y series continúan por empresa. No se configura PAC, pasarela, SMTP ni los
parámetros del servidor. Tampoco se copian datos de desarrollo.

## Comprobaciones y repetición

El archivo no lee, crea, borra ni modifica `__EFMigrationsHistory`. La versión corta
comprueba las nueve tablas cuyo esquema cambia: bolsas, clientes, compras, cuentas,
monedas, movimientos, paquetes, productos e impuestos de productos. Revisa sus
columnas, tipos, campos obligatorios, índices, claves primarias y relaciones. El resto
de la base se supone igual al `script.sql` entregado; no hace una auditoría de todas
las tablas. Si los objetos revisados pertenecen a otra versión o están parcialmente
actualizados, se detiene. Ejecutar siempre el archivo completo.

La conversión de tablas, datos y columnas de correo va dentro de una transacción.
Un error antes de confirmarla revierte esos cambios. Conserva los permisos SQL
explícitos de los cuatro procedimientos de timbres que se recrean. Registra la marca
`Facturacion.EstructuraCuenta.v1` en las propiedades extendidas de la base, sin agregar
una tabla. Esa marca permite repetir el archivo sin volver a fusionar clientes ni
sumar timbres. Si el esquema de las tablas revisadas cambió después, puede detenerse.

Los índices de texto completo se crean después de confirmar las tablas: SQL Server
no permite crearlos dentro de esa transacción
([documentación de Microsoft](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-fulltext-index-transact-sql)).
Si esta parte falla, la conversión anterior ya queda guardada. Corregir el motivo
indicado y ejecutar otra vez el archivo completo para terminar los índices pendientes.
El bloqueo de actualización se conserva hasta terminar ambas partes. La indexación
de los registros existentes continúa en segundo plano y puede tardar.

También puede ejecutarse sobre `Facturacion1.1` si ya recibió la versión anterior de
este archivo: conserva las conversiones y completa las columnas e índices que faltan.
No hace falta recrear la base ni borrar datos.

No registra migraciones ficticias. Para futuras actualizaciones se deberá seguir
comparando la estructura real; no ejecutar `dotnet ef database update` suponiendo
que este archivo llenó su historial. No copia el historial, usuarios, permisos de
usuarios SQL ni los diagramas de SSMS de la principal. Los nombres automáticos de
restricciones pueden variar sin cambiar su función.

Si una actualización ya terminada necesita deshacerse, restaurar tanto el respaldo
de la base como la publicación anterior. Copiar únicamente las DLL anteriores no
revierte la conversión de datos.

El 31 de octubre se comprobó la sintaxis de SQL Server 2014 en 100 bloques, incluido
el SQL dinámico. Se crearon bases temporales ejecutando los objetos de `script.sql`,
sin reutilizar su nombre, sus rutas físicas ni su usuario de IIS. Se verificó la
actualización con datos ficticios de dos cuentas y tres empresas, la separación de
cuentas, los saldos, las referencias del comprobante, los permisos y la conservación
de su receptor histórico. También se probó la reversión ante un fallo, la repetición
del archivo, los permisos vacíos y el rechazo de un esquema con una columna ajena.
Funcionó tanto sin tabla de historial como con esa tabla vacía; no escribió en ella.
Las bases temporales se eliminaron al terminar.

El 2 de noviembre se redujeron las comprobaciones a las tablas modificadas, conservando
las ocho etapas de conversión. El archivo pasó de 221,968 a 73,710 bytes, un 67 % menos.
Después se quitaron los comentarios, las líneas vacías y los mensajes informativos:
esa versión quedó en 70,543 bytes y 1,112 líneas. Conserva los errores y sus comprobaciones.
Se repitieron las pruebas con datos y con permisos vacíos en bases temporales creadas
desde `script.sql`. En ambos casos el resultado también pasó la comprobación completa
del esquema final. Se comprobaron de nuevo la reversión, la repetición, los saldos,
las referencias fiscales, los permisos SQL y el rechazo de una columna ajena en clientes.

La comparación directa de `Facturacion1.1` y `FacturacionDev` detectó después los tres
índices de texto completo y las nueve columnas históricas que faltaban en la referencia.
Se agregaron al archivo, que queda en 83,869 bytes y 1,254 líneas, sin comentarios ni
mensajes de avance. Se revisaron 124 bloques de sintaxis SQL 2014. Las pruebas se
repitieron con datos y con permisos vacíos; el resultado pasó la comprobación completa
del esquema de aplicación, incluidas las nueve columnas adicionales. Las búsquedas
`CONTAINS` funcionan en los tres catálogos. Se comprobó además la recuperación de una
interrupción entre las tablas y los índices y la ejecución sobre una base que ya tenía
la conversión por cuentas, sin repetir fusiones ni acreditar timbres otra vez.

La prueba funcional fue en SQL Server 17 con compatibilidad 120. Esto no equivale a
ejecutar el motor SQL Server 2014. La prueba definitiva debe hacerse en una copia de
la base del ingeniero en su servidor.
