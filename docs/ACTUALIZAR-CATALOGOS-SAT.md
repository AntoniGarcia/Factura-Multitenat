# Actualizar catálogos del SAT

En el panel del operador, abre **Catálogos del SAT**, elige un catálogo y despliega
**Cargar actualización**. Selecciona el archivo oficial completo del SAT en `.xls` o
`.xlsx`, de hasta 50 MB, y pulsa **Aplicar actualización**. Confirma el catálogo y el archivo.

La opción requiere acceso a catálogos y permiso para editar configuración. **Recargar
consulta** solo vuelve a consultar los datos; no importa archivos.

La carga continúa en segundo plano aunque cierres la pantalla. Al volver se muestra su
estado. Solo se aplica el catálogo seleccionado. Se conservan las claves antiguas como no
vigentes, y se registran versión, fecha de carga, operador y huella del archivo en bitácora.
Los comprobantes existentes no se modifican. Un error revierte la carga completa.

Para CFDI utiliza el libro completo `catCFDI`; para Carta Porte, el libro de catálogos
3.1; para Comercio Exterior 2.0, el Excel correspondiente al catálogo elegido. No subas
una selección de filas: las claves ausentes se consideran retiradas.

Se admiten actualizaciones con la estructura de los archivos actuales. Si el SAT cambia
el formato o la versión del complemento, hay que adaptar el importador. No se descargan
archivos de Internet automáticamente ni se puede editar libremente una clave desde el panel.

Si se reinicia el servidor durante la carga, revisa la última fecha y vuelve a enviarla si
no se completó. El proceso usa una transacción por catálogo y no necesita una migración.
En IIS, el límite de solicitudes debe permitir el tamaño del archivo y la identidad del
sitio necesita permiso de modificación en el almacenamiento privado configurado.
