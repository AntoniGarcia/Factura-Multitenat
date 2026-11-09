# Funcionamiento del sistema

Guía operativa · 6 de noviembre de 2026 · [Índice](../README.md)

## 1. Cuenta, empresa y usuarios

Una cuenta representa al contratante. Puede reunir varias empresas emisoras y tiene un titular. El operador fija sus límites de empresas y usuarios activos; los valores iniciales son una empresa y tres usuarios, incluido el titular.

Las empresas de la cuenta comparten clientes, productos y saldo de timbres. Cada empresa conserva documentos, series, certificados, vehículos, figuras y configuración propios. Cambiar de empresa no permite abrir comprobantes de otra empresa.

El titular crea usuarios internos, asigna sus permisos y elige a cuáles empresas de la cuenta pueden acceder. Un usuario mantiene el mismo conjunto de permisos en esas empresas. Desactivarlo impide su uso en la cuenta; la eliminación exige las validaciones de actividad y la confirmación correspondiente.

Un usuario de la aplicación no es un login SQL. La conexión técnica a la base la utiliza el backend con la identidad configurada en instalación.

## 2. Permisos y licencias

Los 66 permisos asignables se distribuyen así:

- Documentos: listado, descarga y eliminación.
- Emitir: crear, modificar, cancelar, timbrar, enviar y eliminar para cada uno de los seis tipos.
- Catálogos: crear, modificar, desactivar y eliminar clientes, productos, series, vehículos y figuras.
- Tienda: comprar timbres.
- Empresa: datos y certificados; configuración general, correo, impuestos y monedas.

El servidor agrega accesos implícitos necesarios; por ejemplo, emitir requiere consultar los catálogos y documentos correspondientes. Crear permite trabajar borradores propios; modificar permite además los de otros usuarios. Los permisos de eliminación no autorizan borrar documentos timbrados.

Las variantes Notaría, Constructoras y Comercio Exterior requieren también la licencia de la empresa. El titular administra empresas y usuarios; estas facultades no se convierten en casillas para usuarios internos. Los operadores tienen permisos administrativos separados.

## 3. Configuración inicial de una empresa

Completar datos del emisor, domicilio fiscal, régimen y contacto. Configurar series y folios, impuestos, monedas y correo. El CSD para una emisión real corresponde a la empresa emisora, no al PAC.

MXN es moneda fija. En Configuración → Monedas se habilitan otras claves del SAT utilizadas por la empresa. Quitar una la desactiva para nuevas capturas; no cambia documentos guardados. El tipo de cambio se captura por documento.

Para correo de comprobantes configurar el SMTP de la empresa y probar conexión. El SMTP del operador atiende únicamente correos del sistema. Una configuración incompleta o un error de conexión impiden el envío; no hay sustitución silenciosa.

## 4. Clientes y productos

Clientes y productos pertenecen a la cuenta. La empresa de alta sirve como procedencia/filtro. Una edición posterior puede usarse en nuevas capturas de sus empresas, pero no debe reescribir los datos fiscales de un documento emitido.

El cliente conserva RFC, nombre fiscal, régimen, domicilio fiscal y datos de contacto. El RFC se valida en el servidor; esta comprobación no equivale a consultar el padrón del SAT. El nombre y código postal fiscal deben corresponder a la información real del receptor.

Para RFC genérico nacional, régimen 616 y uso S01 siguen las reglas del sistema. En una venta individual puede capturarse el nombre del receptor por factura; una factura con información global exige PUBLICO EN GENERAL.

Un producto conserva nombre, descripción, clave SAT, unidad e impuestos. Su precio se precarga y puede modificarse en el concepto de la factura. La descripción del renglón también es editable. El ID interno solo precarga NoIdentificacion en Comercio Exterior.

Guardar una factura guarda sus conceptos; no crea automáticamente nuevos productos de catálogo. Un registro utilizado no debe eliminarse físicamente; se desactiva. La eliminación física está condicionada a que no exista uso, incluidos borradores.

## 5. Flujo común de documentos

1. Elegir tipo y receptor dentro de la empresa activa.
2. Capturar datos generales, moneda y forma/método de pago aplicables.
3. Agregar conceptos, cantidades, precios, descuentos e impuestos.
4. Completar los datos de la variante.
5. Revisar importes y vista previa.
6. Guardar borrador; abrirlo de nuevo y comprobar lo capturado.
7. Descargar o enviar las salidas permitidas por el estado y el modo de ejecución.

La vista previa refleja cambios del formulario; no significa que estén guardados. El servidor valida y recalcula al persistir. El botón de timbrar se conserva separado de guardar: guardar no debe consumir un timbre.

En revisión Staging, los borradores pueden generar PDF/XML y correo sin un PAC ni CSD reales. Los archivos permiten revisar datos y operaciones; no son una certificación fiscal. En una emisión real el folio se reserva mediante el flujo del servidor.

Los documentos admiten los estados borrador, timbrando, timbrado, error, cancelado y en_cancelacion. La cancelación real y su acuse requieren el servicio fiscal correspondiente; una prueba simulada no acredita ese resultado.

### Factura estándar

Cantidad × precio forma el importe; después se aplican descuento y bases/impuestos según el concepto. Por ejemplo: 2 × 100 = 200; descuento 20; base gravada 180; IVA 16 % = 28.80; total 208.80.

Se usa decimal y se conserva precisión de cálculo; la presentación se redondea conforme a la moneda. El total no debe obtenerse sumando textos ya formateados de la pantalla. Información global se usa cuando corresponde al tipo de operación, con periodicidad, meses y año válidos.

### Constructoras

Además de los conceptos se capturan datos de obra/estimación, trabajos, anticipo y amortización, retenciones y deducciones que correspondan. La estimación comercial, su líquido y el total fiscal son importes distintos.

Caso probado: trabajos 200,000; amortización de 30 % = 60,000; base 140,000; IVA 22,400; total comercial 162,400. Deducciones contractuales de 1,000, 2,000, 400 y 600 dejan líquido de 158,400. En el caso de obra pública probado, la retención local de 1,000 reduce el total fiscal a 161,400; las otras deducciones no se convierten automáticamente en impuestos del CFDI.

Este ejemplo describe el cálculo configurado y probado; los porcentajes y su procedencia deben corresponder al contrato real.

### Notaría

Completar perfil notarial, instrumento, operación, inmuebles, vendedores y compradores. La información de la operación inmobiliaria se distingue de los honorarios/conceptos facturados.

La prueba registró una operación por 500,000 y su desglose 431,034.48 + 68,965.52, conservado al reabrir. No implica que los honorarios del notario deban tener ese mismo importe.

### Comercio Exterior 2.0

Capturar datos generales del complemento, receptor extranjero cuando corresponda, domicilios, mercancías, fracciones y valores en USD. Los datos deben coincidir con conceptos y sumas del documento. El servidor valida los cruces implementados y genera el complemento dentro del XML.

Las claves se seleccionan de catálogos; no deben sustituirse por texto libre. La validación XSD comprueba estructura, no confirma por sí sola aceptación del PAC o de una operación aduanera real.

### Carta Porte 3.1

El alcance es autotransporte nacional propio. Capturar origen/destino, códigos postales, distancias, vehículo, permiso, seguros, figura y mercancías. Remolques y material peligroso se capturan cuando correspondan a los datos de transporte.

Los pesos, cantidades, ubicación y configuración del vehículo deben ser coherentes. Los códigos postales ayudan a resolver estado y municipio; comprobar el domicilio antes de guardar. Relacionar una mercancía con un producto permite trazabilidad, pero no sustituye elegir su clave específica Carta Porte.

No se incluyen modalidades marítima, aérea, ferroviaria ni operaciones internacionales por tener referencias en los catálogos.

### Complemento de pagos 2.0

Relacionar documentos emitidos con pago diferido, registrar abono, moneda, tipo de cambio/equivalencia y parcialidad. El importe pagado no es el total de la cabecera CFDI: el comprobante tipo P usa Moneda XXX y total cero, mientras el complemento contiene los importes del pago.

Caso probado: saldo 417.60; abono 120; nuevo saldo 297.60. Se verificaron sobrepago, parcialidad siguiente y restauración del saldo al cancelar la última parcialidad en el flujo de prueba.

Cuando hay monedas diferentes, EquivalenciaDR relaciona unidades de moneda del documento por unidad de moneda del pago. No reutilizar un tipo de cambio sin comprobar su sentido. Los datos SPEI se incluyen cuando corresponden; no deben inventarse para cualquier pago.

## 6. Descarga y correo

El listado permite buscar documentos y descargar sus archivos bajo permiso. El backend verifica empresa y autorización; conocer una ruta o identificador no otorga acceso.

Para enviar, indicar destinatario y revisar asunto/adjuntos. La empresa necesita SMTP completo. Su correo de contacto se utiliza para respuesta y la copia a la empresa va oculta. El historial registra resultado; aceptación del servidor SMTP no equivale a entrega en bandeja.

El PDF presenta los datos e importes. El XML contiene la estructura del documento. Una vista previa, un PDF descargado y una factura timbrada representan estados distintos; consultar siempre el estado guardado.

## 7. Panel del operador

El operador administra cuentas, empresas, límites, paquetes, compras y configuración de plataforma según sus permisos. Los operadores secundarios no sustituyen al principal. Las listas conservan inactivos para consulta/reactivación cuando procede.

Los paquetes generales se ofrecen a cuentas; los exclusivos pertenecen a una cuenta y los utilizan sus empresas. Comprar registra una operación pendiente. La acreditación queda sujeta al flujo administrativo disponible; no existe detección bancaria automática mediante una pasarela ya integrada.

La cantidad acreditada y los movimientos se controlan en el servidor. Las empresas consumen del saldo común de su cuenta, conservando la empresa de origen del movimiento.

### Factura de compras de timbres

Configurar los datos fiscales del proveedor en el operador. Preparar la factura de una compra con los datos fiscales de la compradora y las claves SAT del paquete. El precio incluye IVA; la factura lo desglosa. Caso probado: total 116, base 100 e IVA 16.

El CSD y los folios para emitir esta factura son del proveedor del SaaS. No se utiliza el CSD de la compradora ni uno del PAC. La preparación, PDF de revisión y timbrado son pasos distintos. La emisión real y sus reintentos necesitan las credenciales/integración fiscal correspondientes.

### Catálogos SAT

Consultar por clave, nombre o descripción desde el apartado de catálogos. Para actualizar, obtener la versión oficial y utilizar la carga del operador. Revisar el resultado y versión antes de continuar capturas.

La carga automática inicial proviene de los recursos entregados en el servidor; no significa actualización automática permanente desde Internet.

## 8. Conservación de datos

Desactivar no elimina el historial. Los datos fiscales emitidos permanecen como copia del momento de emisión. Los borradores se pueden modificar o eliminar bajo autorización. Cambiar de cuenta/empresa, actualizar un producto o desactivar una moneda no debe alterar documentos históricos.

Para mantenimiento, respaldo y recuperación consultar [Instalación y actualización](Instalacion-y-actualizacion.md). Para evidencia y límites consultar [Pruebas y pendientes](Pruebas-y-pendientes.md).
