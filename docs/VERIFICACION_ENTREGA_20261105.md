# Verificación de documentos para la entrega

Fecha: 5 de noviembre de 2026.

## Resultado

Los casos probados de factura estándar, Constructoras, Notaría, Comercio Exterior, Carta Porte nacional y complemento de pagos completaron creación, guardado, reapertura, modificación, cálculo y generación de PDF/XML. También se comprobó la factura de compras de timbres del operador.

El SMTP real de una empresa aceptó seis mensajes, uno por tipo de documento, con PDF y XML. El usuario confirmó la recepción de los seis mensajes con sus adjuntos. Los XML adjuntos se compararon byte por byte con los XML descargables y el historial registró los envíos exitosos. No se usó el SMTP del operador para sustituir el de la empresa.

La parte funcional probada pasó. Posteriormente se cerró el paquete IIS con generación de PDF/XML y envío de borradores en Staging, sin PAC ni CSD reales. El detalle de esa entrega y sus pruebas está en ENTREGA_IIS_20261105.md.

## Alcance de las pruebas

Se utilizó el inventario de `UI_Funcional.md`. No se ampliaron notas de crédito, datos de terceros, cuentas prediales ni servicios parciales de vivienda. Tampoco se añadieron módulos nuevos.

Las bases temporales se crearon con nombres propios de prueba y se eliminaron al terminar. Los documentos usan datos ficticios y certificados sintéticos. El certificado se cargó mediante el servicio real de CSD; el PAC se sustituyó por el doble de desarrollo. No se consumieron timbres reales ni se realizaron cobros. No se usaron certificados privados de una empresa real ni credenciales de un PAC.

La configuración SMTP existente se leyó para los envíos autorizados. Su contraseña no se imprimió ni se incluyó en las muestras. El usuario configuró el SMTP en su empresa; esa configuración se conserva.

## Operaciones comprobadas

- Factura estándar: cantidad, precio editable, descuento, IVA, vista previa sin guardar y persistencia de los importes. En el ejemplo modificado: 3 × 120 = 360, IVA 57.60 y total 417.60.
- Constructoras: obra privada y pública, retención local y ejemplo de la captura del ingeniero. Trabajos 200,000; amortización del 30 % por 60,000; base 140,000; IVA 22,400; total comercial 162,400. Deducciones de 1,000, 2,000, 400 y 600 dejan líquido 158,400. Se comprobó el anticipo relacionado y se rechazó un líquido negativo.
- En ese ejemplo, el total fiscal es 161,400 al descontar la retención local de 1,000. El total comercial y el líquido son importes distintos: las otras deducciones contractuales no se convirtieron en impuestos del CFDI.
- Notaría: perfil del notario, instrumento, inmueble, vendedor y comprador, guardado y reapertura. La operación notarial de 500,000 coincide con subtotal 431,034.48 más IVA 68,965.52; sus importes se mantienen separados de los honorarios facturados.
- Comercio Exterior: datos del complemento 2.0, domicilios, mercancía y valores en USD; comprobación de su suma y XML contra XSD.
- Carta Porte: origen, destino, vehículo, operador, mercancía, cantidad, pesos y distancia; modificación, PDF y XML 3.1. Se mantuvo el autotransporte nacional.
- Pagos: factura PPD, pago parcial de 120 sobre 417.60, saldo 297.60, siguiente parcialidad, rechazo de sobrepago y cancelación de la última parcialidad con restauración del saldo.
- Operador: preparación y emisión simulada de una factura de compra; base 100, IVA incluido desglosado en 16 y total 116; PDF, XML, conservación de datos históricos y descargas autorizadas.
- Correo: destinatario y asunto inválidos, bloqueo de borradores y cancelados, ausencia de SMTP, registro de fallos y seis envíos reales con adjuntos correctos.
- Aislamiento: otra empresa no abre documentos ajenos, aunque pertenezca a la misma cuenta; otra cuenta no consulta documentos, clientes ni productos ajenos. Se verificaron rechazos HTTP sin sesión o permiso.

## Evidencia

- Verificador de documentos y correo: 337 comprobaciones aprobadas.
- Verificador de facturas de compras del operador: 68 comprobaciones aprobadas.
- Recálculo independiente y revisión de archivos: 118 comprobaciones sobre 15 XML y 16 PDF, con 17 páginas renderizadas.
- Regresión final: 318 pruebas aprobadas, ninguna fallida y 2 omitidas. Las omitidas son la instalación del operador que necesita una conexión de prueba explícita y una previsualización manual de PDF.
- Compilación final del servidor: sin errores ni advertencias.

Una ejecución anterior de la regresión agotó los 30 segundos de espera de un índice de texto completo. La prueba aislada pasó y la regresión final sin otros verificadores de base de datos en paralelo también pasó. La consulta directa al catálogo principal devolvió 56 coincidencias de «cemento» y sus índices estaban poblados. No se cambió el código de búsqueda ni se desactivó esa prueba.

Las muestras se encuentran en `outputs/revision-entrega-20261105/muestras`. Se revisaron sus páginas renderizadas; no se encontró texto fuera de los límites. El PDF notarial emitido ocupa dos páginas en este caso, sin pérdida del contenido.

## Cierre del paquete de revisión

Los tres puntos pendientes de esta verificación quedaron atendidos en la entrega documentada en `ENTREGA_IIS_20261105.md`:

1. Revisión de borradores con PDF/XML y correo en Staging, sin timbrarlos ni consumir saldo.
2. Retiro del alta de notas de crédito y de la captura adicional de datos fiscales por concepto y servicios parciales de vivienda. Se conserva el acceso a datos históricos.
3. Publicación IIS y un único script acumulado desde el esquema original del ingeniero, probados juntos en una base temporal. El proceso publicado completó 273 comprobaciones, incluida la descarga de PDF/XML de los seis tipos y WebAssembly comprimido con Brotli.

El script se validó con sintaxis de SQL Server 2014 y compatibilidad 120 en el motor local. No se probó sobre un motor SQL Server 2014 nativo ni dentro del IIS del ingeniero.

El PAC real, la pasarela y los CSD reales siguen fuera de esta entrega.

## Límites

Son pruebas de casos concretos mediante los servicios reales y peticiones HTTP; no equivalen a recorrer cada combinación posible de todas las pantallas. No se repitió aquí la captura manual de todos los formularios en el navegador. La validación XSD y la firma sintética no acreditan aceptación de un PAC real, que está fuera del alcance solicitado.
