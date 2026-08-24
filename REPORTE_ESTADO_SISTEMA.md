# REPORTE DE ESTADO — Sistema de Facturación CFDI 4.0
**Generado:** 24 de agosto de 2026 | **Versión:** 1.0

---

## RESUMEN EJECUTIVO

| Aspecto | Estado |
|---|---|
| **Proyecto** | SaaS de facturación electrónica CFDI 4.0 multiempresa |
| **Mitad A (Plataforma)** | ✅ **COMPLETADA** — Fase 9 terminada |
| **Mitad B (Documentos)** | ⏳ **EN DESARROLLO** — Fase B0 en progreso |
| **Plazo MVP** | 5 de diciembre de 2026 (15 semanas restantes) |
| **Decisión actual** | Luis absorbe ambas mitades desde 17 de agosto |
| **Responsable** | Luis (frontera congelada entre mitades, arquitectura desacoplada) |

---

## 1. ESTADO ACTUAL DEL DESARROLLO

### 1.1 Mitad A — COMPLETADA ✅

**Fase 9 cerrada:** Tablero, integración y endurecimiento

Implementado:
- ✅ Fundación, estructura, contratos congelados
- ✅ Autenticación con JWT (access + refresh con rotación)
- ✅ Layout, temas (3: claro, oscuro, sepia), PWA
- ✅ Catálogos del SAT (17 catálogos, búsqueda full-text, precargas)
- ✅ Empresas emisoras, configuración, series, folios (procedimiento almacenado con UPDLOCK)
- ✅ Certificados CSD (cifrados con Data Protection)
- ✅ Clientes (receptores) con validación CFDI 4.0
- ✅ Productos/servicios con impuestos
- ✅ Membresías, paquetes, bolsa de timbres (por empresa)
- ✅ Usuarios, permisos (6 permisos exactos), invitaciones
- ✅ Tablero integrado
- ✅ Repaso de seguridad documentado

**Tecnología:**
- .NET 10, C# 14, Blazor WebAssembly (PWA)
- ASP.NET Core 10, SQL Server, EF Core 10
- MudBlazor sobre CSS semánticas
- Serilog + Sentry (datos sensibles enmascarados)

---

### 1.2 Mitad B — EN DESARROLLO ⏳

**Estado:** Fase B0 en curso (dobles de prueba y esqueleto)

**Pendiente en esta fase:**
- Esqueleto del módulo `DocumentosModule.cs` (estructura, no negocio)
- Dobles de prueba para: `IServicioCatalogosSat`, `IServicioClientes`, `IServicioProductos`, `IServicioEmpresaEmisora`, `IServicioFolios`, `IServicioTimbres`
- Implementación real de `IResumenDocumentos` (aunque devuelva ceros)
- Entidades base: `Comprobante`, `Concepto`, `ImpuestoConcepto`, `ComprobanteRelacionado`, `IntentoTimbrado`, `SolicitudCancelacion`
- Migración inicial: `20260824_B_DocumentosBase`

**Dependencias de la Mitad A que están listas:**
- Catálogos SAT (Fase 3 ✅)
- Clientes (Fase 5 ✅)
- Productos (Fase 6 ✅)
- Empresas, series, folios (Fase 4 ✅)
- Certificados CSD (Fase 4 ✅)
- Timbres (Fase 7 ✅)

---

## 2. RUTA CRÍTICA — FASES PENDIENTES DE B

### 2.1 Orden de ejecución (si absorbes la Mitad B)

Según `PROMPT-FASES-B.md` §6 "Si Luis absorbe esta mitad":

```
B1 → B3 → B4 → B5 → B2 → B7
(recortes opcionales: B9→fase2, B8→manual, B6→manual)
```

| Fase | Contenido | Modelo | Duración | Bloquea | Desbloquea |
|---|---|---|---|---|---|
| **B0** ⏳ | Dobles, esqueleto, entidades base | Sonnet | 2-3 días | B1 | (comienza B1) |
| **B1** | Motor impuestos (Opus) | Opus | 30 h | B3 | (puro cálculo, no web) |
| **B3** | Generación XML CFDI 4.0 | Opus | 40 h | B4 | B4 listo |
| **B4** | PAC, timbrado 3 pasos, idempotencia | Opus | 45 h | B5,B7 | Sandbox PAC |
| **B5** | PDF con QR y cadena original | Sonnet | 35 h | B6 | (representación impresa) |
| **B2** | Formulario emisión, borradores | Sonnet | 50 h | (ninguno) | Capturistas pueden trabajar |
| **B7** | Listado documentos, filtros | Sonnet | 30 h | (ninguno) | Dashboard completo |
| **B9** | Complemento de pagos (Opus) | Opus | 45 h | B4 | MVP completo |
| **B8** | Cancelación y consulta SAT | Opus | 40 h | B4 | Ciclo de vida completo |
| **B6** | Envío por correo | Sonnet | 25 h | B5 | (automatización) |

**Total:** ~340 horas de codificación + integración.

### 2.2 Hitos críticos

- **15 de octubre:** Punto de control de contingencia (PAC en sandbox debe estar timbrado 50 veces)
- **21 de noviembre:** Congelamiento de funcionalidad (solo corrección de bugs)
- **5 de diciembre:** Entrega MVP

---

## 3. DECISIONES YA CERRADAS (NO NEGOCIABLES)

### Scope del MVP
✅ Multiempresa · catálogos SAT · clientes · productos · emisión estándar · impuestos · timbrado · cancelación 01–04 · PDF · correo · **pagos** · timbres · membresías

❌ Fuera del MVP: Carta Porte, Comercio Exterior, Notaría, INE, Constructoras, Cotizaciones, Addendas, Inventario

### Seguridad (no negociable)
- JWT en memoria (15 min), refresh token en cookie HttpOnly con rotación
- Empresa como claim, nunca parámetro
- EmpresaId en todo dato empresarial, query filter global
- Certificados CSD cifrados, nunca en log
- Idempotency-Key obligatorio en POST que cobra/timbra
- Ninguna transacción BDD con llamadas HTTP

### Dominio (no negociable)
- Folios: procedimiento almacenado con UPDLOCK, nunca SELECT MAX+1
- Dinero: `decimal(18,6)` (cálculo 6 decimales, presentación 2)
- Datos congelados: al timbrar se copian nombre, RFC, régimen, CP de receptor
- Estatus comprobante: solo `borrador`, `timbrando`, `timbrado`, `error`, `cancelado`, `en_cancelacion`
- Nada se borra: solo `Activo = false` (excepto borradores)
- Bitácora: toda operación fiscal deja registro

---

## 4. FRONTERAS ARQUITECTÓNICAS (CONGELADAS)

### Shared/Contratos/ — Define la línea de separación

**Lo que B necesita de A:**
```csharp
IContextoEmpresa              // claim del usuario
IServicioCatalogosSat         // búsqueda y resolución de claves
IServicioClientes             // receptor fiscal para timbrado
IServicioProductos            // concepto fiscal para timbrado
IServicioEmpresaEmisora       // emisor fiscal + CSD descifrado
IServicioFolios               // reserva de folio
IServicioTimbres              // reserva, confirmación, devolución
```

**Lo que A necesita de B:**
```csharp
IResumenDocumentos            // tablero: documentos por estatus
```

**Regla:** Cambiar un contrato requiere acuerdo explícito + commit dedicado solo con ese cambio.

---

## 5. ESTRUCTURA DE CARPETAS (ESTADO ACTUAL)

```
Facturacion.Server/
  Modules/
    Plataforma/               ← COMPLETADA
      Auth/         (login, refresh, cambiar empresa)
      Empresas/     (alta de empresa, configuración)
      Usuarios/     (usuarios, permisos, invitación)
      Catalogos/    (carga y búsqueda de catálogos SAT)
      Clientes/     (receptores, validación CFDI)
      Productos/    (servicios, impuestos)
      Folios/       (series, procedimiento reserva)
      Timbres/      (bolsa, movimientos, reserva)
    Documentos/     ← EN CONSTRUCCIÓN
      Emision/      (vacío — será fase B2)
      Impuestos/    (vacío — será fase B1)
      Timbrado/     (vacío — será fase B4)
      Pac/          (vacío — será fase B4)
      Salidas/      (vacío — será B5, B6)
      Cancelacion/  (vacío — será B8)
      Pagos/        (vacío — será B9)

Facturacion.Client/
  Layout/           ← COMPLETADA
  Theme/            ← COMPLETADA (3 temas, CSS semánticas)
  Componentes/
    Comunes/        ← COMPLETADA (TablaDatos, BuscadorCatalogo, CampoRfc, etc.)
    Documentos/     ← VACÍO (será B2, B5, B7)
  Paginas/
    Cuenta/         ← COMPLETADA
    Empresas/       ← COMPLETADA
    Usuarios/       ← COMPLETADA
    Clientes/       ← COMPLETADA
    Productos/      ← COMPLETADA
    Timbres/        ← COMPLETADA
    Tablero/        ← COMPLETADA (integrado con IResumenDocumentos)
    Facturas/       ← VACÍO (será B2)
    Pagos/          ← VACÍO (será B9)
    Cancelaciones/  ← VACÍO (será B8)
```

---

## 6. PRÓXIMOS PASOS — ACCIÓN INMEDIATA

### Fase B0 — Dobles de prueba (2-3 días)

**Deliverables:**
1. `Server/Modules/Documentos/DocumentosModule.cs` (estructura solo)
2. Dobles en `Server/Modules/Documentos/Dobles/`:
   - `DobleServicioCatalogosSat` (RFC/claves reales del SAT)
   - `DobleServicioClientes` (cliente genérico XAXX010101000)
   - `DobleServicioProductos` (3–5 productos reales)
   - `DobleServicioEmpresaEmisora` (datos falsos pero válidos)
   - `DobleServicioFolios` (contador secuencial)
   - `DobleServicioTimbres` (saldo ficticio)
3. Entidades:
   - `Comprobante` (copias congeladas de fiscal de emisor/receptor)
   - `Concepto`, `ImpuestoConcepto`
   - `ComprobanteRelacionado`, `IntentoTimbrado`, `SolicitudCancelacion`
4. Migración `20260824_B_DocumentosBase`

**Criterio de aceptación:**
- Compila sin advertencias nuevas
- Dobles devuelven datos realistas (RFC válidos, claves SAT reales)
- `IResumenDocumentos` existe pero devuelve ceros
- Tablero sigue funcionando
- Sin tocar archivos de Mitad A

---

## 7. QUÉ LEER / NO LEER (optimización de tokens)

### ✅ OBLIGATORIO LEER (antes de continuar)
- **`ARQUITECTURA.md`** (completo) — reglas de seguridad, dominio y diseño, NO negociables
- **`REPARTO-EQUIPO.md`** (completo) — frontera entre mitades, estructura de carpetas
- **`PROMPT-FASES-B.md`** (§1–§6) — orden de fases, plan de contingencia
- **Esta sección actual** — estado actual, próximos pasos

**Total:** ~4,000 palabras. Conviene leerlas una sola vez al inicio de cada sesión de trabajo.

### ⏭️ LEER CUANDO LLEGUES A ESA FASE
- `PROMPT-FASES-B.md` §B1 prompt — solo cuando inicies Fase B1
- `PROMPT-FASES-B.md` §B2 prompt — solo para Fase B2
- (Idem para B3–B9)

### ❌ NO NECESITAS LEER (consumirían tokens sin valor)
- `PROMPT-FASES-A.md` (completo) — Mitad A ya está hecha, es historia
  - Puedes leer §0 (calendario) si necesitas entender el histórico
- `AGENTS.md` — es copia de `ARQUITECTURA.md`, redundante
- `docs/UI_Funcional.md` — inventario del sistema viejo, se usa para consultas específicas, no lectura lineal
- `Sistema anterior/` — carpeta del legacy, no lo toques
- configuración local del entorno — solo si tienes problemas

---

## 8. SETUP RECOMENDADO PARA PRÓXIMA SESIÓN

```markdown
# Para continuar en una próxima sesión:

Lee ESTOS primero (4,000 palabras):
- ARQUITECTURA.md (completo)
- REPARTO-EQUIPO.md (completo)
- Este archivo: REPORTE_ESTADO_SISTEMA.md

Luego pega en el prompt:

---

Lee ARQUITECTURA.md y REPARTO-EQUIPO.md completos.

**Estado actual:** Mitad A completada, Mitad B fase B0 en progreso.

**Próximo paso:** Fase B0 — dobles de prueba y esqueleto del módulo.

Lee el reporte de estado en REPORTE_ESTADO_SISTEMA.md §6 (Próximos pasos).

Construye únicamente:

1. Server/Modules/Documentos/DocumentosModule.cs con estructura AddDocumentos / MapDocumentos

2. Dobles de prueba en Server/Modules/Documentos/Dobles/:
   - IServicioCatalogosSat
   - IServicioClientes
   - IServicioProductos
   - IServicioEmpresaEmisora
   - IServicioFolios
   - IServicioTimbres
   Datos REALISTAS: RFC válidos, claves SAT reales, CSD del SAT.

3. Entidades de documentos en Server/Data/Configurations/Documentos/:
   - Comprobante
   - Concepto
   - ImpuestoConcepto
   - ComprobanteRelacionado
   - IntentoTimbrado
   - SolicitudCancelacion

4. Implementación real de IResumenDocumentos (aunque devuelva ceros)

5. Migración: 20260824_B_DocumentosBase

NO toques ningún archivo de Plataforma. Si necesitas un cambio ahí, dímelo.

Al terminar: qué construiste, qué decisiones tomaste, qué contrato te quedó corto.
```

---

## 9. RIESGOS IDENTIFICADOS

| Riesgo | Severidad | Mitigation |
|---|---|---|
| 15 octubre: PAC no timbra en sandbox | 🔴 Alta | Abordar B1 → B3 → B4 con rigor temprano; tests obligatorios |
| Tokens JWT mal rotados en sesión concurrente | 🔴 Alta | Test unitario ya existe, verificar en B4 |
| Datos de cliente migran entre empresas | 🔴 Alta | Query filter global + test + auditoría de datos |
| Folio duplicado por race condition | 🔴 Alta | Procedimiento almacenado con UPDLOCK, test de concurrencia existe |
| CSD cifrado corruptible en despliegue | 🟠 Media | Data Protection key maestra en appsettings, doc en §4 |
| XML rechazado por validación XSD | 🟠 Media | Serializador debe validar antes de PAC |
| WebAssembly lento (>5MB descarga) | 🟠 Media | PublishTrimmed, Brotli, ensamblados lazy-loaded |

---

## 10. MÉTRICAS DE ÉXITO

- ✅ **B0:** Compila, dobles devuelven datos realistas, ningún archivo de A tocado
- ✅ **B1:** Motor impuestos probado contra 20 casos, sum-per-concepto ≠ tax-on-total manejado
- ✅ **B3:** XML valida contra XSD oficial, cadena original con XSLT SAT correcta
- ✅ **B4:** Sandbox PAC: 50 timbrados consecutivos sin error
- ✅ **B5:** PDF genera con logo, QR, cadena original y sello SAT
- ✅ **Antes del 21 nov:** Todo funciona, congeladas nuevas features
- ✅ **5 dic:** Entrega a cliente

---

## 11. CONTACTO RÁPIDO — DUDAS FRECUENTES

**P: ¿Puedo agregar un campo al cliente?**
R: No sin cambiar el contrato `IServicioClientes`. Si lo necesitas, dímelo en lugar de hacerlo.

**P: ¿Por qué no cacheo el catálogo completo en el cliente?**
R: `c_ClaveProdServ` tiene ~100k filas. Harías WASM +30MB. Se busca contra servidor con debounce 300ms, índice full-text SQL Server.

**P: ¿Qué pasa si el PAC falla entre ReservarTimbre y ConfirmarTimbre?**
R: Queda en `timbrando`. Proceso en segundo plano busca en PAC cada 30 min. Si PAC dice "ya timbrado", confirma. Si "no existe", devuelve.

**P: ¿Puedo usar `localhost` para el CSD?**
R: No. Certificados cifrados con Data Protection, clave maestra en configuración. Nunca en código.

**P: ¿Por qué no envío RFC desde el navegador?**
R: Validación en client = retroalimentación rápida. La que cuenta es la del servidor. WebAssembly viaja legible al navegador.

---

## 12. ARCHIVOS GENERADOS POR ESTA SESIÓN

- `REPORTE_ESTADO_SISTEMA.md` (este archivo)
  - Úsalo de referencia en la próxima sesión
  - Comparte con compañeros de equipo que hereden trabajo
  - Actualiza cuando cierres una fase (§1.2 + hitos)

---

**Fin del reporte.**

---

*Próxima revisión: al cerrar Fase B0*
