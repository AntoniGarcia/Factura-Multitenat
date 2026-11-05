# Entrega de revisión para IIS

Esta entrega se limita a las funciones acordadas del inventario anterior y las funciones del SaaS solicitadas. Se retiraron del alta de documentos las notas de crédito y de los formularios la captura de datos fiscales adicionales por concepto y servicios parciales de vivienda. Se conserva la lectura de datos ya guardados; no se borraron documentos ni columnas.

## Revisión de borradores

`RevisionDocumentos:Habilitada` permite PDF, XML y correo de documentos en borrador de tipo ingreso, traslado y pago. Solo puede habilitarse en Staging o Development. El XML pasa por los generadores, cálculos y XSD existentes, pero utiliza un certificado sintético temporal. Se identifica en el archivo como revisión sin timbre ni validez fiscal. El PDF conserva la marca de borrador y el correo añade `[BORRADOR]` al asunto.

No se registra un doble de los contratos de plataforma, no se contacta al PAC, no se asigna UUID, no se reserva folio ni se consume la bolsa de timbres. La ruta de emisión real conserva el proveedor de CSD existente. Los permisos y filtros de empresa siguen aplicándose.

El complemento de pagos conserva su regla de origen: requiere una factura PPD emitida. En las pruebas se usó una referencia histórica ficticia exclusivamente dentro de la base temporal; el paquete no inserta esa referencia en el servidor del ingeniero.

`appsettings.Staging.json` habilita la revisión al actualizar un sitio que ya usa Staging. Por eso no hace falta reemplazar su `web.config` ni su carpeta `cnf`. El web.config incluido es una plantilla para instalaciones nuevas; el hostname existente del servidor se conserva.

## Único script

`docs/sql/Facturacion.Actualizar-Completo-Sql2014.sql` contiene todos los cambios desde el esquema original proporcionado en `C:/Users/Luis/Documents/script.sql`, sin exigir scripts intermedios. Se entrega como `Actualizar-base.sql` junto al sitio, fuera del directorio público.

Se probó la actualización desde ese script sin historial de EF, con compatibilidad SQL 120, se ejecutó dos veces y se verificó la conservación de clientes, productos, saldos, documentos históricos, parámetros SMTP y permisos SQL explícitos. La comparación con FacturacionDev coincide en los objetos de aplicación; no se copia el historial de EF ni los objetos de diagramas de SSMS. El analizador de SQL Server 2014 validó 140 bloques, incluidos los EXEC dinámicos. No se afirma haber ejecutado el paquete en un motor SQL Server 2014 nativo.

Las columnas opcionales de las funciones retiradas se conservan por compatibilidad con la base actual; no obligan a capturar esos campos ni habilitan sus controles.

## Comprobación

La revisión de borradores en Staging y sobre la base actualizada desde el esquema original completó 304 comprobaciones, incluidos seis envíos reales con PDF/XML a la dirección de prueba autorizada. Se comprobó el asunto de borrador, sus datos y el historial de envío. Los seis documentos permanecieron sin timbre y no consumieron saldo.

La regresión terminó con 318 pruebas aprobadas, dos omitidas y ninguna fallida. La publicación incluye catálogos, XSD y los XSLT de complementos. La carpeta privada de las pruebas está fuera del publicado y no forma parte del ZIP.

Las instrucciones para el ingeniero están en `LEEME.txt`, dentro del paquete `outputs/Facturacion-Revision-IIS-20261105`. La carpeta `sitio` contiene la publicación; el script SQL se ejecuta desde SSMS sobre la base existente.

## Prueba del sitio publicado

El proceso de la publicación Release se ejecutó en Staging contra una base temporal creada desde el esquema original y actualizada con el único script. Completó 273 comprobaciones: estado de integración sin PAC, seis PDF y XML de borrador, controles de acceso, HTML de arranque y entrega de WebAssembly comprimido con Brotli. Se esperó a que terminara la preparación automática de catálogos antes de consultar los documentos. El proceso se detuvo y la base temporal se eliminó al finalizar.

El asistente también abrió con una carpeta privada vacía, sin conexión precargada ni clave de instalación. Esto comprueba el proceso publicado en el equipo local; la configuración particular del IIS del ingeniero debe verificarse al copiar los archivos.

La compilación local terminó sin advertencias ni errores. Los seis nuevos correos de borrador fueron aceptados por SMTP; la confirmación del usuario registrada en la verificación anterior corresponde a la primera serie de seis correos.
