# Fases de desarrollo y arquitectura

Vista unificada de cómo se construyó el sistema: qué problema resolvió cada fase, con qué
solución y qué introdujo en la arquitectura. Los documentos originales de cada fase se
conservan (`PROMPT-FASES-A.md`, `PROMPT-FASES-B.md`, `REPORTE_ESTADO_SISTEMA.md`,
`REPARTO-EQUIPO.md`); este archivo es el mapa que los explica en conjunto.

Para las reglas no negociables de seguridad, dominio y diseño, ver **`ARQUITECTURA.md`**.

---

## Visión general

SaaS de facturación electrónica **CFDI 4.0** para México, multiempresa. Reemplaza a un
sistema de escritorio (GenCFDI V4.2). Stack: .NET 10 / C# 14, Blazor WebAssembly (PWA) en el
cliente, ASP.NET Core 10 en el servidor, SQL Server con EF Core 10, MudBlazor sobre variables
CSS propias.

El trabajo se dividió en dos mitades con una frontera de contratos congelada entre ambas:

- **Mitad A — Plataforma:** identidad, empresas, catálogos del SAT, clientes, productos,
  folios y timbres. No depende de nadie.
- **Mitad B — Documentos:** emisión, impuestos, timbrado, PAC, XML, PDF, correo, cancelación
  y complemento de pagos. Consume lo que produce A, nunca al revés.

---

## Mitad A — Plataforma, identidad y catálogos

### Fase 0 — Fundación
- **Problema:** arrancar el proyecto con una frontera clara entre las dos mitades para poder
  trabajar en paralelo sin bloqueos.
- **Solución:** estructura de tres proyectos (Client, Server, Shared) y contratos congelados
  en `Shared/Contratos/`, que definen lo que B necesita de A.
- **Arquitectura:** dependencia unidireccional A → B; los contratos son `record` inmutables.

### Fase 1 — Identidad de extremo a extremo
- **Problema:** autenticar usuarios de forma segura en una aplicación cuyo cliente viaja al
  navegador y es legible.
- **Solución:** JWT de acceso en memoria (15 min) y refresh token en cookie `HttpOnly` con
  rotación en cada uso; al arrancar, un *gate* que resuelve la sesión antes de renderizar.
- **Arquitectura:** ASP.NET Core Identity como almacén y motor de hash; la emisión de JWT es
  propia. La empresa activa viaja como *claim* del token, nunca como parámetro.

### Fases 2 a 7 — Temas, catálogos, empresa, clientes, productos y timbres
- **Problema:** poblar la plataforma con todo lo que la facturación necesita como insumo.
- **Solución:**
  - Tres temas visuales sobre variables CSS semánticas; PWA con service worker.
  - Catálogos del SAT: los grandes (cientos de miles de filas) se consultan contra el
    servidor con autocompletado; solo los chicos y estables se precargan en el cliente.
  - Empresas emisoras, su configuración, series y **folios reservados con procedimiento
    almacenado y bloqueo de renglón** (nunca `SELECT MAX+1`).
  - Certificados CSD cifrados con Data Protection.
  - Clientes con validación CFDI 4.0 (nombre normalizado, régimen, uso compatible).
  - Productos con sus impuestos.
  - Bolsa de timbres y membresías, por empresa.
- **Arquitectura:** aislamiento por empresa con *query filter* global en EF Core, alimentado
  desde el claim; ninguna tabla de empresa se filtra a mano.

### Fase 8 — Usuarios, permisos y perfil
- **Problema:** un despacho necesita varios usuarios por empresa con distintos alcances.
- **Solución:** autorización **por permiso** (no por rol), con seis permisos exactos, cada uno
  como política de ASP.NET Core aplicada al endpoint.
- **Arquitectura:** ocultar un botón nunca protege; el servidor rechaza la operación igual.

### Fase 9 — Tablero, verificación de contratos y endurecimiento
- **Problema:** cerrar la mitad A lista para que B se apoye en una base estable.
- **Solución:** tablero integrado, cabeceras de seguridad, verificación en arranque que impide
  que un doble de prueba llegue a producción, y guía de despliegue.

---

## Mitad B — Documentos, timbrado y salidas

### Fase B0 — Entidades base y dobles de prueba
- **Problema:** B necesita las APIs de A (catálogos, clientes, productos, emisor, folios,
  timbres) para empezar, pero no puede esperar a que estén todas listas.
- **Solución:** entidades del comprobante y sus hijos, migración inicial, e implementaciones
  **dobles** de los contratos de A con datos realistas, registradas solo en Development.
- **Arquitectura:** cuando la implementación real está lista, se cambia una línea de registro.

### Fase B1 — Motor de impuestos
- **Problema:** el cálculo de impuestos CFDI 4.0 es delicado; sumar la tasa sobre el total en
  vez de sumar por concepto produce diferencias de redondeo que el PAC rechaza.
- **Solución:** motor que calcula **por concepto** y suma, con dinero en `decimal(18,6)`.

### Fase B2 — Formulario de emisión y borradores
- **Problema:** capturar una factura completa sin perder el trabajo a medio hacer.
- **Solución:** formulario de emisión con borradores persistentes; un barrido recoge los
  borradores huérfanos que quedan al abrir y cerrar la pantalla sin guardar.

### Fase B3 — Generación del XML CFDI 4.0
- **Problema:** el XML debe validar contra el esquema oficial (XSD) antes de ir al PAC.
- **Solución:** generador del XML y validación contra `cfdv40.xsd` y sus importados; cadena
  original con el XSLT oficial del SAT.

### Fase B4 — Timbrado contra el PAC
- **Problema:** timbrar es una llamada externa que puede fallar a mitad de camino y dejar un
  comprobante en limbo o un folio desperdiciado.
- **Solución:** orquestación en pasos con `Idempotency-Key`, integración con el PAC (SW Sapien
  en sandbox) y una conciliación en segundo plano que rescata los timbrados que nunca
  volvieron. Nunca una llamada HTTP dentro de una transacción de base de datos.

### Fase B5 — PDF con QR y cadena original
- **Problema:** la representación impresa del comprobante para el receptor.
- **Solución:** PDF con logo, código QR y cadena original, generado con QuestPDF.

### Fase B7 — Listado de documentos
- **Problema:** consultar y filtrar los comprobantes emitidos.
- **Solución:** listado con filtros (parcial: pendiente descarga, filtro por cliente y CSV).

### Fase B8 — Cancelación
- **Problema:** cancelar ante el SAT exige un motivo válido y consultar el estatus resultante.
- **Solución:** solicitud de cancelación con motivos 01 a 04 y consulta de estatus ante el SAT.

### Fase B9 — Complemento de pagos 2.0
- **Problema:** una factura a plazo (PPD) obliga a emitir un complemento por cada pago; sin él
  la empresa queda en incumplimiento.
- **Solución:** complemento de pagos 2.0, modelado como un CFDI de tipo P que toma folio, se
  timbra y se cancela como cualquier otro.

---

## Decisiones arquitectónicas clave

- **Inmutabilidad (datos congelados):** al timbrar se copian dentro del comprobante los datos
  fiscales del emisor, del receptor y de cada concepto. Un cambio de domicilio de hoy no puede
  alterar una factura de hace dos años.
- **Aislamiento por empresa:** `EmpresaId` en toda tabla de empresa, filtrado con *query
  filter* global alimentado desde el claim del token.
- **Folios sin condiciones de carrera:** reserva con procedimiento almacenado y bloqueo de
  renglón; si el timbrado falla tras tomar folio, el comprobante queda en `error` con ese
  folio apartado y no se recicla.
- **Dinero exacto:** `decimal(18,6)`, cálculo a seis decimales y presentación a dos; nunca
  `double` ni `float`.
- **Nada se borra:** solo `Activo = false`, salvo un borrador nunca timbrado.
- **Secretos protegidos:** CSD y contraseñas cifrados con Data Protection, nunca en claro ni
  en logs.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| El PAC no timbra en el entorno real | Abordar impuestos → XML → timbrado con rigor temprano y pruebas obligatorias |
| Timbrado que falla a mitad de camino | Conciliación en segundo plano que consulta el PAC y confirma o devuelve |
| Datos de un cliente que migran entre empresas | *Query filter* global + auditoría |
| Folio duplicado por concurrencia | Procedimiento almacenado con bloqueo de renglón + prueba de concurrencia |
| Primera carga pesada de WebAssembly | `PublishTrimmed`, compresión Brotli y carga diferida de ensamblados |
