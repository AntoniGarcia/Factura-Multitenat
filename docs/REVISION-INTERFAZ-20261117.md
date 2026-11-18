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
