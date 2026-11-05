# Materiales peligrosos en Carta Porte 3.1

Fecha: 4 de noviembre de 2026.

## Captura del traslado nacional

Cada mercancía declarada peligrosa permite seleccionar la clave de material del
SAT, su embalaje y una descripción opcional de hasta 100 caracteres. El traslado
captura una aseguradora ambiental y su póliza cuando contiene al menos una de
esas mercancías. Los campos aparecen al seleccionar «Sí».

El servidor comprueba la clasificación del bien transportado: `0` omite el atributo
de peligrosidad, `1` exige declarar «Sí» y `0,1` exige confirmar «Sí» o «No».
Una declaración incompatible se rechaza. La clave y el embalaje deben existir y
estar vigentes. No se admiten fichas peligrosas en mercancías declaradas «No» ni
seguro ambiental en traslados sin mercancías peligrosas.

La aseguradora admite de 3 a 50 caracteres; la póliza, de 3 a 30. Ni esos textos ni
la descripción del embalaje admiten el separador `|`. La validación se ejecuta al
guardar y antes de generar el XML, también para borradores existentes.

Cambiar la clave del bien transportado limpia la clasificación y la ficha anterior
en el formulario. Cambiar la declaración a «No» limpia los datos específicos. Al
guardar un traslado sin mercancías peligrosas, la interfaz no envía seguro ambiental.
Una petición directa con campos incompatibles se rechaza en el servidor.

Los datos se guardan en las copias del traslado y sus mercancías, se incluyen en la
bitácora y conservan el aislamiento por empresa. El XML incluye los atributos de la
mercancía y de Seguros; el PDF muestra la ficha y el seguro. No se reescriben archivos
emitidos anteriormente.

## Catálogos y base de datos

La migración `20261104230741_20261104_B_MaterialPeligrosoCartaPorte` crea los catálogos
`SatMaterialPeligroso` y `SatTipoEmbalaje`. Añade cinco columnas anulables:

- En `MercanciasCartaPorte`: `CveMaterialPeligroso`, `Embalaje` y `DescripEmbalaje`.
- En `TrasladosCartaPorte`: `AseguraMedAmbiente` y `PolizaMedAmbiente`.

Se aplicó en `FacturacionDev`. La carga automática completó 2337 materiales y 59
embalajes vigentes a partir del libro SAT incluido. El inventario suma 31 catálogos.
También se pueden consultar y actualizar desde el operador.

La importación conserva las claves de cuatro dígitos, incluidos sus ceros iniciales.
Cuando el libro contiene varias descripciones de una misma clave, las reúne para
poder buscarlas; no inventa claves nuevas ni elimina variantes por conservar solo
la última fila. Se limita su longitud antes de escribir en la base.

El script incremental `docs/sql/Facturacion.MaterialPeligrosoCartaPorte.Sql2014.sql`
se probó dos veces y sin historial EF, sobre SQL Server local con compatibilidad
120. No se ejecutó en una instancia nativa de SQL Server 2014. No sustituye el
esquema inicial ni los scripts de fases anteriores. No se modificaron los contratos
de la carpeta compartida congelada.

## Comprobaciones y límites

- Compilación final sin errores ni advertencias.
- 278 pruebas aprobadas, 2 omitidas y ninguna fallida.
- XML con material peligroso, embalaje y seguro validado contra el XSD Carta Porte 3.1.
- 38 comprobaciones SQL y de servicios reales: script repetible, importación,
  creación, reapertura, edición, PDF, campos obligatorios, claves inválidas o
  desactivadas, reimportación, limpieza al cambiar de mercancía y aislamiento.
- Base temporal eliminada al terminar; la base principal solo recibió la migración
  y los catálogos de esta fase.
- Arranque local completado, con respuesta HTTP 200 en `http://localhost:5184`.

La evidencia está en `tmp/peligrosos-sat-20261104/`, excluida de Git. No se hizo una
revisión visual autenticada completa ni se solicitó timbrado a un PAC real.

Esta fase cubre los datos de peligrosidad del traslado nacional por autotransporte
ya ofrecido. No completa campos sanitarios COFEPRIS, transporte internacional,
otras modalidades, ni acredita la cobertura legal de una póliza capturada. La
clasificación técnica del contenido corresponde al expedidor; el sistema verifica
coherencia con los catálogos y los datos obligatorios, no analiza sustancias.

Se revisaron el [XSD oficial Carta Porte 3.1](https://www.sat.gob.mx/sitio_internet/cfd/CartaPorte/CartaPorte31.xsd),
las condiciones CP155–CP157 y CP182–CP183 de su matriz de errores, y las
[preguntas frecuentes del SAT para autotransporte](https://www.sat.gob.mx/minisitio/CartaPorte/documentos/PreguntasFrecuentes_Autotransporte.pdf).
El hecho de cargar el libro incluido no certifica que todos sus catálogos sean la
publicación más reciente. PAC real y pasarela de pagos siguen fuera de esta fase.
