# Fase 2: validaciones de Comercio Exterior 2.0

Revisión: 4 de noviembre de 2026.

## Referencias revisadas

- [SAT: Comercio Exterior](https://wwwmat.sat.gob.mx/consultas/61165/comprobante-de-comercio-exterior).
- [XSD de Comercio Exterior 2.0 publicado por el SAT](https://www.sat.gob.mx/sitio_internet/cfd/ComercioExterior20/ComercioExterior20.xsd).
- Estándar y matriz de errores de Comercio Exterior 2.0, revisión A, descargados
  para consultar reglas y fórmulas. El sitio de descargas `omawww.sat.gob.mx` no
  respondió; se usaron copias del documento técnico en el repositorio público
  [sat-catalogos](https://huggingface.co/datasets/mayrop/sat-catalogos/tree/main/raw/complementos/comercio-exterior/2-0-a).

Se descargó directamente del SAT el XSD y se comparó su XML con el archivo local:
coinciden. Esto no certifica que todos los XLS instalados sean la última
publicación. Los archivos utilizados conservan sus fechas de origen: CFDI
20260806 y fracciones 20240513; deben sustituirse desde el operador cuando el
SAT publique o entregue una actualización posterior.

## Cambios

Se agregó validación común para la captura y la generación XML. Comprueba
INCOTERM, CURP del emisor según su tipo de persona, certificado de origen y
ausencia de información global. El patrón CURP corresponde al tipo SAT e incluye
la clave X admitida por ese esquema.

En mercancías comprueba la fracción cuando corresponde y su omisión para servicios,
la positividad del precio aduanero de bienes y las tolerancias de cantidad por
precio. Se serializan cantidad con tres decimales y precio aduanero con seis para
que la precisión coincida con las fórmulas. También se comprueba la conversión
entre importes del CFDI y valores USD, agrupada por identificador, y el tratamiento
del descuento para la fracción de muestras `9801000100`.

Los identificadores pueden repetirse entre conceptos; no se permite repetir una
misma combinación de identificador y fracción entre mercancías.

Se validan los cruces entre país y estado del receptor, formatos de CP y registro
tributario, pertenencia a la UE para el número de exportador, CP/estado/municipio/
localidad del emisor y claves numéricas de colonia contra su CP. La unidad aduanera
se comprueba contra la UMT de la fracción y su vigencia.

La revisión se aplica al guardar datos y al recuperar/generar el XML del borrador;
la emisión vuelve a comprobarla. No basta con las restricciones del formulario.
Los borradores pueden seguir incompletos: las condiciones obligatorias para emitir
se exigen al preparar el XML, y los datos ya capturados se revisan al guardar.

## Base de datos

Migración: `20261104193407_20261104_A_CrucesFiscalesComercioExterior`.

Añade cinco columnas opcionales:

- `SatFraccionArancelaria.UnidadMedida`.
- `SatPais.FormatoCodigoPostal`.
- `SatPais.FormatoRegistroTributario`.
- `SatPais.ValidacionRegistroTributario`.
- `SatPais.Agrupaciones`.

Los importadores conservan estos metadatos. El de fracciones rechaza un archivo
sin UMT antes de sustituir el catálogo. Al arrancar, la preparación de catálogos
detecta los datos antiguos sin estos campos y vuelve a cargar países y fracciones.

La migración se probó primero en una base temporal y después se aplicó en
`FacturacionDev`. El arranque local completó automáticamente esos dos catálogos:
28 preparados, 2 cargados. No se borraron empresas, usuarios, compras ni documentos.
No se modificaron los contratos compartidos congelados.

## Script para SQL Server 2014

`docs/sql/Facturacion.CrucesComercioExterior.Sql2014.sql` añade únicamente estas
cinco columnas en una base existente. No crea desde cero la base ni sustituye el
script acumulado de las migraciones anteriores.

Se probó en SQL Server local con compatibilidad 120, dos veces seguidas y también
sin historial EF. Usa `COL_LENGTH` para evitar columnas duplicadas. Si el historial
EF existe y ya contiene la migración anterior, registra esta nueva; no inventa
el historial de las bases creadas manualmente. No fue una prueba en una instancia
nativa de SQL Server 2014.

## Verificación

- Compilación de cliente y servidor sin errores ni advertencias nuevas.
- Regresión final: 253 aprobadas, 2 omitidas y ninguna fallida.
- 20 comprobaciones con SQL: migración, script repetible, catálogos reales, UMT,
  formatos extranjeros, UE, domicilios, reimportación y rechazo de archivo inválido.
- Las bases temporales se eliminaron al finalizar.
- XSD local comparado con el publicado actualmente por el SAT: coincide.

La evidencia local está en `tmp/comercio-sat-20261104/`, excluida de Git. Incluye
matriz/estándar consultados, registros de compilación, pruebas, integración,
migración y arranque. No se hizo una revisión visual autenticada completa en esta fase.

## Límites que continúan

La modalidad de captura sigue siendo ingreso por exportación definitiva A1 a
receptor extranjero. No se ampliaron traslados internacionales, propietarios,
destinatarios distintos ni otras modalidades. La captura continúa asociando una
mercancía por renglón de concepto.

Cuando un país tiene validación tributaria en línea, no se sustituye esa consulta
por una expresión regular: su comprobación externa queda pendiente de la
integración correspondiente. Tampoco se incorporó un servicio de cotizaciones
cambiarias ni se acredita automáticamente la procedencia legal de valores
simbólicos permitidos por la normatividad. Un XML estructuralmente válido no
equivale a aceptación por un PAC real.

PAC real y pasarela de pagos continúan fuera de esta fase. La siguiente fase
propuesta es Carta Porte 3.1.
