# Equivalencias del complemento de pagos

Fecha: 4 de noviembre de 2026.

## Alcance

El pago puede recibirse en una moneda distinta de la de sus facturas. Cada abono y
saldo permanece en la moneda de su factura; el reparto convierte el abono a la
moneda recibida dividiéndolo entre `EquivalenciaDR`. Por ejemplo, 58 USD con una
equivalencia de 0.05 USD por MXN representan 1,160 MXN recibidos.

La equivalencia admite diez decimales. Para la misma moneda vale uno; al cambiar
de moneda se solicita una equivalencia nueva. El servidor comprueba moneda,
precisión, saldo disponible y reparto, también antes de emitir. XML y PDF
conservan la moneda, equivalencia y precisión de cada documento relacionado.

El reparto sigue siendo completo: admite los márgenes de redondeo de la
conversión, pero no importes recibidos sin aplicar a documentos. La sustitución
de pagos y el grupo opcional SPEI quedan para otra fase. No se integró un PAC.

## Base de datos y catálogo

La migración `20261104233416_20261104_B_EquivalenciasDePago` amplía
`PagosDocumentos.EquivalenciaDR` a `decimal(28,10)` y agrega
`DecimalesMonedaDR`. Las filas anteriores toman la precisión de `SatMoneda`.
Ya se aplicó en la base local `FacturacionDev`.

Se corrigió la importación de los decimales de `c_Moneda`: el libro SAT puede
contenerlos como texto. Antes, el yen terminaba con dos decimales; ahora conserva
cero. Se recargaron las 183 monedas del libro local y se verificaron JPY=0,
MXN=2 y USD=2. No hubo diferencias entre la precisión de los documentos pagados
y la del catálogo después de la migración local.

## Actualización de otra instalación

Respaldar la base y detener el sitio antes de sustituir los archivos. Con el
servidor nuevo y su configuración SQL existente, recargar primero `c_Moneda`:

```powershell
dotnet Facturacion.Server.dll --cargar-catalogos "RUTA\catCFDI_V_4_20260806.xls" c_Moneda
```

Después aplicar `docs/sql/Facturacion.EquivalenciasDePago.Sql2014.sql` sobre la
base correcta y arrancar el sitio. Es un script incremental de esta fase: exige
las tablas previas y no sustituye los scripts de las fases anteriores. Puede
ejecutarse sin historial de EF y repetirse sin volver a modificar los datos.
Si la instalación administra migraciones con EF, usar la migración en lugar
del script, después de recargar el catálogo.

El script se probó con nivel de compatibilidad 120 sobre SQL Server local más
reciente. No se ejecutó en una instancia nativa de SQL Server 2014.

## Comprobaciones

- Compilación final: cero errores y cero advertencias.
- Regresión: 281 pruebas aprobadas, dos omitidas y ninguna fallida.
- Integración con los servicios reales: 23 comprobaciones en una base temporal,
  incluyendo pago mixto USD/MXN, JPY sin decimales, conservación de diez decimales,
  rechazo de importes incorrectos, aislamiento entre empresas y actualización de
  saldos en la moneda original. La base temporal se eliminó al terminar.
- XML firmado de prueba validado contra XSD y PDF revisado visualmente. Esto no
  acredita aceptación por un PAC.
- Script repetido sobre filas antiguas: conserva equivalencias y completa las
  precisiones cero/dos sin perder datos. La base temporal se eliminó.
- No se completó una prueba visual autenticada del formulario en navegador en
  esta fase; sí se compiló y se verificaron sus operaciones mediante servicios.

## Referencias

La conversión y sus márgenes se contrastaron con las páginas 31–34 de la
[guía de llenado del SAT](https://ampocdevbuk01a.s3.us-east-1.amazonaws.com/Guia_llenado_pago_0a0d7b758a.pdf)
y la matriz de errores CRP 2.0 revisión B enlazadas por el
[portal del complemento de recepción de pagos](https://www.sat.gob.mx/portal/public/tramites/complemento-recepcion-de-pagos).
