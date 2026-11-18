# Ajustes de interfaz y recuperación de errores

Esta revisión mejora la consulta móvil, la presentación de timbres y la captura de datos. No añade migraciones ni cambia los contratos de plataforma, los cálculos fiscales, los precios del servidor o el proceso de timbrado.

## Cambios

- Los paquetes muestran cantidad de timbres, total en MXN con IVA, precio unitario y vigencia. El distintivo de menor precio compara los valores recibidos del servidor.
- Documentos presenta tarjetas compactas en móvil y permite plegar los filtros, conservando búsqueda, ordenamiento, consulta y apertura.
- Usuarios usa tarjetas en móvil, conserva Editar como acción principal y agrupa las acciones secundarias. Incluye paginación de 12, 24 o 48 usuarios para mantener accesibles todas las filas.
- La empresa activa se recorta con puntos suspensivos en pantallas pequeñas. Se ajustaron el contraste de las pestañas y los textos de interacción en los tres temas.
- El arranque fija la cultura es-MX. Si no llega a cargarse el script inicial de Blazor, muestra el error y permite reintentar.
- Forma de pago muestra el valor seleccionado. La sustitución de un pago permanece disponible en una sección opcional desplegable.
- Clientes, Productos, Facturas, Pagos, Carta Porte y la edición existente de notas de crédito avisan antes de descartar cambios. Cancelar conserva la captura; guardar o restaurar los valores originales no produce una confirmación innecesaria. Los borradores vacíos se eliminan después de aceptar la salida.
- El visor PDF oculta el canvas durante la carga o un error, permite reintentar y comunica los fallos al redibujar tras cambiar el tamaño de pantalla.

## Verificación

- Compilación de la solución: cero errores y cero advertencias.
- Suite existente: 319 pruebas aprobadas, 13 omitidas y ninguna fallida, de 332 pruebas descubiertas.
- Navegación, captura, reversión de cambios, guardado y recuperación del PDF: 26 comprobaciones en Edge y WebKit con perfil móvil.
- PDF: cuatro comprobaciones adicionales de recuperación y ausencia de errores, incluida una falla de redibujado al cambiar el ancho.
- Usuarios: listado controlado de 13 registros en ambos motores, con 12 filas en la primera página y una en la segunda; comprobación de anchos de 320, 390, 768 y 1440 píxeles y apertura del menú de acciones.
- Las comprobaciones de notas de crédito utilizan respuestas controladas para verificar la confirmación antes de eliminar un borrador. No habilitan ni acreditan un flujo nuevo de emisión de notas.
- Los clientes y productos temporales de las pruebas de guardado se eliminaron al terminar.

WebKit con un perfil de iPhone permite comprobar el motor y el tamaño de pantalla, pero no sustituye la prueba en el teléfono físico. Estas verificaciones no acreditan timbrado real con un PAC.

## Entrega

La generación del ZIP y la publicación en IIS quedan aplazadas por indicación de Luis, hasta integrar los cambios pendientes de su compañero. No se debe presentar esta revisión de código como una actualización ya instalada en el servidor.

## Compras y facturas del operador — 18 de noviembre

El módulo antes llamado «Pagos pendientes» ahora se presenta como «Compras y facturas» y conserva el filtro inicial de pagos pendientes. «Confirmar pago» mantiene la acreditación manual existente y explica que el saldo se entrega a la cuenta. La confirmación permanece bloqueada mientras está abierto el diálogo para evitar solicitudes simultáneas desde esa pantalla.

Después de acreditar se ofrecen «Ver compras pagadas» y, con el permiso de preparar facturas, «Abrir factura de esta compra». La columna Factura distingue entre por preparar, pendiente de timbrar, en proceso, timbrada y requiere revisión. Las acciones permiten revisar una factura antes de solicitar su timbrado; abrirla no vuelve a acreditar la compra. En móvil los botones se distribuyen en varias líneas y las celdas crecen para contenerlos.

El timbrado de facturas de compras continúa limitado por el servidor a Development con el PAC simulado. El diálogo muestra esa limitación antes de operar. Esta revisión no habilita timbrado real de compras ni integra una pasarela.

Para la futura pasarela se recomienda sustituir la confirmación manual por la confirmación del pago validada en el servidor, manteniendo una sola acreditación por compra. La facturación debe conservar un seguimiento independiente: un reintento de factura no debe repetir la entrega de timbres.

Se verificaron los perfiles de consulta, acreditación y facturación en Edge y WebKit: cancelar no solicita un abono, el error conserva la compra pendiente, abrir una factura no repite la acreditación ni solicita timbrado y cada perfil conserva sus acciones permitidas. Las tarjetas móviles se comprobaron a 320 y 390 píxeles, sin recortes horizontales ni botones fuera de su celda. Las respuestas de compras y sesiones utilizadas en esta comprobación fueron simuladas; no se acreditó ni timbró ninguna compra real.

Al integrar `afd6853`, la suite detectó una prueba que esperaba omitir todo el resumen de impuestos para conceptos exentos. Se actualizó únicamente esa expectativa: la [guía de llenado del SAT, páginas 24–25](https://wwwmatnp.sat.gob.mx/cs/Satellite?blobcol=urldata&blobkey=id&blobtable=MungoBlobs&blobwhere=1705376468921&ssbinary=true) describe el resumen con base, impuesto y factor para ese caso. La prueba comprueba además que se omitan tasa, importe y total de traslados, y que se conserve el total del comprobante. No se modificó el generador de XML.

La solución integrada compiló con cero errores y cero advertencias. La ejecución final de la suite existente terminó con 319 pruebas aprobadas, 13 omitidas y ninguna fallida, de 332 descubiertas. La entrega de esta revisión sigue siendo de código; el ZIP y la publicación en IIS permanecen aplazados.
