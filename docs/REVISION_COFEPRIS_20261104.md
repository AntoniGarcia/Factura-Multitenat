# Revisión de vigencia sanitaria en Carta Porte

Fecha: 4 de noviembre de 2026.

## Resultado

Antes de agregar la captura sanitaria se contrastó el archivo instalado con el
libro enlazado desde el portal actual del SAT. Los tres catálogos sanitarios siguen
indicando «Por definir» en fecha inicial: cinco sectores COFEPRIS, veinte formas
farmacéuticas y cuatro condiciones especiales de transporte.

Por esta razón no se habilitó su captura ni se hicieron obligatorios esos campos.
La existencia de atributos en el XSD y reglas condicionales en la matriz no permite
inventar la fecha inicial de las claves. Esta es una decisión técnica basada en
el catálogo revisado; no sustituye la determinación fiscal de una operación concreta.

Se corrige la prioridad del informe anterior: estos campos no se consideran un
error pendiente del formulario nacional actual. Su activación requiere comprobar
la publicación de las vigencias correspondientes.

## Fuentes comprobadas

El enlace antiguo de descarga no respondió. Se localizaron los enlaces de documentos
en los datos públicos del portal nuevo:

- [Sección Carta Porte del SAT](https://www.sat.gob.mx/portal/public/tramites/complemento-carta-porte).
- [Datos públicos de esa sección](https://www.sat.gob.mx/public/data/tramites/3039AGRCCP.json).
- [Catálogo enlazado por el SAT](https://ampocdevbuk01a.s3.us-east-1.amazonaws.com/Catalogos_Carta_Porte31_2db736cf0c.xls).

La copia descargada tiene SHA-256
`C457A7D67BE11C2557FBE1DFA3D30A8450C5312D4B9EF06B094A3D7CA26415AD`.
El libro identifica publicación del 17 de junio de 2024; la comprobación del enlace
se realizó en la fecha de esta revisión.

## Actualización realizada

Se compararon diez hojas: configuración de autotransporte, permisos, figuras,
mercancías, subtipos de remolque, materiales peligrosos, embalajes y los tres
catálogos sanitarios. Nueve coinciden en sus valores; en bienes transportados
solo difieren trece descripciones. No cambian claves, fechas ni indicadores de
peligrosidad en esas hojas.

Se respaldó el libro anterior y se sustituyó el archivo local por la descarga
oficial. Se volvió a cargar únicamente `c_ClaveProdServCP` en `FacturacionDev`:
48757 claves vigentes. Por ejemplo, la descripción de `15101505` quedó como
«Combustible diésel». Las copias fiscales de documentos guardados no se reescribieron.

No se creó una migración ni se añadieron tablas, columnas, formularios o contratos.
El libro XLS está excluido de Git por la configuración existente; la actualización
queda en los recursos locales para las publicaciones que se generen desde aquí.

## Verificación

La compilación final terminó con cero errores y cero advertencias. Se comprobó
la carga y las descripciones en SQL y la respuesta HTTP 200 de la aplicación local.
No se repitió la regresión de 278 pruebas porque no hubo cambios de código.

La evidencia de descarga, comparación, respaldo e importación está en
`tmp/cofepris-sat-20261104/`, excluida de Git. La comparación no certifica los demás
catálogos del SAT que no forman parte de esas diez hojas.

La siguiente fase propuesta es revisar el complemento de pagos: equivalencias,
relación de sustitución y datos SPEI opcionales. PAC real y pasarela de pagos
continúan fuera del alcance actual.
