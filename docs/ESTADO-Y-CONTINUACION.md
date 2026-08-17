# Estado del proyecto y cómo continuar

Documento de traspaso para una sesión nueva de Claude Code.
Cortado al **17 de agosto de 2026**. Mitad A (plataforma, identidad y catálogos).

---

## 1. Lee esto primero, y solo esto

Para retomar el trabajo bastan **cuatro archivos**:

| Archivo | Para qué | Tamaño aprox. |
|---|---|---|
| `CLAUDE.md` | Reglas no negociables: stack, seguridad, dominio, diseño. Se lee completo, siempre. | 15 KB |
| Este documento | Dónde quedó todo y qué sigue. | — |
| `PROMPT-FASES-A.md` — **solo la sección de la fase que vas a hacer** | El prompt exacto de la fase. Fases 0 a 8 ya están hechas: no las leas. | 31 KB completo |
| `docs/ARRANQUE-LOCAL.md` | Solo si hay que levantar el entorno o la base. | 6 KB |

### Lo que NO hay que leer (gasta tokens sin devolver nada)

- **`docs/UI_Funcional.md`** — es la especificación del sistema de escritorio viejo, de
  varios cientos de KB. Solo se abre para consultar **una** sección puntual cuando el
  prompt de la fase la cita por número (`§9`, `§31`, etc.). Nunca completo, nunca "por
  contexto". Úsalo con `grep`, no con `Read`.
- **`AGENTS.md`** — es una copia de `CLAUDE.md` para otras herramientas. Leer uno de los
  dos, no los dos.
- **`PROMPT-FASES-B.md`** y todo lo de la mitad B — no se toca (ver §5).
- **`Facturacion.Server/Migrations/*`** — 23 archivos, muchos de miles de líneas
  autogeneradas. Nunca se leen ni se editan a mano; se generan con `dotnet ef migrations add`.
  Para saber el esquema, lee las entidades en `Data/Entidades/` y las configuraciones en
  `Data/Configurations/`, que son cortas y están comentadas.
- **`CatalogosSAT/`** — 48 MB de datos del SAT, fuera del repositorio.
- **`Facturacion.Client/wwwroot/css/*`** — solo se abren si la tarea es de estilos.
- **`bin/`, `obj/`, `.vs/`** — nunca.

### Atajos para orientarse sin leer archivos enteros

```bash
# Inventario de endpoints y su política de permiso
grep -rn "Map\(Get\|Post\|Put\|Delete\|Group\)(\"" Facturacion.Server/Modules --include=*.cs

# Contratos congelados entre las dos mitades
ls Facturacion.Shared/Contratos/

# Qué componentes comunes ya existen (antes de crear uno nuevo)
ls Facturacion.Client/Componentes/Comunes/
```

---

## 2. Dónde quedó: fases 0 a 8 construidas, fase 9 pendiente

| Fase | Contenido | Estado |
|---|---|---|
| 0 | Fundación: tres proyectos, `Directory.*.props`, EF Core, Serilog, Problem Details, idempotencia, Data Protection, query filter de empresa | ✅ commiteada |
| 1 | Identidad de punta a punta: JWT en memoria (15 min), refresh en cookie `HttpOnly` con rotación y muerte de familia, gate de arranque, empresa activa como claim, seis políticas de permiso, control de intentos sin sesgo de tiempo | ✅ commiteada |
| 2 | `MainLayout` único, tres temas por variables CSS, PWA, service worker que no cachea `/api/`, guarda de versión, componentes comunes | ✅ commiteada |
| 3 | Catálogos del SAT: importador del `.xls`, catálogos grandes por autocompletado en el servidor, chicos precargados, versionado, pantalla de administración | ✅ commiteada |
| 4 | Empresa, configuración, logo, series, folios con `UPDLOCK` en procedimiento almacenado, CSD cifrado con Data Protection | ✅ commiteada |
| 5 | Clientes con validación CFDI 4.0 (nombre normalizado, CP, régimen, matriz de compatibilidad de `UsoCFDI`, genéricos) | ✅ commiteada |
| 6 | Productos y servicios, importación por CSV con análisis previo | ✅ commiteada |
| 7 | Membresías, paquetes, bolsa de timbres, reservas, compras idempotentes, acreditación por consola | ✅ commiteada |
| 8 | Usuarios, permisos por usuario y por empresa, invitaciones con token de un solo uso y 72 h, perfil propio, correo (SMTP y doble de consola) | ⚠️ **construida pero sin commit** |
| 9 | Tablero, integración, endurecimiento y despliegue | ⛔ **no empezada** |

Último commit: `3241797 Fases 2 a 7: temas, catálogos del SAT, empresa, clientes, productos y timbres`.

### Lo primero que hay que hacer

**Cerrar la fase 8.** Todo su código está en el árbol de trabajo sin commitear —
83 archivos entre modificados y nuevos. Antes de tocar nada:

1. `dotnet build` — sin advertencias nuevas.
2. `dotnet test` — deben pasar todas (48 al cerrar la fase 7; la fase 8 pudo sumar).
3. Las cuatro preguntas de revisión del final de `PROMPT-FASES-A.md`.
4. Commit de la fase 8 **antes** de abrir la fase 9. Mezclar dos fases en un commit
   deja el diff inservible para revisar.

> Nota: en una sesión previa `git` reportó `unable to unlink .git/index.lock` desde el
> entorno Linux del sandbox. Las operaciones de git conviene correrlas desde Windows.

---

## 3. Qué falta — fase 9, la última de la mitad A

El prompt literal está en `PROMPT-FASES-A.md`, sección «Fase 9». Resumen de lo que pide:

**Tablero.** Hoy `Paginas/Inicio.razor` es un marcador: imprime el nombre de la sesión y
los permisos, y nada más. Falta el widget de timbres restantes con aviso por debajo de 50,
el conteo de comprobantes por estatus del periodo, los accesos rápidos y los avisos de
vencimiento de certificado y de membresía.

**Bloqueo conocido:** el conteo por estatus depende de `IResumenDocumentos`, que lo
implementa la mitad B y **todavía no existe**. El contrato ya está congelado en
`Facturacion.Shared/Contratos/IResumenDocumentos.cs`. La instrucción de la fase es
explícita: si no existe la implementación real, **decirlo y no inventar datos**. La parte
de timbres, certificado y membresía sí se puede construir completa, porque es de la mitad A.

**Integración.** Verificar que toda implementación de `Shared/Contratos/` esté registrada,
y escribir una comprobación al arrancar que falle ruidosamente si un doble de prueba se
cuela fuera de `Development`.

**Endurecimiento.** Repaso de ocho puntos (empresaId desde el cliente, cobertura del query
filter, `IgnoreQueryFilters` justificado, políticas en todo endpoint que muta, logs sin
datos sensibles, archivos inaccesibles por ruta adivinada, service worker sin `/api/`, CSP
sin `unsafe-*` más allá de `wasm-unsafe-eval`, y Brotli realmente servido). El resultado se
escribe en **`docs/REPASO-SEGURIDAD.md`**, con lo encontrado, lo corregido y lo pendiente.

**Despliegue.** Crear **`docs/DESPLIEGUE.md`**: variables de configuración, cómo se provee
la llave maestra, carga de catálogos, migración y verificación de Brotli. Sin secretos.

### Después de la fase 9

La mitad A queda cerrada. Lo que resta del MVP es **mitad B** (emisión, motor de impuestos,
timbrado, PAC, XML, PDF, correo de comprobantes, listado de documentos, cancelación,
complemento de pagos) más lo que `CLAUDE.md` §6 deja fuera para la fase 2.

**Fecha límite del MVP: 5 de diciembre de 2026.**

---

## 4. Mapa del código, para no buscar a ciegas

```
Facturacion.Server/
  Data/Entidades/Plataforma/        entidades — el esquema real, léelo aquí
  Data/Configurations/Plataforma/   índices, filtros, precisión decimal
  Data/FiltroDeEmpresa.cs           query filter global por EmpresaId
  Infra/Almacen/                    Data Protection, CSD y logo cifrados fuera de wwwroot
  Infra/Idempotencia/               filtro y buffer de Idempotency-Key
  Infra/Seguridad/                  cabeceras, CSP, control de intentos
  Infra/Tenencia/                   contexto de empresa desde el claim, interceptor de sellado
  Modules/Plataforma/Auth/          tokens, refresh con rotación, políticas, sembrado
  Modules/Plataforma/{Catalogos,Empresas,Folios,Clientes,Productos,Timbres,Usuarios}/
  Modules/Documentos/               vacío a propósito: es el enganche de la mitad B

Facturacion.Client/
  Componentes/Comunes/              revísalo antes de crear cualquier componente
  Layout/                           MainLayout único, selector de empresa, selector de tema
  Paginas/                          una carpeta por área
  Servicios/Plataforma/             clientes HTTP y estado

Facturacion.Shared/
  Comun/                            Rfc, NombreFiscal, Permiso, Resultado, EstatusComprobante
  Contratos/                        frontera congelada entre las dos mitades
  Plataforma/                       DTOs
```

Endpoints: 9 grupos bajo `/api/` — `auth`, `catalogos`, `clientes`, `empresa`, `series`,
`productos`, `timbres`, `perfil`, `usuarios`, más `invitaciones` anónimo.

Pruebas (`tests/Facturacion.Pruebas/`, contra SQL Server real, no en memoria): validación de
RFC, reserva concurrente de folios, aritmética de la bolsa, aislamiento por empresa,
compatibilidad de `UsoCFDI`, idempotencia de compra, reutilización de refresh token,
enmascarado de registro, manejador de autenticación.

---

## 5. Frontera que no se cruza

`REPARTO-EQUIPO.md` §3 y §4. **No se escribe código de la mitad B**: emisión, motor de
impuestos, timbrado, PAC, XML, PDF, correo de comprobantes, listado de documentos,
cancelación, complemento de pagos. Sus archivos se pueden **leer** para entender el
contrato; no se editan. Si una tarea cruza esa frontera: **parar y avisar**, no improvisar.

Tampoco se genera código de fases futuras, ni «de una vez». Al terminar una fase se para y
se espera revisión.

---

## 6. Acceso al sistema

No hay usuarios en el repositorio: los crea el **sembrado de desarrollo**, que solo corre en
`Development` y solo si `Sembrado:Correo` y `Sembrado:Contrasena` están puestos en
`Facturacion.Server/appsettings.Development.json` (archivo gitignoreado).

Las credenciales vigentes en esta máquina están en ese archivo, bajo la sección `Sembrado`.
Es el único usuario que existe, y trae los **seis permisos** y **dos empresas**, así que
después de iniciar sesión la primera pantalla es el selector de empresa.

URL: `https://localhost:7123`

Para cambiar la contraseña del sembrado: edítala en `appsettings.Development.json` y borra
la base (`FacturacionDev`) para que el sembrado vuelva a correr. La política de Identity
exige **12 caracteres o más, con mayúscula, minúscula y dígito**; si no la cumple, el
sembrado no llega a crear al usuario y el inicio de sesión falla sin explicación obvia.

Tras varios intentos fallidos entra el bloqueo por IP, que crece con cada tanda y vive en
memoria: esperar o reiniciar el servidor.

Los demás usuarios se crean **solo por invitación** desde la pantalla de usuarios: el
administrador captura correo y nombre, y el invitado fija su propia contraseña. Nadie
conoce la contraseña de nadie, ni el administrador. Sin `Correo:Servidor` configurado, la
invitación se escribe en el log de la consola en vez de enviarse — de ahí se copia el
enlace en desarrollo.

---

## 7. Prompt sugerido para arrancar la sesión nueva

```
Lee CLAUDE.md completo y docs/ESTADO-Y-CONTINUACION.md. No leas docs/UI_Funcional.md
ni AGENTS.md ni los archivos de Migrations.

La fase 8 está construida pero sin commitear. Empieza por verificarla: dotnet build sin
advertencias nuevas, dotnet test en verde, y las cuatro preguntas de revisión del final
de PROMPT-FASES-A.md. Dime qué encontraste y espera.
```
