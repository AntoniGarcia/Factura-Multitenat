# Datos opcionales SPEI en pagos

Fecha: 4 de noviembre de 2026.

## Captura y validación

La pestaña Bancos del complemento de pagos permite activar «Incluir datos SPEI»
cuando la forma de pago es `03`. Captura `TipoCadPago=01`, `CertPago`, `CadPago`
y `SelloPago`. Al desmarcar la opción o cambiar a otra forma de pago se retira
el grupo completo. Los pagos sin esta información siguen siendo válidos.

El servidor comprueba el grupo al guardar y al preparar el XML: solo forma `03`,
tipo `01`, los tres datos completos, certificado y sello en Base64, caracteres
XML válidos y límites de longitud. Se admite hasta 16384 caracteres de certificado,
8192 de cadena y 4096 de sello. El límite de cadena proviene del XSD; los límites
de certificado y sello acotan la captura del sistema.

Son datos del comprobante bancario, distintos del CSD y sello del emisor del CFDI.
La validación comprueba formato y coherencia del grupo; no autentica la firma del
banco ni confirma que una transferencia se haya realizado. La consulta o validación
del CEP en Banco de México no se automatizó en esta fase.

Los cuatro datos se guardan y recuperan con el pago, incluida la carga de un pago
anterior para sustituirlo. El XML incorpora el grupo solo cuando se captura. La
serialización representa las plecas de `CadPago` como `&#124;`, conserva el valor
al leer el XML y evita el doble escape de esa referencia. El PDF muestra el
indicador «Cadena de pago: 01 — SPEI»; el contenido completo permanece en el XML.

## Base de datos y actualización

La migración `20261105041146_20261104_B_CadenaSpeiPago` agrega cuatro columnas
opcionales a `Pagos`: `TipoCadPago` como `nvarchar(2)` y las otras tres como
`nvarchar(max)`. Las filas anteriores quedan con valores nulos; no se cambian
sus importes ni datos bancarios. Ya se aplicó en `FacturacionDev` y se verificó
la estructura mediante una consulta de solo lectura.

Para una instalación que se actualiza por SQL, respaldar la base, detener el
sitio y ejecutar `docs/sql/Facturacion.CadenaSpeiPago.Sql2014.sql` sobre la base
correcta antes de arrancar los archivos nuevos. Es incremental de esta fase;
no sustituye los scripts anteriores. Si se usa EF, aplicar la migración en lugar
del script. El script puede repetirse sin reemplazar los datos ya capturados y
puede ejecutarse sobre una base sin historial EF.

El script se probó en SQL Server local con compatibilidad 120, no en una
instancia nativa de SQL Server 2014. Los DTO cambiados pertenecen a Documentos;
no se modificaron los contratos compartidos congelados.

## Comprobaciones

- Compilación final sin errores ni advertencias.
- Regresión: 285 pruebas aprobadas, dos omitidas y ninguna fallida.
- Integración: 71 comprobaciones con servicios reales y una base temporal,
  incluyendo equivalencias y sustitución de pagos, guardado y reapertura SPEI,
  rechazo de datos incompletos o incompatibles, retiro del grupo completo,
  XML firmado validado contra XSD y conservación de las plecas al serializar.
- Script repetido sobre filas antiguas sin historial EF: conserva importes,
  monedas y datos SPEI ya capturados. Las bases temporales se eliminaron.
- PDF renderizado y revisado visualmente. No se realizó una prueba visual
  autenticada del formulario en navegador.
- No se conectaron un PAC real ni una pasarela de pagos.

## Referencias

La [guía y matriz del complemento de pagos del SAT](https://www.sat.gob.mx/portal/public/tramites/complemento-recepcion-de-pagos)
describen el grupo opcional y sus condiciones. Se contrastaron las páginas 37–38,
la pregunta frecuente 10 y las reglas CRP20229–CRP20235 con el XSD local Pagos 2.0
y su catálogo `c_TipoCadenaPago`, que admite `01`.

Banco de México publica la [consulta del CEP](https://www.banxico.org.mx/servicios/mi-spei_-transferencias-ban.html)
y su [validador](https://www.banxico.org.mx/validador-cep-spei/).
