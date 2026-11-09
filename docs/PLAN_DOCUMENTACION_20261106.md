# Plan de consolidación documental

Fecha: 6 de noviembre de 2026. Fase 1: inventario y propuesta. No es el manual de entrega.

## Resultado de la revisión

Se inventariaron 55 Markdown del código fuente y sus guías de operación, con 10 914 líneas y 637.6 KB. Se excluyeron bin/obj, tmp, outputs, entregas generadas, paquetes, dependencias y los archivos personales de residencia. Los conteos son anteriores a crear este plan.

No se encontraron duplicados exactos por contenido. La repetición es temática: arquitectura, instalación, estados del proyecto y revisiones por fase explican asuntos similares en diferentes momentos. No se propone borrar directamente informes únicos por tener nombres parecidos.

Se revisaron 39 enlaces Markdown internos hacia archivos .md y todos sus destinos existen. Esta comprobación resuelve rutas y espacios codificados; no valida todas las anclas, enlaces web ni menciones de rutas escritas como texto. Las referencias desde código son principalmente comentarios y ayudas; no se encontró en la búsqueda una carga de estos Markdown como requisito de arranque.

## Decisiones propuestas

- Consolidar: 23 documentos. Extraer lo vigente, corregir contradicciones y retirar la copia sustituida solo después de respaldarla y reparar referencias.
- Archivar tras extraer: 23 informes históricos; 15 son archivos FASE_. No entregarlos como instrucciones actuales.
- Conservar protegidos: 2 archivos: AGENTS.md y Facturacion.Shared/Contratos/LEEME.md. No se modificarán ni moverán.
- Conservar internos: 4 archivos de planificación/reparto e integración del PAC.
- Actualizar índice: README.md.
- Conservar referencia original: docs/UI_Funcional.md.
- Conservar apoyo breve: docs/sql/LEEME.md, para distinguir los scripts SQL.

El resultado no es «55 archivos basura». Hay información válida que debe integrarse y evidencia que debe conservarse como historial.

## Los seis documentos de entrega

1. **LEEME.md.** Qué es el sistema, alcance acordado, requisitos, contenido del paquete y orden de lectura. En el repositorio seguirá siendo README.md; el paquete puede usar el nombre LEEME.md sin mantener dos índices manuales.
2. **Arquitectura.md.** Blazor WASM, API, SQL, almacén, sesión, permisos, separación cuenta/empresa e integración externa. La fuente del repositorio seguirá en ARQUITECTURA.md para conservar las referencias existentes; sus apartados citados por contratos deben seguir localizables.
3. **Base-de-datos.md.** Modelo de relaciones y diccionario de tablas, llaves, pertenencia, datos históricos, dinero/fechas y ubicación de XML/CSD. Contendrá la información útil de la guía actual, con redacción para la empresa.
4. **Instalacion-y-actualizacion.md.** IIS, publicación Windows x64 autocontenida, variante dependiente del runtime, esquema SQL, asistente, identidad de ejecución, permisos, cnf, certificados, actualización y respaldo/restauración. Debe diferenciar desarrollo local, prueba IIS y servidor de revisión.
5. **Funcionamiento.md.** Operador, titular, usuarios, catálogos, compras, acreditación, documentos, cálculos, PDF/XML, correo y complementos. Describirá solo las funcionalidades acordadas y habilitadas.
6. **Pruebas-y-pendientes.md.** Casos probados y evidencia con fecha, diferencias entre automatización/recorrido manual/servidor remoto y pendientes de CSD/PAC reales y pasarela. No presentará validación XSD como certificación o aceptación del PAC.

Los archivos para entregar se prepararán juntos en una carpeta independiente de sitio. El código conservará ayudas internas o referencias breves donde sean necesarias. La carpeta de entrega no incluirá AGENTS, prompts, reportes académicos, configuraciones privadas ni el archivo de historial documental.

## Diagramas que se prepararán en la fase técnica

- Contexto y despliegue: navegador, IIS/proceso .NET, SQL, almacén privado, SMTP y PAC externo pendiente.
- Contenedores y recorrido de petición: Client → Server → servicio → EF Core → SQL; consultas y salidas a archivos.
- Propiedad de datos: cuenta frente a empresa, usuarios y accesos, catálogos compartidos, series y documentos separados.
- Relaciones de la base en grupos: identidad/cuenta, documentos e impuestos, complementos y compras/facturas del proveedor. Evitar un dibujo de 93 tablas ilegible; el diccionario conservará el detalle.

Los diagramas se basarán en el modelo y código comprobados en la fase 2. No se generan diagramas de un estado anterior solo para completar la entrega.

## Contradicciones que deben corregirse

1. ARQUITECTURA.md todavía indica que no se almacena SMTP de empresas y habla de clientes/RFC por empresa. La decisión actual es SMTP obligatorio del emisor para comprobantes y catálogos por cuenta. Evidencia: ARQUITECTURA.md, apartados 4–6; AGENTS.md, cambios posteriores; entidades y servicios actuales.
2. RESUMEN_EJECUTIVO.md conserva un corte del 25 de septiembre y afirma clientes/productos aislados por empresa. REPORTE_ESTADO_SISTEMA.md mezcla el corte del 30 de octubre con secciones anteriores y recuentos de pruebas distintos. No basta cambiar su fecha.
3. docs/GUIA-ARQUITECTURA.md y docs/DEFENSA_TECNICA_DEL_SISTEMA.md repiten reglas anteriores de permisos, tenencia o SMTP y tienen tono de estudio personal. Deben alimentar la arquitectura técnica, sin crear otra fuente de verdad.
4. README.md aún menciona Azure. La entrega acordada es IIS para revisión; Azure no debe aparecer como paso necesario.
5. docs/ASISTENTE-CONEXION.md conserva una referencia a clave de instalación. La variante de revisión actual no la pide. Las guías de IIS señalan paquetes de octubre que ya no deben anunciarse como la entrega más reciente.
6. Las tres guías de actualización SQL explican orígenes y etapas distintos. Debe distinguirse el instalador para base vacía del acumulado desde el esquema original sin historial EF completo. No usar automáticamente dotnet ef database update sobre esa última base.
7. Hay informes de notas de crédito y captura fiscal adicional que no deben confundirse con alcance nuevo de entrega. La especificación original es inventario, no prueba de vigencia fiscal. Conservar solo lo aceptado en el manual funcional y trasladar la historia al respaldo.
8. La guía reciente de base de datos explica la estructura inspeccionada, pero las bases locales pueden restaurarse o cambiar. Una comparación del 5 de noviembre no demuestra que todas las copias sigan iguales el 6. El diagnóstico de IIS encontró una Facturacion1.1 anterior; antes de fijar el estado final hay que revisar el esquema efectivo.
9. Las guías deben diferenciar runtime .NET de AspNetCoreModuleV2. La publicación autocontenida incluye runtime, pero sigue necesitando IIS/módulo y Windows compatible. El hostname, puerto, certificado, identidad de IIS y usuario SQL son configuraciones distintas.
10. Las cifras 273, 275 y 276 corresponden a distintas pruebas del publicado; no se suman como casos únicos. La regresión de 318 aprobadas y dos omitidas y los envíos SMTP deben conservar su contexto y fecha.

## Referencias que condicionan la limpieza

- AGENTS.md cita UI_Funcional, planes, reparto y reporte de estado. No se modificará AGENTS: mantener rutas internas necesarias o referencias breves de compatibilidad.
- Los contratos compartidos citan ARQUITECTURA.md y REPARTO-EQUIPO.md. No cambiar contratos para mover documentación. Conservar la arquitectura en su ruta y respetar sus referencias de apartados.
- Código de esquemas SAT y una prueba señalan docs/DESPLIEGUE.md en sus ayudas. Mantener una referencia breve hacia el manual unificado mientras no se cambien esas ayudas de forma coordinada.
- herramientas/entrega-pc/INICIAR.cmd menciona LEEME.md en el mensaje de error. El paquete de PC debe seguir entregando esa ayuda o actualizarse coherentemente.
- Muchos informes enlazan otros informes. Antes de retirar archivos habrá que comprobar referencias entrantes, no solo su contenido.

Estos vínculos son documentales; no se detectó una dependencia de compilación que obligue a entregar todos los Markdown al servidor. La búsqueda no cubre automatizaciones externas que no estén en el repositorio.

## Orden de trabajo y cierre por fase

1. **Inventario, esta fase.** Completar la clasificación de los 55 archivos, registrar problemas y conservar todo sin modificaciones.
2. **Documentación técnica.** Redactar los seis documentos con datos actuales, contrastarlos con código y esquema, y preparar diagramas legibles. Confirmar alcance de revisión, uso de PAC/CSD reales y pruebas acreditadas.
3. **Limpieza.** Crear un ZIP de respaldo histórico fuera de sitio, con manifiesto de rutas/huellas, incluyendo documentos no versionados. Consolidar y retirar los sustituidos; conservar el historial de Git y ayudas protegidas. Verificar enlaces y rutas de herramientas.
4. **Revisión.** Comprobar nombres, rutas, comandos y coherencia entre documentos. Los ejemplos usarán nombres genéricos, sin contraseñas, llaves ni conexión real. Distinguir Windows del proceso de IIS del usuario SQL y del usuario del sistema.
5. **Entrega.** Preparar carpeta/ZIP documental independiente del publicado, con los seis manuales, diagramas y referencias necesarias a scripts. Los scripts SQL se identifican por origen, fecha y huella; no se entregan todos los scripts intermedios como si hubiera que ejecutarlos juntos.

No se ha archivado, borrado, renombrado ni reescrito ninguno de los 55 originales en esta fase. La limpieza de fase 3 depende de completar y revisar la consolidación; la fase 1 no autoriza presentar los seis manuales como ya terminados.

## Inventario archivo por archivo

### Actualizar índice — 1

#### README.md

Destino propuesto: LEEME.md del paquete; conservar README.md en el repositorio.

Mantener una sola entrada al proyecto, corregir referencias a Azure y diferenciar revisión de producción e instalación por script de migraciones EF.

Tamaño: 40 líneas; 1933 bytes. Versionado en Git.

Enlaces entrantes revisados: REPORTE_ESTADO_SISTEMA.md:493.

### Consolidar — 23

#### ARQUITECTURA.md

Destino propuesto: Arquitectura.md del paquete; conservar ARQUITECTURA.md como fuente.

Reemplazar reglas antiguas con el diseño actual, manteniendo la ruta y los apartados citados desde los contratos. No crear otra fuente de verdad contradictoria.

Tamaño: 370 líneas; 19341 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:34; REPORTE_ESTADO_SISTEMA.md:498.

Referencias desde código/herramientas: Facturacion.Shared/Contratos/IServicioTimbres.cs:9; Facturacion.Shared/Contratos/IServicioFolios.cs:7; Facturacion.Shared/Contratos/IServicioFolios.cs:36:    /// <b>No devuelve el folio a la serie.</b> ARQUITECTURA.md §5 es explícito: si el timbrado; Facturacion.Shared/Contratos/IServicioEmpresaEmisora.cs:30.

#### REPORTE_ESTADO_SISTEMA.md

Destino propuesto: Pruebas-y-pendientes.md.

Mezcla cortes de varias fechas y recuentos de pruebas. Separar estado actual de historia, con evidencia y limitaciones de cada verificación.

Tamaño: 500 líneas; 34673 bytes. Versionado en Git.

Enlaces entrantes revisados: ARQUITECTURA.md:237; FASES_Y_ARQUITECTURA.md:4; PROMPT-FASES-A.md:4; PROMPT-FASES-B.md:5; README.md:5; README.md:30; REPARTO-EQUIPO.md:4; RESUMEN_EJECUTIVO.md:7; docs/ESTADO-Y-CONTINUACION.md:5.

#### RESUMEN_EJECUTIVO.md

Destino propuesto: LEEME.md y Funcionamiento.md.

Corte antiguo; describe clientes y productos por empresa. Integrar resumen corregido en la entrada y el alcance funcional.

Tamaño: 73 líneas; 4162 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:31.

#### docs/ACTUALIZAR-CATALOGOS-SAT.md

Destino propuesto: Funcionamiento.md e Instalacion-y-actualizacion.md.

Procedimiento breve útil. Integrar carga inicial, actualización por operador y diferencia entre cambiar datos de catálogo y cambiar su estructura.

Tamaño: 26 líneas; 1694 bytes. Versionado en Git.

#### docs/ARRANQUE-LOCAL.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 211 líneas; 8416 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:10; README.md:32; REPORTE_ESTADO_SISTEMA.md:494; RESUMEN_EJECUTIVO.md:57.

#### docs/ASISTENTE-CONEXION.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 242 líneas; 15015 bytes. Versionado en Git.

#### docs/DEFENSA_TECNICA_DEL_SISTEMA.md

Destino propuesto: Arquitectura.md y Base-de-datos.md.

Explican decisiones útiles, pero duplican arquitectura e incluyen reglas antiguas. Extraer lo vigente, respaldar y retirar el manual sustituido después de reparar referencias.

Tamaño: 180 líneas; 25999 bytes. Versionado en Git.

#### docs/DESPLIEGUE.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores. Conservar una referencia corta en su ruta mientras código y pruebas la sigan citando.

Tamaño: 339 líneas; 15805 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:33; REPORTE_ESTADO_SISTEMA.md:495; RESUMEN_EJECUTIVO.md:62; docs/ARRANQUE-LOCAL.md:131.

Referencias desde código/herramientas: tests/Facturacion.Pruebas/EsquemasSatPruebas.cs:29:            Skip = $"Faltan los artefactos del SAT en '{EsquemasSatPruebas.Raiz}'. Ver docs/DESPLIEGUE.md.";; Facturacion.Server/Modules/Documentos/Salidas/EsquemasSat.cs:58:            "Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.");; Facturacion.Server/Modules/Documentos/Salidas/EsquemasSat.cs:187:                $"Falta 'cfdv40.xsd' en '{_raiz}'. Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.",; Facturacion.Server/Modules/Documentos/Salidas/EsquemasSat.cs:192:                $"Falta 'CartaPorte31.xsd' en '{_raiz}'. Se descarga del portal del SAT; ver docs/DESPLIEGUE.md.",.

#### docs/ENTREGA-IIS.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 56 líneas; 3811 bytes. Versionado en Git.

#### docs/ENTREGA_IIS_20261105.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores. Conservar sus resultados de pruebas en Pruebas-y-pendientes.md; es la fuente reciente de la variante autocontenida.

Tamaño: 55 líneas; 7644 bytes. Versionado en Git.

#### docs/FACTURACION_DE_PAQUETES.md

Destino propuesto: Funcionamiento.md, Base-de-datos.md y Arquitectura.md.

Conservar el flujo de compras y facturas del proveedor; retirar narración por fases y no presentar emisión simulada como timbrado real.

Tamaño: 283 líneas; 18402 bytes. Versionado en Git.

#### docs/GUIA-ARQUITECTURA.md

Destino propuesto: Arquitectura.md y Base-de-datos.md.

Explican decisiones útiles, pero duplican arquitectura e incluyen reglas antiguas. Extraer lo vigente, respaldar y retirar el manual sustituido después de reparar referencias.

Tamaño: 561 líneas; 28344 bytes. Versionado en Git.

#### docs/GUIA_BASE_DE_DATOS_20261105.md

Destino propuesto: Base-de-datos.md.

Base del diccionario y relaciones. Convertirlo en referencia de empresa, quitar el tono de defensa académica y fechar la revisión sin asumir que todas las bases locales sigan iguales.

Tamaño: 1124 líneas; 103794 bytes. No versionado todavía; respaldar antes de retirar.

#### docs/VERIFICACION_ENTREGA_20261105.md

Destino propuesto: Pruebas-y-pendientes.md.

Conservar pruebas de documentos y confirmación de correo. Distinguir los seis mensajes recibidos de envíos posteriores solo aceptados por SMTP.

Tamaño: 60 líneas; 6479 bytes. Versionado en Git.

#### docs/sql/Actualizar base completa.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 93 líneas; 5175 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/sql/Actualizar base del ingeniero.md:4; docs/sql/Actualizar base desde script.md:5.

#### docs/sql/Actualizar base del ingeniero.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 101 líneas; 6148 bytes. Versionado en Git.

#### docs/sql/Actualizar base desde script.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 136 líneas; 9191 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/sql/Actualizar base del ingeniero.md:10.

#### herramientas/entrega-archivos/LEEME.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 48 líneas; 3069 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/ENTREGA-IIS.md:6.

#### herramientas/entrega-iis/Actualizar el sistema.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 36 líneas; 2420 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/ENTREGA-IIS.md:18.

#### herramientas/entrega-iis/Configuracion del servidor.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 218 líneas; 13291 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/ENTREGA-IIS.md:15.

#### herramientas/entrega-iis/Donde se guarda la configuracion.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 64 líneas; 3359 bytes. Versionado en Git.

#### herramientas/entrega-iis/Pasos de instalacion.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores.

Tamaño: 113 líneas; 6422 bytes. Versionado en Git.

#### herramientas/entrega-pc/LEEME.md

Destino propuesto: Instalacion-y-actualizacion.md; evidencia a Pruebas-y-pendientes.md.

Reúne procedimientos de instalación, configuración o actualización que hoy están repartidos entre varias versiones. Integrar el procedimiento actual y distinguirlo de los anteriores. INICIAR.cmd lo nombra al fallar: mantener el LEEME de ese paquete o actualizar de forma coordinada la ayuda, sin romper los paquetes existentes.

Tamaño: 77 líneas; 4275 bytes. Versionado en Git.

Referencias desde código/herramientas: herramientas/entrega-pc/INICIAR.cmd:52:echo No se pudo iniciar. Revisa el mensaje anterior y el archivo LEEME.md..

### Archivar tras extraer — 23

#### FASES_Y_ARQUITECTURA.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 152 líneas; 8420 bytes. Versionado en Git.

#### docs/AUDITORIA-2026-08-19.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 426 líneas; 24422 bytes. Versionado en Git.

#### docs/COMPARACION_FUNCIONAL_CFDI_20261104.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 346 líneas; 20338 bytes. Versionado en Git.

#### docs/ESTADO-Y-CONTINUACION.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 272 líneas; 16103 bytes. Versionado en Git.

#### docs/FASE_CARTA_PORTE_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 79 líneas; 4322 bytes. Versionado en Git.

#### docs/FASE_COMERCIO_EXTERIOR_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 108 líneas; 5656 bytes. Versionado en Git.

#### docs/FASE_DATOS_FISCALES_Y_VIVIENDA_20261105.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 45 líneas; 4727 bytes. Versionado en Git.

#### docs/FASE_EQUIVALENCIAS_PAGOS_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 73 líneas; 3787 bytes. Versionado en Git.

#### docs/FASE_MATERIALES_PELIGROSOS_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 80 líneas; 4687 bytes. Versionado en Git.

#### docs/FASE_NOTAS_CREDITO_PPD_20261105.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 93 líneas; 5139 bytes. Versionado en Git.

#### docs/FASE_NOTAS_CREDITO_PUE_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 92 líneas; 5491 bytes. Versionado en Git.

#### docs/FASE_NOTAS_MIXTAS_POSTERIORES_20261105.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 84 líneas; 4526 bytes. Versionado en Git.

#### docs/FASE_NOTAS_PPD_IMPUESTOS_MIXTOS_20261105.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 86 líneas; 4717 bytes. Versionado en Git.

#### docs/FASE_OBJETO_IMPUESTO_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 73 líneas; 3893 bytes. Versionado en Git.

#### docs/FASE_PAGOS_OBJETOS_MIXTOS_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 70 líneas; 4008 bytes. Versionado en Git.

#### docs/FASE_REMOLQUES_CARTA_PORTE_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 68 líneas; 3630 bytes. Versionado en Git.

#### docs/FASE_SPEI_PAGOS_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 70 líneas; 3910 bytes. Versionado en Git.

#### docs/FASE_SUSTITUCION_PAGOS_20261104.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 67 líneas; 3544 bytes. Versionado en Git.

#### docs/FASE_SUSTITUCION_PAGOS_CON_NOTAS_20261105.md

Destino propuesto: Respaldo histórico; reglas vigentes a Funcionamiento.md y pruebas al informe.

Informe de una fase, no manual vigente. Extraer resultados y límites antes de retirar. Los informes de notas de crédito y captura fiscal adicional no deben ampliar el alcance acordado de entrega.

Tamaño: 75 líneas; 4234 bytes. Versionado en Git.

#### docs/PRUEBA-IIS-LOCAL.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 62 líneas; 3604 bytes. Versionado en Git.

Enlaces entrantes revisados: docs/ENTREGA-IIS.md:49.

#### docs/REPASO-SEGURIDAD.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 308 líneas; 15956 bytes. Versionado en Git.

#### docs/REVISION_COFEPRIS_20261104.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 64 líneas; 3314 bytes. Versionado en Git.

#### docs/REVISION_LOCAL_20261104.md

Destino propuesto: Respaldo histórico; hallazgos útiles a Arquitectura, Funcionamiento o Pruebas.

Corte histórico o informe de revisión. Conservar evidencia y trazabilidad, pero no incluirlo como procedimiento actual. Reparar sus referencias antes de retirar la copia activa.

Tamaño: 104 líneas; 11774 bytes. Versionado en Git.

### Conservar protegido — 2

#### AGENTS.md

Destino propuesto: Uso interno; fuera de la entrega.

Instrucciones permanentes. No se modifica ni se mueve en este trabajo; sus referencias se deben respetar.

Tamaño: 477 líneas; 26629 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:34; REPORTE_ESTADO_SISTEMA.md:348; REPORTE_ESTADO_SISTEMA.md:498.

Referencias desde código/herramientas: Facturacion.Server/Data/IEntidadDeCuenta.cs:5; Facturacion.Shared/Contratos/IUsoDeCatalogos.cs:9; Facturacion.Shared/Contratos/IServicioTimbres.cs:5; Facturacion.Shared/Plataforma/UsuarioDtos.cs:5.

#### Facturacion.Shared/Contratos/LEEME.md

Destino propuesto: Uso interno junto a los contratos.

Documenta la frontera congelada. No editar ni mover; no se requiere cambiar contratos para limpiar documentación.

Tamaño: 68 líneas; 3779 bytes. Versionado en Git.

### Conservar interno — 4

#### Facturacion.Server/Modules/Documentos/Pac/LEEME.md

Destino propuesto: Integración técnica del PAC, junto al código.

Guía necesaria para el ingeniero que conectará el PAC. Debe actualizarse su redacción y separar integración de acreditación fiscal, sin cambiar firmas ni implementar el PAC en esta fase.

Tamaño: 68 líneas; 3660 bytes. Versionado en Git.

#### PROMPT-FASES-A.md

Destino propuesto: Historial de planificación; mantener la ruta.

Planes y reparto antiguos citados por AGENTS.md. No entregarlos como instrucciones operativas vigentes.

Tamaño: 674 líneas; 31797 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:38; REPORTE_ESTADO_SISTEMA.md:349.

#### PROMPT-FASES-B.md

Destino propuesto: Historial de planificación; mantener la ruta.

Planes y reparto antiguos citados por AGENTS.md. No entregarlos como instrucciones operativas vigentes.

Tamaño: 190 líneas; 8878 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:38; REPORTE_ESTADO_SISTEMA.md:350.

#### REPARTO-EQUIPO.md

Destino propuesto: Historial de planificación; mantener la ruta.

Planes y reparto antiguos citados por AGENTS.md. No entregarlos como instrucciones operativas vigentes.

Tamaño: 361 líneas; 15163 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:39; REPORTE_ESTADO_SISTEMA.md:350.

### Conservar referencia — 1

#### docs/UI_Funcional.md

Destino propuesto: Inventario funcional original, anexo de consulta.

Especificación proporcionada por la empresa. Conservar el original; no usar sus capturas o reglas antiguas como prueba de vigencia actual.

Tamaño: 1133 líneas; 41612 bytes. Versionado en Git.

Enlaces entrantes revisados: README.md:35; REPORTE_ESTADO_SISTEMA.md:496.

### Conservar apoyo — 1

#### docs/sql/LEEME.md

Destino propuesto: Ayuda junto a los SQL; referenciar el manual principal.

Mantener una explicación breve que distinga el instalador de base vacía y el acumulado para el esquema original. Está junto al generador y los scripts; no convertirlo en otro manual largo.

Tamaño: 40 líneas; 2307 bytes. Versionado en Git.


## Verificación de esta fase

Se comparó la huella del contenido de cada uno de los 55 originales antes y después del inventario: permanecen idénticos. La clasificación contiene exactamente una entrada por archivo. La compilación Release del servidor, con sus referencias Client/Shared, terminó con cero advertencias y cero errores. No se ejecutaron migraciones, no se modificaron contratos ni se hicieron commits o push en esta fase.

## Fase 2 — manuales técnicos consolidados

Completada el 6 de noviembre de 2026. Se actualizó README.md como índice y ARQUITECTURA.md como referencia técnica. Se crearon:

- [Base de datos](Base-de-datos.md): diccionario de 93 tablas, 950 columnas, índices y 54 FK; distingue las tablas de aplicación del historial EF y sysdiagrams.
- [Instalación y actualización](Instalacion-y-actualizacion.md): IIS, identidad SQL, asistente, permisos y conservación de cnf/llaves.
- [Funcionamiento](Funcionamiento.md): flujos de operador/cliente, documentos, cálculos, PDF/XML y correo.
- [Pruebas y pendientes](Pruebas-y-pendientes.md): resultados fechados, límites y comprobaciones del receptor.
- Dos diagramas SVG de conexiones y propiedad de datos, más dos esquemas Mermaid de relaciones físicas en el diccionario.

Se contrastaron rutas y opciones con el código. La comparación de metadatos local del día mantiene como diferencia estructural los objetos de diagramas SSMS; el historial EF y los registros de negocio no se presentan como idénticos.

Verificación: seis manuales, 37 enlaces internos resueltos, 93 entradas de tablas y 950 columnas; las relaciones seleccionadas de los diagramas coinciden con las FK capturadas. Los SVG son XML válido y sus representaciones se inspeccionaron visualmente. No se incluyeron contraseñas ni cadenas de conexión reales.

AGENTS.md, el LEEME de contratos y UI_Funcional.md conservan sus huellas binarias. De los 55 originales inventariados, solo se reescribieron README.md y ARQUITECTURA.md; los otros 53 conservan su contenido.

La compilación Release del servidor y sus referencias terminó con cero advertencias y cero errores. El primer intento restringido falló al iniciar el proceso auxiliar de MSBuild/WebAssembly; al ejecutarse con los permisos del entorno pasó. Se comprobó HTTP 200 del asistente en un proceso temporal con configuración aislada, sin guardar parámetros ni conectar SQL; el proceso se cerró al terminar. El sitio IIS local previamente usado no estaba respondiendo, por lo que no se presenta como una comprobación de la instalación existente.

No se repitieron en esta fase las pruebas funcionales del 5 de noviembre: el manual las identifica como evidencia previa. No se modificó código, esquema SQL o configuración de la instalación, ni se hicieron commits/push.

**Pendiente de fase 3:** respaldar y retirar las copias sustituidas, archivar informes históricos, conservar las referencias internas y reparar sus enlaces. Aún no se eliminaron originales ni se preparó el ZIP documental final.
