# Revisión local del sistema

Fecha: 4 de noviembre de 2026.

Los recorridos descritos abajo aprobaron la revisión local. Esto comprueba el funcionamiento de los casos ejecutados; no sustituye las pruebas fiscales con el PAC, los certificados definitivos ni la validación del servidor de entrega.

## Resultados de la revisión inicial

- Compilación Release del servidor, cliente y biblioteca compartida: correcta, sin errores.
- Batería automatizada final: 208 aprobadas, 0 fallidas y 2 omitidas de 210.
- El caso omitido de instalación del operador se ejecutó por separado con una base nueva: aprobado. En total, 209 casos automatizados aprobados.
- El único caso que quedó sin ejecutar es la previsualización manual con QuestPDF Companion. Se revisaron los PDF generados directamente como alternativa.
- Recorridos de documentos, cálculos, timbrado simulado, cancelación y acceso por HTTP: 278 comprobaciones aprobadas.
- Facturas de compras de paquetes, reintentos y descargas del cliente y operador: 68 comprobaciones aprobadas.
- PDF de documentos: 14 archivos y 15 páginas revisadas; sin páginas vacías ni texto fuera de los límites. Se revisó también su distribución visual.

Las comprobaciones de integración son verificaciones dentro de herramientas temporales; no equivalen a 346 pruebas nuevas incorporadas a la batería del repositorio.

## Recorridos comprobados

1. **Factura básica:** crear, guardar, reabrir, modificar precio y cantidad, calcular sin guardar, generar PDF y XML sellado, emitir con PAC simulado y descargar los archivos persistidos. Cambiar el catálogo después de emitir conservó el XML original.
2. **Constructoras:** guardar y reabrir la estimación de obra privada y pública; comprobar el 5 al millar y generar PDF/XML. La prueba pública usó únicamente esa deducción, sin amortización de anticipos.
3. **Notaría:** perfil del notario, instrumento, inmueble y partes; reapertura, PDF, XML y emisión simulada. El caso usado fue de una vendedora y una compradora, sin copropiedad.
4. **Comercio Exterior:** receptor extranjero, domicilios, mercancía, identificador, tipo de cambio e INCOTERM; guardar, reabrir, PDF/XML y emisión simulada. El PDF emitido recupera ahora el complemento del XML guardado.
5. **Complemento de pagos:** factura PPD, guardar y modificar un abono, reabrir, PDF/XML, emisión simulada, liquidación, rechazo de sobrepago y recuperación del saldo al cancelar un pago.
6. **Carta Porte 3.1:** vehículo y conductor, origen/destino nacionales, mercancía y peso; crear, reabrir, modificar, PDF, XML validado con XSD y emisión simulada. El caso es de autotransporte nacional.
7. **Timbres:** siete documentos consumieron siete timbres y dejaron cero reservados. Repetir el timbrado de un documento emitido no consumió otro; cancelar un pago tampoco devolvió el timbre consumido.
8. **Aislamiento:** dos cuentas y tres empresas. Clientes y productos se compartieron entre empresas hermanas; otra cuenta no pudo consultarlos. Los documentos y descargas siguieron separados incluso entre empresas de la misma cuenta.
9. **Permisos por HTTP:** acceso propio, rechazo anónimo y sin permisos; rechazo de consultas, ediciones y eliminaciones desde otra cuenta. La propuesta de correo no expuso destinatarios ni historial a otra cuenta.
10. **Compras del operador:** factura desde la compra, firma, recuperación de intentos, descargas PDF/XML, permisos y aislamiento entre cuentas. Estas pruebas utilizaron servicios reales del proveedor y el PAC simulado.
11. **Instalación:** dos solicitudes concurrentes produjeron un solo operador principal; contraseña con hash, permisos, bitácora y cierre del asistente incluso después de desactivar al operador.

La batería existente también cubrió RFC y dígito verificador, folios concurrentes, aritmética e idempotencia de timbres, sesión y refresh, monedas, catálogos, descargas, validaciones SMTP, impuestos y esquemas SAT.

En el navegador local se comprobó la apertura de una factura, el cambio de cantidad y cálculo en pantalla, la búsqueda de formas de pago por descripción, Información global y el desglose corregido de la vista rápida. No se guardaron los ajustes hechos para esa comprobación visual.

## Correcciones realizadas

- **Vista rápida de Documentos:** el total incluía impuestos locales, pero faltaban sus renglones. Ahora muestra ISH y retenciones locales con su importe y signo.
- **PDF emitido de Comercio Exterior:** faltaba el complemento que sí aparecía en borrador. Ahora obtiene domicilios, mercancías e importes del XML emitido, sin consultar la captura editable. Se añadieron cuatro pruebas de regresión, incluida la prohibición de entidades externas del XML.

No se modificaron contratos congelados ni se añadieron migraciones. La base principal no se modificó. Los recorridos utilizaron bases temporales, catálogos SAT consultados en modo de lectura y certificados sintéticos; no se utilizó un CSD definitivo ni se contactó al SAT o a un PAC real. Las bases temporales de estos recorridos se eliminaron al terminar.

## Pendientes y límites

- Los cuatro avisos iniciales de NuGet se resolvieron en la actualización posterior descrita abajo.
- No se enviaron correos reales. Queda comprobar conexión y entrega con el SMTP definitivo del SaaS y de una empresa.
- No se probaron un CSD expedido por el SAT, timbrado/cancelación ante el PAC real ni la pasarela de pagos.
- No se ejecutó esta revisión en el servidor del ingeniero ni en un motor SQL Server 2014 nativo. La compatibilidad del script SQL tiene su verificación separada; esta prueba de aplicación se hizo con el SQL Server local.
- No son pruebas exhaustivas de todas las combinaciones fiscales. La actualización posterior amplió copropiedad, amortización de anticipos y captura aduanera; las operaciones de transporte fuera del autotransporte nacional siguen fuera de estos recorridos.
- No se hicieron pruebas de carga ni una nueva auditoría completa de seguridad de todos los endpoints. Los rechazos documentados corresponden a los recorridos enumerados.

## Evidencia local

En `tmp/revision-completa-20261104/` quedan el resultado TRX final, los registros `suite-final.log`, `wizard.log`, `flujos.log`, `proveedor.log`, `pdfs.log`, la compilación final y las herramientas temporales. Esta carpeta está excluida de Git y no forma parte del paquete de entrega.

Para repetir la batería del repositorio desde la raíz:

```powershell
dotnet test .\tests\Facturacion.Pruebas\Facturacion.Pruebas.csproj --configuration Release
```

El caso SQL del operador requiere su base temporal y la variable específica de pruebas; no debe apuntarse a una base con datos reales.

## Actualización posterior: dependencias y casos fiscales

Se atendieron únicamente los puntos 1 y 2 solicitados después de la revisión inicial. No se configuró SMTP ni se preparó una nueva entrega o un ZIP.

### Dependencias

- Paquetes de ASP.NET Core, Blazor, EF Core y extensiones de Microsoft: `10.0.14`.
- `System.Security.Cryptography.Xml` y `System.Security.Cryptography.Pkcs`: `10.0.14`.
- `Microsoft.OpenApi`: `2.12.2`, requerida por la nueva versión de ASP.NET Core OpenAPI.
- Runtime mínimo del servidor: .NET y ASP.NET Core `10.0.14`, comprobado en el archivo generado `Facturacion.Server.runtimeconfig.json`. El servidor de entrega necesitará Hosting Bundle 10.0.14 o una revisión posterior de .NET 10.
- Restauración y compilación Release: correctas, con cero advertencias y cero errores.
- Auditoría NuGet de los cuatro proyectos de la solución, incluidos paquetes transitivos: no reportó vulnerabilidades conocidas al consultar el 4 de noviembre de 2026. No se desactivó la auditoría ni se suprimieron sus avisos.

La selección del parche se contrastó con los avisos de Microsoft para [Blazor WebAssembly](https://github.com/advisories/GHSA-g27p-qf6j-c96c) y [PKCS](https://github.com/advisories/GHSA-846f-f764-76mx).

### Casos añadidos y correcciones

**Notaría:** dos inmuebles, dos vendedores y dos compradores en copropiedad. Se conservaron porcentajes de 33.33/66.67 y 60/40 al reabrir y generar XML/PDF. Se rechazaron porcentajes que no suman 100, RFC repetido, tres decimales en porcentajes e importes inconsistentes.

Se encontraron dos límites faltantes y se corrigieron en el servidor: ahora se rechazan importes superiores a `decimal(18,6)` y valores con más de seis decimales significativos. Antes, el primer caso podía producir una excepción SQL y el segundo podía redondearse al guardar.

**Constructoras:** un anticipo emitido con clave 84111506 y relación 07 se aplicó a tres estimaciones. Se comprobaron el descuento, el IVA del remanente, la leyenda y la suma consumida: 108 + 90 + 2 = 200, exactamente el subtotal del anticipo.

Dos borradores se prepararon contra el mismo saldo. Después de emitir el primero, se rechazó el segundo cuando su amortización ya superaba el saldo disponible, sin consumir folio ni timbre. Ajustar el porcentaje permitió continuar. También se rechazó amortizar sin relacionar el anticipo y guardar una amortización superior al saldo.

**Comercio Exterior:** factura en USD con dos mercancías, fracción arancelaria vigente, unidad aduanera, cantidad, valor unitario, certificado de origen y descripciones específicas. Se comprobó el PDF/XML, la emisión simulada y la recuperación de sus archivos. Se rechazaron una fracción inexistente, cantidad con cuatro decimales, modelo sin marca, domicilio del emisor fuera de México, certificado de origen sin número e identificadores repetidos.

Se encontraron y corrigieron otros dos casos: el total USD debe coincidir con la suma de `ValorDolares` redondeada a dos decimales; si una mercancía registra cantidad, unidad o valor unitario aduaneros, las tres propiedades se exigen en todas las mercancías. Son las comprobaciones CCE122 y CCE165–CCE166 de la [matriz de Comercio Exterior 2.0 consultada](https://cdn.taecel.com/src/fiscal/descargas/matriz-errores-comercio-exterior.pdf), junto con el XSD local del SAT. La matriz enlazada es una copia publicada por un proveedor, no un enlace alojado en el portal del SAT.

Estas pruebas no acreditan la matriz fiscal completa del PAC. No se incorporó una comprobación de la columna UMT de la fracción arancelaria: el catálogo de la base actual no conserva esa columna. Tampoco se incorporaron todas las tolerancias aduaneras ni reglas de regímenes especiales. No debe interpretarse el timbrado simulado como validación de esas reglas.

### Resultado de esta ampliación

- Batería final: 218 aprobadas, cero fallidas y dos omitidas. Incluye diez casos nuevos de regresión de Comercio Exterior.
- Instalación del operador ejecutada por separado en otra base nueva: una aprobada. Total de casos automatizados ejecutados y aprobados: 219; la previsualización manual con Companion continúa omitida.
- Herramienta de casos fiscales con servicios reales y certificado sintético: 136 comprobaciones aprobadas. Incluye preparación básica y casos especiales; no son 136 pruebas nuevas permanentes del repositorio.
- PDF de esta ampliación: 14 archivos y 17 páginas, con comprobación de campos, importes y límites de página, además de revisión visual de los documentos especiales.
- EF Core actualizado: el modelo coincidió con las migraciones existentes. No se añadieron migraciones, no se cambió el esquema ni se modificaron datos de la base principal. Las bases temporales se eliminaron al terminar.

La evidencia de esta ampliación queda en `tmp/cierre-fiscal-20261104/`: auditoría de paquetes en JSON, TRX final, compilación y registros de casos, asistente y PDF. Esa carpeta no se publica ni se incorpora a Git.
