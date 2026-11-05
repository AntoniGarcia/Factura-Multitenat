# Datos fiscales por concepto y servicios parciales de construcción

Actualización: 5 de noviembre de 2026.

## Cambios

- Cada concepto permite capturar pedimentos, cuentas prediales y datos fiscales de un tercero. Se conservan al guardar, reabrir y modificar; aparecen en XML y PDF.
- Los pedimentos respetan las separaciones del formato SAT. Se rechazan duplicados, cuentas prediales con caracteres no admitidos y terceros con datos fiscales incompletos. El tercero no puede ser el emisor ni el receptor.
- Obra incorpora opcionalmente el complemento de servicios parciales de construcción de casa habitación: permiso, licencia o autorización y domicilio del inmueble. Cambiar únicamente estos datos también actualiza el estado del formulario.
- La generación valida el complemento contra su XSD local. No determina por sí misma si el contribuyente cumple los requisitos del estímulo; tampoco convierte automáticamente el tratamiento de IVA del producto.
- Los PDF emitidos recuperan los datos del inmueble del XML guardado. Los borradores usan la captura actual.
- La publicación incluye también los XSLT de complementos que se encuentran en `CatalogosSAT/xslt`.

## Base de datos

Se agregaron dos columnas opcionales, sin eliminar datos:

- `Conceptos.DatosFiscalesJson`, mediante `20261105125228_20261105_B_DatosFiscalesConceptos`.
- `DatosObra.ServicioParcialJson`, mediante `20261105131603_20261105_B_ServiciosParcialesConstruccion`.

Ambas son `nvarchar(max)` y el servidor serializa su contenido. No se utilizan funciones JSON de SQL Server. Las migraciones ya se aplicaron a la base local `FacturacionDev`.

El incremental `sql/Facturacion.DatosFiscalesConceptosYVivienda.Sql2014.sql` agrega las mismas columnas. Se probó dos veces con compatibilidad 120, tanto con historial de EF como sin él. Este archivo no sustituye el script acumulado de entrega ni incluye los cambios de fases anteriores.

## Comprobación

El verificador temporal ejecutó 38 comprobaciones: guardado, reapertura, modificación del permiso, retiro del complemento, entradas inválidas, XML firmado validado contra XSD, generación del PDF, aislamiento entre empresas y repetición del incremental. La base temporal se eliminó al terminar. El PDF se renderizó y revisó visualmente.

Regresión general: 318 pruebas aprobadas, 2 omitidas y ninguna fallida. Las dos omitidas corresponden a la instalación del operador bajo una conexión de prueba explícita y a una previsualización manual de PDF.

El verificador de flujos completó otras 278 comprobaciones con dos cuentas y tres empresas: factura, Obra, Notaría, Comercio Exterior, pagos y Carta Porte nacional; PDF/XML; cancelación de la última parcialidad; bolsa de timbres; consultas y modificaciones HTTP sin permiso o desde otra empresa/cuenta; clientes y productos compartidos dentro de la cuenta; e inmutabilidad del XML después de modificar los catálogos. La consulta de destinatarios de correo se comprobó sin enviar mensajes. Se usó CSD sintético mediante el servicio real de certificados y únicamente el PAC fue sustituido por un doble en estos verificadores. Esto no cierra por sí solo todas las combinaciones de permisos ni la revisión del operador.

No se usaron credenciales de un PAC ni se probó aceptación fiscal real. Compatibilidad 120 en SQL local no equivale a una ejecución en SQL Server 2014 nativo. La revisión visual autenticada de los controles nuevos en navegador sigue pendiente.

## Sesiones

Se corrigió la renovación de sesión al cambiar de empresa: exige usuario activo y una familia de sesión vigente. También se hizo atómico el consumo del refresh token, tanto del cliente como del operador. Una reutilización concurrente revoca la familia; la prueba comprobó que solo una solicitud obtiene sucesor y que ninguno queda vigente después de detectar la reutilización.

## Recursos SAT

El complemento usa el [XSD oficial de servicios parciales de construcción](https://www.sat.gob.mx/sitio_internet/cfd/servicioparcialconstruccion/servicioparcialconstruccion.xsd), guardado en `CatalogosSAT/servicioparcialconstruccion.xsd`, y el XSLT de ese complemento ya incluido en la cadena original. Los artefactos del SAT se mantienen fuera del repositorio, como los demás esquemas del proyecto; deben acompañar la publicación.

## Continuación del cierre

Queda pendiente ampliar las notas de crédito a los demás tratamientos y monedas, terminar la revisión de permisos y flujos restantes, y consolidar el script SQL y el ZIP IIS con todas las fases. Carta Porte conserva exclusivamente el alcance de autotransporte nacional acordado. El PAC real y la pasarela de cobro continúan fuera de esta entrega de pruebas.
