# Facturación de paquetes de timbres

## Fase 1: emisor del proveedor

En **Operador → Configuración → Facturación** se guardan RFC, razón social, régimen fiscal,
código postal de expedición, huso horario y serie para las ventas de paquetes. Son datos
globales del proveedor; no cambian las empresas emisoras de los clientes.

Consultar requiere **Ver configuración**. Guardar datos o cargar el CSD requiere
**Editar configuración** y confirmar la contraseña del operador. Las claves del SAT,
RFC y compatibilidad de régimen se validan en el servidor. La razón social se normaliza
al guardar y la pantalla informa si se ajustó.

El CSD se carga con sus archivos `.cer` y `.key` y la contraseña de la llave. Se comprueban
RFC, vigencia y correspondencia entre certificado y llave. Cada archivo admite 64 KB.
Los archivos y contraseña se guardan cifrados mediante Data Protection en
`CertificadosProveedor`, con propósitos propios del proveedor. La respuesta solo contiene
número de serie, vigencia y estado. Los CSD anteriores se conservan inactivos. Después de
cargar un certificado, el RFC queda fijo. Los cambios y cargas quedan en la bitácora sin
archivos ni contraseñas. Una pestaña desactualizada debe recargarse antes de guardar.

La configuración y el llavero del servidor deben respaldarse junto con la base: copiar
solo SQL a otro servidor no permite recuperar los certificados cifrados.

## Actualizar la base

La migración `20261103173722_20261103_A_FiscalDelProveedor` agrega
`ConfiguracionFiscalProveedor` y `CertificadosProveedor`. No modifica las tablas de
compras, saldos ni comprobantes. No contiene datos fiscales ni certificados precargados.

Para una base local administrada con EF, desde `Facturacion.Server`:

```powershell
dotnet ef database update --project .\Facturacion.Server.csproj --startup-project .\Facturacion.Server.csproj
```

Para la base del ingeniero creada con el script de SQL Server, usar
`sql/Facturacion.FiscalProveedor-Sql2014.sql` en la base correcta después del respaldo.
Este archivo agrega únicamente las dos tablas nuevas; supone que ya se aplicó la
actualización previa de cuentas y monedas. No modifica `__EFMigrationsHistory`.
Si las tablas ya existen, se detiene sin reemplazarlas. No ejecutar ambos métodos
sobre la misma base. El script no necesita credenciales del PAC.

La verificación local cubrió 42 casos de validación, guardado, concurrencia, cifrado,
reemplazo de CSD y autorización HTTP, en una base temporal que se eliminó al terminar.
El script creó las mismas columnas que el modelo EF y se revisó con la gramática de
SQL Server 2014. La ejecución se comprobó en SQL local con compatibilidad 120;
queda comprobarla en la instancia SQL Server 2014 del servidor.

## Fase 2: borrador de la factura de compra

En **Operador → Compras → Pagadas**, **Preparar factura** abre un formulario sobre el
listado. La compra debe tener pago acreditado. Abrir el formulario no guarda registros;
**Guardar borrador** crea una sola factura vinculada a esa compra. Después se abre con
**Ver factura** y puede corregirse mientras siga en borrador.

El permiso **Preparar facturas**, dentro de Compras, autoriza guardar y modificar.
**Ver compras** permite consultar un borrador ya guardado. Acreditar compras no otorga
el permiso de facturación. La migración habilita la nueva acción al operador principal;
los demás operadores necesitan que se les asigne. Actualizar la sesión después de migrar.

El proveedor se toma de la configuración fiscal. El receptor es la empresa que originó
la compra. Se confirma su razón social, régimen, código postal fiscal y uso de CFDI;
el CP no se precarga desde expedición porque ambos datos pueden ser diferentes. También
se seleccionan las claves SAT del servicio y unidad, la descripción y la forma en que
se recibió el pago. El servidor valida vigencia y compatibilidad de las claves.

La factura representa un paquete, con los timbres indicados en su descripción. Los
importes se copian de la compra, con seis decimales y el IVA incluido: no se reciben
precios del navegador ni se vuelve a sumar el impuesto. Como la compra ya está pagada,
el método es PUE y se exige una forma distinta de 99 o 30. Referencias del SAT:
[forma y método de pago](https://www.sat.gob.mx/minisitio/Factura/documentos/infografias/formaspagometodopagos.pdf),
[datos fiscales del receptor](https://www.sat.gob.mx/minisitio/Factura/documentos/Infografia_FacturaElectronica_Datos_Receptor.pdf).

Emisor, receptor e importes quedan copiados en `FacturasDeCompras`; cambios posteriores
en catálogos o configuración no reescriben lo guardado. La cuenta se obtiene desde la
compra en SQL. La tabla tiene filtro global por cuenta y sus escrituras conservan el
sellado de tenencia. El operador utiliza un contexto de la cuenta resuelta por el
servidor. El índice único evita duplicar una factura para la misma compra; la versión
de fila rechaza ediciones simultáneas. Cada cambio queda en bitácora.

La migración `20261103180949_20261103_A_FacturasDeCompras` agrega esa tabla, índices y
el permiso del principal, sin cambiar compras, saldos ni comprobantes existentes.
Para EF, desde `Facturacion.Server`:

```powershell
dotnet ef database update --configuration Release --project .\Facturacion.Server.csproj --startup-project .\Facturacion.Server.csproj
```

Para una base administrada mediante scripts SQL Server 2014, aplicar primero la
actualización de cuentas y monedas, después `sql/Facturacion.FiscalProveedor-Sql2014.sql`
y finalmente `sql/Facturacion.FacturasDeCompras-Sql2014.sql`. No ejecutar ambos métodos
de actualización en la misma base.

Se verificaron 40 casos en una base temporal: validaciones, guardado y reapertura,
datos históricos, ediciones concurrentes, filtro y sellado por cuenta, permisos HTTP,
importes manipulados, listado de compras y equivalencia del script con el modelo EF.
La base temporal se eliminó al terminar; estas pruebas no actualizan la base principal.
El script se revisó con la gramática SQL Server 2014 y se ejecutó en SQL local con
compatibilidad 120. Falta comprobarlo en la instancia 2014 del servidor.

## Fase 3: emisión simulada desde el operador

En **Compras → Pagadas → Ver factura**, guardar los datos antes de usar
**Timbrar simulación**. Se exige el nuevo permiso **Timbrar facturas de compras**,
además de **Ver compras**. Preparar facturas o acreditar una compra no concede timbrado.
La actualización habilita la acción al principal; los demás operadores deben recibirla
expresamente. Actualizar la sesión después de aplicar la migración.

Esta fase solo funciona en `Development` con `Pac:Modo=Simulado`. El proveedor necesita
su CSD vigente, cargado en Configuración. Se descifra el certificado reservado al intento;
no se utiliza el doble de CSD ni las series de una empresa cliente. No se configura ni se
invoca un PAC real. La pantalla identifica el resultado como timbrado simulado.

El contrato `IEmisionDeFacturasDeCompras` separa la persistencia de Plataforma del XML
y el PAC de Documentos. Fue autorizado y agregado con su documentación en el commit
dedicado `37b7bf2`. El firmado y la serialización comparten las mismas funciones que
los comprobantes de empresa. Se usa el XSLT oficial para la cadena original y el XSD
CFDI 4.0 para validar el XML. El concepto representa un paquete y conserva el total
con IVA incluido; el desglose se valida desde ese total para evitar diferencias a seis
decimales. No se recibe un precio desde el navegador.

`SeriesFacturasProveedor` numera por RFC y serie del proveedor. El procedimiento
`ReservarFolioProveedor` usa `UPDLOCK` y una transacción corta. `IntentosFacturasProveedor`
conserva certificado, clave de solicitud, XML firmado y resultado. Los intentos pertenecen
a la cuenta compradora y tienen filtro global y sellado; las escrituras resuelven esa
cuenta desde SQL. Cada preparación, acceso al CSD y resolución deja bitácora sin secretos.

El encabezado `Idempotency-Key` es obligatorio. Repetir compra, versión y clave devuelve
el mismo identificador de intento mediante una respuesta 202 estable. Una clave usada
para otra compra o versión se rechaza. Los registros se conservan, sin purga a las 24
horas. La consulta GET de la factura devuelve su estado fiscal actual. Un segundo intento
no puede iniciarse mientras el primero siga en `timbrando`.

Se persiste el XML exacto antes de llamar al simulador, fuera de la transacción SQL.
Si se pierde la respuesta, la conciliación revisa cada cinco minutos los intentos de
más de treinta minutos. Si el simulador ya lo aceptó, recupera el mismo timbre. Si no
recibió ese XML, envía el mismo con la misma clave. Si el corte ocurrió antes de guardar
el XML, registra error sin enviarlo. Al reclamar un intento cambia su control, por lo
que una ejecución anterior no puede sobrescribirlo. Un fallo definitivo conserva el
folio; un reintento no lo devuelve al contador. El folio solo se muestra al emitir.

Un borrador o una factura con fallo confirmado puede corregirse; una factura emitida
o pendiente de consulta no se edita. **Actualizar estado** permite consultar el resultado.
Timbrar esta venta no descuenta ni reserva timbres de la bolsa del comprador.

La migración `20261103185621_20261103_A_TimbradoFacturasProveedor` agrega las dos tablas,
el procedimiento, tres columnas de resultado en `FacturasDeCompras` y el permiso del
principal. No cambia los comprobantes de empresas ni los saldos.

Para una base administrada con EF, desde `Facturacion.Server`:

```powershell
dotnet ef database update --configuration Release --project .\Facturacion.Server.csproj --startup-project .\Facturacion.Server.csproj
```

Para la base del ingeniero administrada con scripts, aplicar
`sql/Facturacion.TimbradoProveedor-Sql2014.sql` después de los dos scripts de proveedor
anteriores y con respaldo previo. No combinar ese método con EF en la misma base.
El script no altera el historial de migraciones ni elimina datos existentes. Si detecta
la actualización ya instalada o parcial, se detiene para revisión.

La revisión ejecutó 43 comprobaciones en una base temporal: firma verificable con el
CSD del proveedor, esquema oficial, IVA incluido, solicitudes y folios concurrentes,
inmutabilidad, aislamiento por cuenta, ausencia de cargos al saldo, permisos HTTP,
respuesta idempotente, cortes antes y después del envío, recuperación de intentos y
equivalencia entre el script y las columnas EF. La base temporal se eliminó al terminar.
El archivo pasó el analizador de sintaxis SQL Server 2014, incluidos sus comandos
dinámicos. Se ejecutó en SQL local; falta validarlo en el motor SQL Server 2014 del ingeniero.

También se agregó `ReservaDeFoliosProveedorPruebas` al proyecto de pruebas. Crea una
base temporal mediante todas las migraciones, establece compatibilidad 120 y verifica
25 reservas simultáneas, la continuidad del contador y la separación por RFC y serie.
La base se elimina al finalizar. Se ejecuta con:

```powershell
dotnet test .\tests\Facturacion.Pruebas\Facturacion.Pruebas.csproj --configuration Release --filter FullyQualifiedName~ReservaDeFoliosProveedorPruebas
```

## Fase 4: descarga y vista previa de la factura

En **Operador → Compras → Ver factura**, una factura emitida muestra **Vista previa**,
**Descargar PDF** y **Descargar XML**. La consulta exige **Ver compras**. Preparar,
timbrar y consultar siguen siendo acciones separadas.

En el historial de **Timbres** del cliente aparece el estado de la factura. Al quedar
emitida, muestra **Ver factura**, **PDF** y **XML**. Pulsar esa compra abre la vista
previa fiscal. El menú **Compra** conserva la vista y descarga del comprobante comercial.
Antes de emitir, se conserva únicamente el comprobante de compra pagada. La emisión
continúa a cargo del operador; el cliente no modifica sus datos ni timbra esa venta.

Las rutas del cliente son `/api/timbres/compras/{compraId}/factura/pdf` y `/xml` y
exigen **Comprar timbres**, igual que el historial. Las del operador viven bajo
`/api/operador/compras/{compraId}/factura/pdf` y `/xml`. Cada identidad usa su cliente
HTTP autenticado; no se abre una URL directa que pierda el token en memoria.
Los archivos se entregan con `Cache-Control: no-store`, no desde `wwwroot`.

La consulta `ObtenerEmitidaAsync` se autorizó y agregó con su documentación en el
commit dedicado `5d80c34`. Para el cliente conserva los filtros globales de cuenta
tanto en la factura como en sus intentos. Una empresa puede consultar las compras
de su misma cuenta. Una compra ajena y una inexistente responden como no encontradas;
sin factura emitida no se ofrece un archivo fiscal. Para el operador se comprueba su
identidad activa antes de consultar otras cuentas. No se descifran CSD ni contraseñas.

El XML se descarga exactamente como se guardó al resolver el intento. El PDF reutiliza
el generador de comprobantes y lee exclusivamente ese XML: emisor, receptor, concepto,
IVA, total, folio, UUID y sellos. La consulta solo aporta los datos congelados de fecha,
huso horario y cadena original SAT. No usa nombres, precios, certificados ni logotipos
de la empresa activa. Cambiar los catálogos o el proveedor después no altera esos datos.
El lector bloquea DTD y limita el tamaño del documento. La simulación conserva su leyenda
visible en el PDF. Emisión y certificación se muestran en el huso horario correspondiente.

Se ejecutaron 68 comprobaciones de integración en una base temporal, incluidos los
casos de emisión de la fase anterior. Las pruebas nuevas cubrieron descarga HTTP,
permisos, cuentas ajenas, empresas hermanas, XML exacto, estados del historial,
datos históricos, generación PDF y bloqueo de XML inválido. La base temporal se eliminó.
También se renderizó una factura de prueba para revisar su distribución y legibilidad.
Las diez pruebas existentes de representación impresa pasaron después de corregir la
conversión de la fecha de certificación al huso horario del PDF.

Esta fase no cambia tablas ni requiere una migración adicional. Si ya se aplicaron
las tres fases anteriores, basta compilar, reiniciar el proyecto y volver a abrir la
pantalla para ver las nuevas acciones.

## Claves SAT configuradas en el paquete

En **Operador → Paquetes → Crear o editar** se seleccionan **Producto o servicio SAT**
y **Unidad SAT**. Los mismos campos aparecen al crear o editar un paquete exclusivo
desde la cuenta. Ambos buscan en el servidor por clave o descripción y resuelven las
claves guardadas para mostrar su descripción. Los paquetes nuevos proponen
`81112106` (Proveedores de servicios de aplicación) y `E48` (Unidad de servicio).
El operador puede elegir otras claves vigentes cuando corresponda.

Si se omite una clave al crear o editar un paquete, el servidor asigna su valor
predeterminado y comprueba la vigencia de ambas en el catálogo. Consultar
estos dos catálogos requiere **Ver paquetes** o **Ver cuentas**, según la pantalla de
origen, sin depender del permiso de consulta de compras.

Las compras nuevas copian ambas claves del paquete en `ComprasTimbres`. Al preparar
su factura se propone esa copia, aunque después cambie el paquete. Para compras que
no tienen claves copiadas, se propone la configuración actual del paquete, comprobando
que sea general o pertenezca a la misma cuenta; si falta configuración, se usan los
valores predeterminados. Una factura ya guardada conserva sus
propias claves y no se actualiza desde el paquete.

La migración `20261103195727_20261103_A_ClavesSatDePaquetes` agrega cuatro columnas
opcionales: `Paquetes.ClaveProdServ`, `Paquetes.ClaveUnidad`,
`ComprasTimbres.ClaveProdServAlComprar` y `ComprasTimbres.ClaveUnidadAlComprar`.
No modifica precios, saldos, compras anteriores ni facturas. Para una base administrada
mediante scripts se incluye `sql/Facturacion.ClavesSatPaquetes-Sql2014.sql`, que no
modifica el historial de EF. Usar solo uno de los dos métodos de actualización.

Se verificaron 29 casos en una base temporal: validación de claves, configuración de
paquetes generales y exclusivos, compra y copia histórica, precarga de compras antiguas,
conservación de facturas guardadas, aislamiento entre cuentas, permisos del selector,
bitácora y aplicación del script sin perder compras. La base de prueba se eliminó.
El script pasó la revisión de sintaxis SQL Server 2014; su ejecución se comprobó en SQL
local y queda comprobarla en la instancia 2014 del ingeniero.

La migración `20261103212648_20261103_A_ClavesSatPredeterminadas` completa solo las
claves vacías de los paquetes actuales, generales y exclusivos. Conserva las claves
elegidas, registra los cambios en la bitácora y agrega valores predeterminados en SQL.
No modifica compras anteriores, facturas guardadas, precios ni saldos. Para la base
administrada con scripts se aplica `sql/Facturacion.ClavesSatPredeterminadas-Sql2014.sql`
después del script que agrega las columnas. Repetirlo no duplica los registros de
bitácora ni cambia los paquetes que ya tienen sus claves.

Se ejecutaron 37 comprobaciones en una base temporal: valores predeterminados en
paquetes generales y exclusivos, validación SAT, permisos, copias históricas,
aislamiento, migración con datos y ejecución repetida del script. Se conservaron las
claves personalizadas y las facturas guardadas. La base temporal se eliminó al terminar.
El script pasó el analizador de sintaxis SQL Server 2014 y se ejecutó en SQL local;
queda comprobarlo en la instancia 2014 del ingeniero.

## Pendientes

En la factura de compra se excluyen los usos `CP01` y `CN01`, correspondientes a pagos
y nómina. El servidor rechaza esas claves y los valores de longitud incorrecta antes
de guardar, aunque se envíen directamente a la API. Este ajuste no modifica el esquema.

La integración real del PAC, la cancelación de estas facturas y la pasarela permanecen
fuera de esta fase. Los comprobantes emitidos con el simulador conservan su identificación
de prueba al consultarse o descargarse.
