# Manual de entrega del sistema de facturación

GRUPO MARPA IT · Área de Sistemas · 19 de noviembre de 2026

Este manual reúne la operación, la instalación y la continuidad técnica del sistema. Los campos, las relaciones y los tipos de datos se consultan en el [diccionario de la base](Base-de-datos.md). Juntos forman la documentación técnica de entrega.

- [Qué incluye la entrega](#alcance)
- [Operación de la plataforma](#operacion)
- [Dónde se procesan y guardan los datos](#arquitectura)
- [Instalación y actualización en IIS](#iis)
- [Diagnóstico y comprobaciones](#verificacion)
- [Integración futura del PAC y la pasarela](#integracion)

<a id="alcance"></a>

## Qué incluye la entrega

La plataforma aplica CFDI 4.0 y permite trabajar desde el navegador con varias empresas emisoras por cuenta. Incluye identidad, catálogos, series y folios, bolsa de timbres, compras administradas por el operador y gestión documental. Las variantes abarcan factura estándar, Constructoras, Notaría, Comercio Exterior 2.0, Carta Porte 3.1 para autotransporte nacional propio y complemento de pagos 2.0.

El paquete de revisión permite preparar y verificar documentos, consultar sus salidas y probar el envío por correo. La aceptación fiscal con un PAC real y el cobro automático con pasarela quedan a cargo de la empresa. La bolsa de timbres utiliza la acreditación administrativa mientras se integra la pasarela.

<a id="operacion"></a>

## Operación de la plataforma

### Configurar una empresa y administrar accesos

1. Entra como titular y elige la empresa emisora en la barra superior. Completa sus datos fiscales, series, monedas e impuestos. El uso productivo del CSD requiere un entorno fiscal autorizado.
2. Da de alta a los usuarios internos, elige sus empresas y asigna permisos por operación. La edición reúne nombre, contraseña cuando corresponda, y permisos. Guarda los cambios y comprueba el acceso con el usuario correspondiente.
3. Configura el SMTP de la empresa y prueba el envío de un documento a un destinatario autorizado. El SMTP general del SaaS, administrado desde el panel del operador, atiende los mensajes del sistema, incluida la recuperación de acceso.

El panel de operador se abre en `/operador`. Su identidad y sus permisos se gestionan aparte de los usuarios de facturación. Los permisos se asignan por acción. La lista de operadores muestra un resumen desplegable y el historial usa nombres de actividades legibles. **Limpiar sesiones**, cuando el operador tiene autorización, borra los registros normales de inicio, cierre y renovación de sesión. Se conservan las alertas y los registros administrativos o de facturación.

Si un cliente u operador olvida su contraseña, debe usar **Olvidé mi contraseña** en su pantalla de acceso y seguir el flujo que recibe por correo. Para comprobar la recuperación, termina el cambio e inicia sesión con la contraseña nueva.

### Capturar, consultar y enviar documentos

1. Confirma la empresa emisora y abre el tipo de documento correspondiente. Selecciona el receptor y los conceptos del catálogo. Las claves del SAT se seleccionan mediante búsqueda.
2. Revisa las cantidades, descuentos, impuestos y totales. Guarda el borrador para conservar la captura y continuar después.
3. En **Documentos**, abre el registro con la acción disponible en su fila o tarjeta. Revisa el estado antes de descargar o enviar. El borrador sirve para revisión y su PDF no tiene validez fiscal. El XML timbrado requiere una certificación real.
4. Para enviar, usa la acción de correo disponible según el permiso y el estado del documento. El servidor usa el SMTP propio de la empresa emisora. Si ese envío falla, informa el error para revisar esa configuración.

El complemento de pagos relaciona los documentos cubiertos por el abono. Revisa la parcialidad, el saldo anterior, el importe pagado y el saldo insoluto. Constructoras conserva el desglose de estimación y amortización. Notaría, Comercio Exterior y Carta Porte guardan sus datos particulares con el documento.

### Administrar timbres y compras

El servidor calcula el precio a partir del paquete elegido. Una compra pendiente se revisa desde el panel del operador. **Acreditar** agrega el saldo, y **Preparar factura** abre un flujo distinto para la factura que emite el proveedor de los timbres. La acreditación se registra una sola vez por compra.

**Retirar** permite volver a publicar un paquete. **Eliminar** lo saca del catálogo general o exclusivo, con el permiso correspondiente. La compra anterior conserva su referencia y los importes que se guardaron al adquirir el paquete. La vigencia se registra en la compra. El sistema actual no caduca saldo por lotes.

<a id="arquitectura"></a>

## Dónde se procesan y guardan los datos

| Proyecto o carpeta | Responsabilidad |
| --- | --- |
| `Facturacion.Client` | Interfaz Blazor WebAssembly, PWA y servicios HTTP. |
| `Facturacion.Server` | API ASP.NET Core, permisos, cálculos, persistencia, correo y publicación del cliente. |
| `Facturacion.Shared` | DTO y contratos entre módulos. |
| `Facturacion.Server/Data` y `Facturacion.Server/Migrations` | Entidades, configuración EF Core y evolución del esquema. |
| `Facturacion.Server/Modules/Documentos` | Emisión, timbrado, cancelación, complementos y salidas. |
| `Facturacion.Server/Modules/Plataforma` y `Modules/Operador` | Identidad, datos de la cuenta y administración del proveedor. |
| `tests/Facturacion.Pruebas` | Pruebas de reglas, autorización y operaciones con SQL. |

La empresa activa y la cuenta se obtienen de la sesión. El servidor comprueba acceso y permisos con sus políticas y filtros EF Core. Los catálogos de clientes y productos y la bolsa de timbres pertenecen a la cuenta. Comprobantes, series, configuración y datos de los complementos pertenecen al emisor. Los movimientos y compras guardan también la empresa que los originó.

Al guardar un borrador, la API valida los datos y recalcula los importes con tipos decimales. La emisión conserva copias fiscales del emisor, receptor y conceptos. Cuando se inicia el timbrado, SQL reserva el folio y el saldo conforme a las reglas de concurrencia. El servicio llama al PAC fuera de la transacción SQL, registra el intento y utiliza una clave de idempotencia para identificar los reintentos.

SQL conserva datos e importes y referencias a archivos. El XML final del comprobante se resguarda en el almacén privado autorizado, y los intentos conservan el XML enviado según su tabla. El PDF se genera desde los datos guardados. La descarga comprueba permisos y ámbito antes de servir el archivo. El llavero y los secretos de instalación se respaldan junto con los datos, según las rutas de cada sitio.

Para continuar una corrección, sigue la acción desde la página y el servicio HTTP hasta el endpoint, la política y el servicio de negocio. Revisa la entidad y la migración si toca datos persistidos. Comprueba el flujo con una cuenta válida y otra fuera de su ámbito cuando el cambio afecte aislamiento.

<a id="iis"></a>

## Instalación y actualización en IIS

### Preparar una instalación

La publicación autocontenida para Windows x64 incluye el runtime. IIS necesita **AspNetCoreModuleV2** del Hosting Bundle. Configura el grupo, binding HTTPS, certificado, resolución del nombre y `AllowedHosts`. La raíz física apunta a la publicación completa, que contiene el ejecutable, sus dependencias, catálogos SAT y `wwwroot`.

Para generar la publicación desde el código, usa el SDK .NET 10 del proyecto y ejecuta desde el repositorio:

```powershell
.\herramientas\entrega-iis\Publicar-Compatible.ps1 -Salida 'C:\Entregas\Facturacion' -NombreHost 'facturacion.ejemplo.com'
```

Una base vacía requiere el esquema de instalación. Una existente requiere el SQL correspondiente a su punto de partida. El asistente prueba la conexión, protege la configuración y permite crear al operador principal cuando corresponde. El administrador debe aplicar el esquema antes de finalizar. Para una base creada por script, revisa primero su historial antes de considerar una migración directa con EF.

La identidad de IIS necesita lectura y ejecución sobre la publicación, y modificación en la carpeta privada correspondiente, normalmente `cnf`, y el almacén configurado. La cuenta SQL de ejecución necesita los permisos de datos y ejecución de procedimientos utilizados por la aplicación. La actualización de esquema requiere los permisos SQL de mantenimiento. El SQL acumulado se comprobó en un motor posterior con compatibilidad 120 y en una base de ensayo del esquema anterior. La revisión del servidor receptor con SQL Server 2014 debe distinguir el motor instalado, Full-Text y la compatibilidad de cada operación.

### Actualizar un sitio configurado

1. Respalda base, publicación, configuración privada, certificados y llaveros. Confirma la base de destino y el script incluido con la versión.
2. Para actualizar por SSMS, detén el grupo del sitio, aplica el SQL a esa base y revisa el resultado. Reemplaza la publicación y conserva el `web.config`, la identidad del grupo y la configuración privada. En el paquete autocontenido, IIS inicia `Facturacion.Server.exe` en modo `outofprocess`.
3. Inicia el sitio y comprueba acceso, catálogos, listado de paquetes, reapertura de un borrador y consulta de sus archivos.

En un sitio de revisión con acceso temporal habilitado, también se puede usar **Mi perfil → Actualizar base de datos**. La ruta directa es `/api/mantenimiento-base/interfaz`. El archivo privado `appsettings.Mantenimiento.json` se lee al iniciar. Si se instala o cambia con la aplicación en marcha, recicla el grupo una vez antes de iniciar la actualización. Después verifica el destino y carga el SQL original de esa compilación, cuya huella se valida. Mantén el grupo en marcha mientras la operación se ejecuta y espera el estado final y la verificación de catálogos.

El SQL de la entrega del 19 de noviembre agrega `Paquetes.Eliminado` al esquema previo. Una publicación de esa fecha que consulta una base anterior puede producir un error 500 en el listado. El registro de servidor permite confirmar la columna faltante. Tras aplicar el SQL, revisa que los paquetes disponibles y las compras anteriores sigan consultables.

<a id="verificacion"></a>

## Diagnóstico y comprobaciones

| Síntoma | Primera comprobación |
| --- | --- |
| Error 500 en Paquetes | Revisa que la publicación y el esquema correspondan y confirma el diagnóstico en el registro del servidor. |
| Actualizar base de datos no aparece | Comprueba entorno, correo autorizado, vigencia y ubicación del archivo privado. |
| El script aparece como no registrado | Usa el archivo exacto del paquete correspondiente. La revisión comprueba su huella. |
| Inicio detenido o catálogos pendientes | Revisa los recursos publicados, la versión, Full-Text y el progreso de la preparación SAT. |
| PDF en blanco en móvil | Prueba la acción de abrir de cada fila y registra navegador, dispositivo y estado del documento. |
| Correo o recuperación falla | El envío de documentos usa SMTP del emisor. La recuperación usa SMTP del SaaS. Comprueba el flujo correspondiente. |

Antes de liberar una versión, registra compilación, base de prueba y resultado. La regresión integrada documentada reportó 319 aprobadas, 13 omitidas y ninguna fallida, de 332 descubiertas. En una ejecución específica posterior pasaron ocho pruebas de permisos y timbres. En pruebas locales se verificó que eliminar un paquete conserva su compra, que la actualización SQL se puede ejecutar de nuevo sobre un esquema reconocido y que el paquete publicado arranca. El residente confirmó manualmente el envío de un borrador y la recuperación de contraseña.

Para repetir pruebas desde el repositorio, utiliza una base de desarrollo o prueba. Algunos casos crean o eliminan su base de ensayo.

```powershell
dotnet build Facturacion.sln
dotnet test tests/Facturacion.Pruebas/Facturacion.Pruebas.csproj
```

<a id="integracion"></a>

## Integración futura del PAC y la pasarela

La empresa contrata al PAC, resguarda sus credenciales, prepara un emisor y coordina la certificación. El sistema deja la interfaz `IProveedorPac`, el registro de intentos y los flujos de timbrado, consulta, cancelación y conciliación. El adaptador existente deberá validarse con el proveedor elegido. En caso de respuesta incierta, se debe consultar y conciliar el intento usando la misma clave antes de decidir si procede volver a enviar.

Para la pasarela, la empresa selecciona y configura el proveedor bancario. La integración debe relacionar el pago confirmado con una orden generada en el servidor y validar autenticidad, importe, moneda e identificador antes de acreditar. La prueba de aceptación debe incluir confirmaciones duplicadas, pagos fallidos, importes distintos y conciliación. Hasta habilitarla, el operador conserva el procedimiento de acreditación administrativa.
