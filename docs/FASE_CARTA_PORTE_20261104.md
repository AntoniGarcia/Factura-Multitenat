# Carta Porte 3.1: clasificación y límites del traslado nacional

Fecha: 4 de noviembre de 2026.

## Alcance de esta fase

Se revisó el traslado nacional por autotransporte ya implementado. Se añadió la
declaración de material peligroso por mercancía y se cerró la validación de los
casos que requieren datos todavía no capturados. Esta fase no completa todos los
escenarios de Carta Porte.

El importador conserva los indicadores de los catálogos SAT: `Remolque` de
configuración de autotransporte y `Material peligroso` de bienes transportados.
Rechaza archivos que no incluyen esas columnas o contienen indicadores inválidos
antes de modificar el catálogo. La vigencia considera fecha inicial y final.

## Comportamiento

- Indicador de mercancía `0`: el XML omite `MaterialPeligroso`.
- Indicador `0,1`: exige confirmar la clasificación según la composición de la
  mercancía. Si se confirma `No`, se guarda y se incluye ese atributo en el XML.
- Indicador `1` o declaración `Sí`: se bloquea el caso hasta disponer de captura
  de clave de material peligroso, embalaje y seguro ambiental.
- Configuración con remolque obligatorio: se bloquea hasta capturar subtipo y placa.
  La configuración con remolque opcional permite el caso actual sin remolques.
- Cambiar la clave de mercancía limpia la declaración anterior en el formulario.
- El servidor valida al guardar y al generar XML, incluidos los borradores
  anteriores. Si cambia el catálogo, exige revisar una declaración incompatible.
- El PDF muestra la declaración cuando existe. No se modifican las copias de
  comprobantes emitidos ni sus archivos guardados.

La modalidad sigue limitada a traslado nacional, dos ubicaciones y un operador.
No se ampliaron recorridos internacionales ni otras modalidades de transporte.

## Base de datos y despliegue

La migración `20261104223450_20261104_B_ClasificacionCartaPorte` añade tres columnas
anulables:

- `SatConfiguracionAutotransporte.Remolque`: `nvarchar(3)`.
- `SatClaveProdServCartaPorte.MaterialPeligroso`: `nvarchar(3)`.
- `MercanciasCartaPorte.MaterialPeligroso`: `nvarchar(2)`.

Se aplicó en la base local `FacturacionDev`. El arranque detectó los metadatos
faltantes y recargó automáticamente los dos catálogos: 28 preparados, 2 cargados.
No quedaron claves vigentes sin indicador. No se eliminaron datos de usuarios,
empresas ni documentos. No se cambiaron los contratos de la carpeta congelada.

`docs/sql/Facturacion.ClasificacionCartaPorte.Sql2014.sql` es un script incremental
para una base existente con estas tablas. No reemplaza el esquema inicial ni los
scripts anteriores. Se probó dos veces y también sin historial EF, en SQL Server
local con compatibilidad 120. No se ejecutó en una instancia nativa de SQL Server
2014. Solo registra la migración si existe historial EF con la migración anterior.

## Verificación

- Compilación de cliente y servidor sin errores ni advertencias nuevas.
- 264 pruebas aprobadas, 2 omitidas y ninguna fallida.
- 18 comprobaciones SQL: migración, repetición del script, importación real,
  clasificación, remolque obligatorio, cambios de catálogo y archivo inválido.
- La base temporal de integración se eliminó al finalizar.
- XML con declaración `No` validado contra el XSD de Carta Porte 3.1.
- XSD local comparado con la descarga oficial del SAT: coincide.
- Aplicación local iniciada y respuesta HTTP 200 comprobada.

No se realizó una revisión visual autenticada completa de este formulario en esta
fase. La evidencia técnica está en `tmp/carta-porte-sat-20261104/`, excluida de Git.
La coincidencia del XSD no certifica que todos los archivos de catálogos instalados
sean la publicación más reciente. Tampoco equivale a aceptación por un PAC real.

## Fuentes y pendientes

Se consultaron el [XSD oficial de Carta Porte 3.1](https://www.sat.gob.mx/sitio_internet/cfd/CartaPorte/CartaPorte31.xsd),
las [preguntas frecuentes del SAT para autotransporte](https://www.sat.gob.mx/minisitio/CartaPorte/documentos/PreguntasFrecuentes_Autotransporte.pdf)
y la matriz de errores 3.1, en particular CP155–CP157 y CP182–CP184.

La siguiente fase propuesta es capturar remolques y después los datos completos
de materiales peligrosos. PAC real y pasarela de pagos continúan fuera del trabajo
actual.
