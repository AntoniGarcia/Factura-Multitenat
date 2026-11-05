# Remolques en Carta Porte 3.1

Fecha: 4 de noviembre de 2026.

## Captura y validación

El traslado permite agregar hasta dos remolques, cada uno con subtipo SAT y placa.
El selector muestra las opciones y filtra por clave o descripción. Las placas se
normalizan a mayúsculas, sin espacios ni guiones, y deben tener de 5 a 7 caracteres
alfanuméricos. No se admiten placas repetidas dentro del traslado.

El servidor consulta la configuración vehicular: indicador `0` excluye remolques,
`1` exige al menos uno y `0,1` los permite opcionalmente. También verifica la
vigencia de cada subtipo al guardar y al generar XML. La captura de mercancías
peligrosas sigue pendiente y permanece bloqueada.

Los datos se conservan en el traslado, con `EmpresaId`, filtro global y sellado de
empresa. Se incluyen en la bitácora al crear o modificar. El XML escribe el grupo
`Autotransporte/Remolques` después de Seguros y lo omite cuando no hay remolques.
El PDF muestra subtipo y placa de cada uno. La consulta de borradores, las salidas
fiscales y las copias para emisión cargan estos datos.

## Catálogos y base de datos

La migración `20261104224848_20261104_B_RemolquesCartaPorte` crea:

- `SatSubTipoRemolque`: claves, descripción y vigencia del catálogo común.
- `RemolquesCartaPorte`: datos capturados, orden, empresa y relación con el traslado.

Se aplicó en `FacturacionDev`. El arranque cargó automáticamente 31 subtipos
vigentes del archivo SAT incluido. El inventario pasó de 28 a 29 catálogos. El
catálogo nuevo también se puede consultar y actualizar desde el operador.

El libro original contiene un espacio inicial en el nombre de esta hoja y usa
«Remolque o semirremolque» como encabezado de descripción. El importador reconoce
ambas particularidades sin modificar el archivo SAT.

El script incremental `docs/sql/Facturacion.RemolquesCartaPorte.Sql2014.sql` se
probó dos veces y sin historial EF en SQL Server local con compatibilidad 120.
No sustituye los scripts anteriores y no se probó en una instancia nativa de SQL
Server 2014. No elimina tablas ni registros existentes. No se modificaron los
contratos de la carpeta compartida congelada.

## Comprobaciones

- Compilación final: cero errores y cero advertencias.
- Regresión: 274 pruebas aprobadas, 2 omitidas y ninguna fallida.
- XML de dos remolques validado contra el XSD de Carta Porte 3.1.
- 21 comprobaciones de integración: importación y reimportación del archivo real,
  script repetible, creación, reapertura, modificación, PDF, configuración
  obligatoria/opcional/prohibida, subtipo inexistente o desactivado, conservación
  de datos ante rechazo y aislamiento entre empresas de la misma cuenta.
- La base temporal de integración se eliminó al terminar.
- Carga automática local finalizada y respuesta HTTP 200 en `http://localhost:5184`.

Las evidencias están en `tmp/remolques-sat-20261104/`, excluida de Git. No se hizo
una revisión visual autenticada completa ni se envió el XML a un PAC real.

## Fuentes y siguiente fase

Se revisaron el [XSD oficial de Carta Porte 3.1](https://www.sat.gob.mx/sitio_internet/cfd/CartaPorte/CartaPorte31.xsd),
la condición CP184 de la matriz de errores y las
[preguntas frecuentes del SAT para autotransporte](https://www.sat.gob.mx/minisitio/CartaPorte/documentos/PreguntasFrecuentes_Autotransporte.pdf).

La siguiente fase propuesta es la captura completa de materiales peligrosos y
sus condiciones de clave, embalaje y seguro ambiental. El traslado internacional
y otras modalidades no se incorporaron aquí. PAC real y pasarela de pagos siguen
fuera del trabajo actual.
