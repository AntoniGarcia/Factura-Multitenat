# Pruebas y pendientes de entrega

Estado documentado · 6 de noviembre de 2026 · [Índice](../README.md)

## 1. Qué acredita esta revisión

Los casos de prueba registrados comprobaron creación, guardado, reapertura, modificación, operaciones, generación de PDF/XML y envío de los seis tipos: factura estándar, Constructoras, Notaría, Comercio Exterior, Carta Porte nacional y pagos. También se comprobó la factura de compra de paquetes del operador.

Se utilizaron datos ficticios y bases temporales para las pruebas de emisión. No se consumieron timbres reales ni se hicieron cobros. La revisión incluye tanto servicios reales con doble de PAC de desarrollo como la modalidad Staging de archivos de borrador sin PAC/CSD reales.

La evidencia es de casos concretos. No prueba todas las combinaciones de pantallas, todos los navegadores ni aceptación fiscal por un PAC real.

## 2. Evidencia registrada el 5 de noviembre

### Documentos y operaciones

- Verificador de documentos y correo: 337 comprobaciones aprobadas.
- Verificador de facturas de compras del operador: 68 comprobaciones aprobadas.
- Recálculo independiente: 118 comprobaciones sobre 15 XML y 16 PDF, con 17 páginas renderizadas.
- Regresión de integración: 318 pruebas aprobadas, cero fallidas y dos omitidas, de 320 totales.

Las dos omitidas corresponden al alta del operador que necesita una conexión explícita de prueba y a una previsualización manual PDF. No deben describirse como aprobadas. La ejecución de regresión registró una advertencia por un ejecutable Debug bloqueado; su resultado de pruebas fue satisfactorio. La compilación de entrega se verifica por separado.

Casos relevantes:

- Cantidades, precio editable, descuentos e IVA; un caso modificado dio 360 + 57.60 = 417.60.
- Constructoras: estimación, amortización, deducciones, líquido y total fiscal diferenciados; rechazo de líquido negativo.
- Notaría: instrumento, operación, inmuebles y partes persistidos.
- Comercio Exterior: cruces, valores y complemento XML.
- Carta Porte: ubicaciones, pesos, transporte, remolques/material peligroso según el caso y relación opcional de producto.
- Pagos: parcialidad, saldo, rechazo de sobrepago y restauración en cancelación de prueba.
- Factura del proveedor: base 100 + IVA 16 = total 116, conservación histórica y descargas autorizadas.
- Aislamiento: rechazo de acceso a documentos de otra empresa y de catálogos de otra cuenta; rechazos sin sesión o permiso.

### Correo

Un SMTP real de empresa aceptó seis correos de prueba con PDF y XML. El destinatario confirmó recibir los seis con adjuntos. Los XML enviados se compararon con los descargables y se registró el resultado de envío.

También se probaron destinatarios/asuntos inválidos, falta de SMTP y fallos. No se recurrió al SMTP del SaaS como sustituto del de empresa.

Una prueba posterior de borradores registró aceptación SMTP; no debe confundirse esa aceptación con otra confirmación explícita de recepción. Esta fase documental no envió nuevos correos.

### Publicación compatible

La última prueba registrada de publicación autocontenida completó **276 comprobaciones**. Verificó proceso nativo con runtime incluido, salidas de seis tipos, permisos, persistencia de referencia de producto en Carta Porte y recursos WebAssembly con Brotli. Una prueba de asistente sin parámetros SQL obtuvo respuesta HTTP 200.

Las ejecuciones previas de 273 y 275 comprobaciones son antecedentes de otras revisiones del paquete; no se suman para inflar el resultado de la última.

La publicación se ejecutó incluso con una ruta DOTNET_ROOT inválida para comprobar que utilizaba su runtime incluido. IIS todavía requiere AspNetCoreModuleV2.

## 3. Comparación de bases del 6 de noviembre

La consulta de metadatos fue de solo lectura:

- Principal: 93 tablas, 950 columnas y 184 índices.
- Facturacion1.1: 92 tablas, 945 columnas y 182 índices.
- Ambas: 54 FK, 24 valores predeterminados, dos CHECK, tres índices de texto completo y 66 permisos de aplicación.
- Diferencia estructural identificada: sysdiagrams y sus objetos de soporte de SSMS.
- Historial EF: distinto por el origen de la base de prueba. No se sustituyó por un historial inventado.

El acumulado se ensayó partiendo del script original de SSMS y se comprobó su repetición en el estado admitido. Se validó sintaxis de SQL 2014 y ejecución con compatibilidad 120 en motor posterior. La comparación actual se hizo con ambas bases locales en compatibilidad 170; estos hechos son pruebas distintas, no una ejecución en SQL Server 2014 nativo.

La comparación no acredita que existan los mismos registros, usuarios SQL, configuración SMTP, certificados o archivos privados. Su resultado no autoriza borrar la base ni copiar datos de prueba al servidor.

## 4. Registro de evidencia para mantenimiento

Los informes fechados de verificación e IIS se conservan como antecedentes durante la consolidación. En el espacio de trabajo de desarrollo, las salidas de pruebas están bajo:

- `tmp/entrega-iis-20261105/regresion-integracion.log`.
- `tmp/entrega-iis-20261105/prueba-autocontenida.log`.
- `tmp/entrega-iis-20261105/wizard-autocontenido.log`.
- `tmp/documentacion-20261106/comparacion-fase2.log`.

Son evidencias locales, no dependencias de ejecución ni archivos obligatorios del servidor. Las muestras y registros deben revisarse antes de compartirlos: un paquete de documentación no debe incluir configuración privada ni secretos.

La fase de documentación no vuelve a ejecutar estas pruebas funcionales: las identifica por fecha y alcance. Su propia compilación y verificación de enlaces se registran en el plan documental.

## 5. Comprobación en el servidor receptor

Antes de aceptar la instalación:

1. Respaldar y aplicar el acumulado al esquema original correcto.
2. Comprobar identidad SQL, tablas y ejecución de procedimientos, incluido el del proveedor.
3. Verificar arranque IIS, ruta física, hostname, HTTPS y recursos del cliente.
4. Confirmar guardado de parámetros dentro de la carpeta privada.
5. Verificar carga/versiones de catálogos.
6. Entrar como operador y cliente; comprobar cambio de empresa y acceso permitido.
7. Crear un documento de muestra por variante habilitada, reabrirlo y revisar cálculos/PDF/XML.
8. Preparar el PDF de una compra de timbres del operador.
9. Probar SMTP de la empresa y confirmar recepción de adjuntos.
10. Comprobar que respaldo de base, archivos y llaves pueda recuperarse.

Las pruebas locales no sustituyen esta aceptación en el IIS del receptor. Los fallos anteriores de runtime o carga SAT requieren diagnóstico del servidor; no se dan por resueltos en el servidor remoto solo por funcionar localmente.

## 6. Fuera de la aceptación de revisión

Quedan para integración posterior:

- Credenciales PAC, CSD reales y validación de emisión/cancelación con ese servicio, tanto de empresas como del proveedor.
- Selección e integración de la pasarela y confirmación real de pagos.
- Prueba sobre un motor SQL Server 2014 nativo del receptor.
- Medición de carga, capacidad y rendimiento en su infraestructura.
- Revisión de configuración de producción, recuperación en otra máquina e identidad y pruebas adicionales de seguridad/accesibilidad según el entorno.

La validación XSD y los cálculos comprobados no certifican que todos los datos capturados coincidan con constancias fiscales o que un PAC acepte cada operación. No se declara cumplimiento fiscal integral a partir de XML de prueba.

El alcance acordado sigue siendo las funciones del inventario anterior actualizadas para esta aplicación; no se amplía a otras modalidades de Carta Porte o módulos nuevos.

## 7. Repetir comprobaciones de código

Desde la raíz del repositorio, con SDK y dependencias disponibles:

```powershell
dotnet build .\Facturacion.Server\Facturacion.Server.csproj -c Release --no-restore -p:UseAppHost=false
dotnet test .\tests\Facturacion.Pruebas\Facturacion.Pruebas.csproj --no-restore
```

Las pruebas que requieren SQL o una configuración explícita deben ejecutarse en el entorno de prueba previsto, nunca sobre datos de producción sin revisar sus requisitos. Un resultado omitido debe mantenerse identificado.

La guía de despliegue está en [Instalación y actualización](Instalacion-y-actualizacion.md); el funcionamiento esperado, en [Funcionamiento](Funcionamiento.md).
