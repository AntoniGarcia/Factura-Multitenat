# Estado del proyecto y cómo continuar

Traspaso de contexto para una sesión nueva de Claude Code.
**Corte: 17 de agosto de 2026, rama `develop`.**

Este documento reemplaza cualquier versión anterior. Si encuentras otro documento de
traspaso, está viejo.

---

## 1. Respuesta corta: ¿en qué fase se quedó?

**La mitad A está terminada y commiteada: fases 0 a 9, completas.**

**La mitad B está en curso y sin commitear.** De sus diez fases:

| | |
|---|---|
| **Hechas** (sin commit) | **B0** esqueleto y entidades · **B1** motor de impuestos · **B3** XML CFDI 4.0 · **B5** PDF con QR |
| **A medias** | **B4** timbrado — todo el andamiaje está, **falta el proveedor de PAC concreto** |
| **Sin empezar** | **B2** formulario de emisión · **B6** correo · **B7** listado · **B8** cancelación · **B9** pagos |

El orden que se está siguiendo no es el numérico: es el de `PROMPT-FASES-B.md`, sección
«Si Luis absorbe esta mitad» → **B1 → B3 → B4 → B5 → B2 → B7**. Por eso B2 sigue vacía
mientras B5 ya está hecha. Va en **B4, atorada en la elección del PAC**.

**Lo primero que hay que hacer: commitear.** Hay 99 archivos pendientes en el árbol de
trabajo, incluidos `CLAUDE.md`, `REPARTO-EQUIPO.md` y la migración `B_DocumentosBase`.
Conviene partirlo en commits por fase (B0, B1, B3, B4-parcial, B5); mezclarlas en uno
solo deja el diff inservible para revisar.

> `git` falla desde el entorno Linux del sandbox con
> `unable to unlink .git/index.lock`. Los commits hay que hacerlos desde Windows.

---

## 2. Qué NO leer — esto es lo que gasta tokens sin devolver nada

| No leer | Por qué |
|---|---|
| **`docs/UI_Funcional.md`** — 41 KB, 1 133 líneas | Es la especificación del sistema de escritorio viejo. Solo se consulta **una sección puntual** cuando el prompt de la fase la cita por número (`§12`, `§27`, `§1.4`). **Usa `grep -n "^## §27" docs/UI_Funcional.md` y lee solo ese bloque.** Nunca completo, nunca «para tener contexto». |
| **`AGENTS.md`** — 17 KB | Es copia literal de `CLAUDE.md` para otras herramientas. Leer uno de los dos, jamás los dos. |
| **`Facturacion.Server/Migrations/*`** — 1 MB, 27 archivos | Autogenerado. `AppDbContextModelSnapshot.cs` solo son 2 722 líneas. No se leen ni se editan a mano: se generan con `dotnet ef migrations add`. Para el esquema, lee `Data/Entidades/` y `Data/Configurations/`, que son cortos y están comentados. |
| **`PROMPT-FASES-A.md`** — 31 KB | La mitad A ya está terminada. Solo sirve si hay que auditar por qué algo quedó así. |
| **`PROMPT-FASES-B.md`** — leer **solo la fase en curso** | El resto son fases futuras, y `CLAUDE.md` §10 prohíbe adelantarlas. |
| **`CatalogosSAT/`** — 48 MB | Datos del SAT, fuera del repositorio. |
| **`Sistema de Facturacion/`** | Bóveda de Obsidian que se coló en la carpeta. **No es código.** Debería ir al `.gitignore`. |
| **`Facturacion.Client/wwwroot/css/*`** | Solo si la tarea es de estilos. |
| **`bin/`, `obj/`, `.vs/`, `.git/`** | Nunca. |
| **`tests/**/*Pruebas.cs`** | Se corren con `dotnet test`; no se leen salvo que una falle y haya que entender por qué. |

### Lo que sí hay que leer, y nada más

1. **`CLAUDE.md`** — completo, siempre, antes de escribir una línea. Es la única lectura no negociable.
2. **Este documento.**
3. **La sección de la fase en curso** de `PROMPT-FASES-B.md`.
4. **Los archivos que la tarea toca**, localizados con `grep`, no explorando carpetas.

### Cómo orientarse sin abrir archivos

```bash
# Inventario de endpoints con su política de permiso
grep -rn "Map\(Get\|Post\|Put\|Delete\|Group\)(\"" Facturacion.Server/Modules --include=*.cs

# La frontera congelada entre las dos mitades
ls Facturacion.Shared/Contratos/

# Componentes comunes que ya existen — revísalo ANTES de crear uno nuevo (CLAUDE.md §10)
ls Facturacion.Client/Componentes/Comunes/

# Qué hay construido de la mitad B
find Facturacion.Server/Modules/Documentos -name "*.cs"
```

---

## 3. Detalle de lo construido en la mitad B

Todo vive en `Facturacion.Server/Modules/Documentos/` y `Data/{Entidades,Configurations}/Documentos/`.

**B0 — base.** Seis entidades: `Comprobante`, `Concepto`, `ImpuestoConcepto`,
`ComprobanteRelacionado`, `IntentoTimbrado`, `SolicitudCancelacion`. El `Comprobante` lleva
**copias congeladas** de los datos fiscales del emisor y del receptor (`EmisorRfc`,
`ReceptorNombre`, `ReceptorRegimenFiscal`, `ReceptorDomicilioFiscal`…), no llaves foráneas:
reimprimir una factura de hace dos años tiene que dar el mismo papel. Migración
`20260817211601_B_DocumentosBase`. `ResumenDocumentos` es la implementación **real** del
único contrato que va de B hacia A, y ya alimenta el tablero.

**B1 — motor de impuestos.** `Impuestos/MotorDeImpuestos.cs` y `CalculoDeImpuestos.cs`.
Clase pura, sin EF ni HTTP. **32 pruebas** en `MotorDeImpuestosPruebas.cs`, por encima de las
20 que pedía el prompt.

**B3 — XML.** `Salidas/GeneradorDeXmlCfdi.cs`, `ServicioDeXmlCfdi.cs` y `EsquemasSat.cs`.
El XSD y el XSLT del SAT se compilan **una sola vez** (`EsquemasSat` es singleton: compilarlos
cuesta cientos de milisegundos). Validación contra el esquema antes de mandar nada. Sellado
en `CfdiSellado`. Pruebas en `GeneracionDeXmlCfdiPruebas.cs` y `EsquemasSatPruebas.cs`.

**B4 — timbrado, parcial.** `Timbrado/ServicioDeTimbrado.cs` implementa los **tres pasos**:
transacción corta que aparta folio y timbre → llamada al PAC **fuera de toda transacción** →
transacción corta que confirma o revierte. `CierreDeTimbrado.cs` resuelve el cierre y
`ConciliacionDeTimbrados.cs` es un `HostedService` que rescata los comprobantes que quedaron
en `timbrando` tras un corte de red.

**Aquí está el bloqueo.** `Pac/IProveedorPac.cs` define la frontera —`TimbrarAsync`,
`ConsultarAsync`, `RespuestaDePac` con los cuatro resultados posibles— pero **no hay ninguna
implementación**. `ConciliacionDeTimbrados` ya lo contempla: usa `GetService<IProveedorPac>()`
y no hace nada si no está registrado. La interfaz existe precisamente porque **la elección
del PAC todavía no está tomada**, y todo lo caro del módulo es independiente de cuál sea.

**B5 — PDF.** `Salidas/GeneradorDePdfCfdi.cs` con QuestPDF (licencia Community, declarada en
`DocumentosModule`) y QRCoder. Campos según §1.4 del documento funcional, QR, cadena original
del complemento de certificación y los dos sellos. Todo sale del comprobante congelado: ni una
consulta al catálogo. Pruebas en `RepresentacionImpresaPruebas.cs`.

**Lo que falta y está vacío:** `MapDocumentos` no mapea **ningún endpoint** todavía. Las
carpetas `Client/Paginas/{Facturas,Pagos,Cancelaciones}` y `Client/Servicios/Documentos`
existen pero están vacías. No hay una sola pantalla de la mitad B.

---

## 4. Lo siguiente, en orden

1. **Commitear las fases B0, B1, B3, B4-parcial y B5**, separadas.
2. **Decidir el PAC** (Finkok, SW sapien, Facturama…) e implementar `IProveedorPac` contra su
   **sandbox**. Regla del plan: producción solo cuando el sandbox pase **50 timbrados
   seguidos**.
3. **Cerrar B4** de punta a punta: endpoint de timbrado con `Idempotency-Key` obligatorio, y
   reintentos con espera creciente.
4. **B2 — formulario de emisión**, que es la fase grande que falta: cabecera, rejilla editable
   de conceptos, totales en vivo del motor de B1, CFDI relacionados, información global solo
   con `XAXX010101000`, y borradores que no consumen folio ni timbre. Optimizado para teclado:
   un capturista de diez renglones no debe tocar el ratón.
5. **B7 — listado de documentos** con filtros, `Virtualize` y descarga autorizada de XML y PDF.

Después, según alcance: **B6** correo, **B8** cancelación, **B9** pagos. El plan de absorción
contemplaba recortarlas —B9 bloqueando `PPD`, B8 a cancelación manual desde el portal del SAT,
B6 a descarga manual—, pero ese recorte estaba calculado para absorber el 15 de octubre con
siete semanas. **La absorción se adelantó al 17 de agosto, hay ~15 semanas, y los recortes no
están decididos.** No los des por hechos: pregunta.

**Fecha límite del MVP: 5 de diciembre de 2026.**

---

## 5. Reglas de trabajo que Claude Code debe respetar

Están en `CLAUDE.md` §10, pero son las que más se incumplen:

- **Por fases, con parada y revisión entre cada una.** Al terminar una fase te detienes, dices
  qué construiste y qué quedó pendiente, y **esperas**. No empiezas la siguiente sin que te lo
  pidan.
- **No generes código de fases futuras.** Ni «de una vez», ni «para dejarlo listo».
- **Si algo de lo que se pide tiene un problema, dilo antes de construirlo.** Vale más una
  objeción de tres líneas que una corrección de tres horas.
- **Nada de `TODO` ni de métodos que devuelven datos falsos** en código que se sube. Hoy el
  repositorio está limpio de ambos: mantenlo así.
- **`Facturacion.Shared/Contratos/` está congelada.** Cambiar una firma o un DTO de ahí exige
  acuerdo explícito y un commit dedicado sin ningún otro cambio. Que ahora las dos mitades
  sean del mismo autor no la afloja.
- **La mitad B consume lo que produce la A, nunca al revés.** Si Documentos necesita un cambio
  en Plataforma, el contrato quedó corto: **avisar**, no cruzar la frontera por dentro.
- **Nada de dobles de prueba de los contratos de la mitad A.** Ya tienen implementación real, y
  `VerificacionDeContratos` tumba el arranque si un doble llega a producción.
- **Al cerrar cada fase:** `dotnet build` sin advertencias nuevas y la aplicación corriendo.
  Una fase que no compila no está terminada.

Tres reglas específicas de esta mitad, de `PROMPT-FASES-B.md`, que cuestan caro al romperse:

1. **Nunca una llamada HTTP dentro de una transacción de base de datos.**
2. **El timbrado son tres pasos, no uno.**
3. **Al timbrar se congelan los datos** dentro del comprobante.

---

## 6. Mapa del código

```
Facturacion.Server/
  Data/Entidades/{Plataforma,Documentos}/   el esquema real — léelo aquí, no en Migrations
  Data/Configurations/{Plataforma,Documentos}/  índices, filtros, precisión decimal
  Data/FiltroDeEmpresa.cs                   query filter global por EmpresaId
  Infra/Almacen/                            Data Protection: CSD y logo cifrados fuera de wwwroot
  Infra/Idempotencia/                       filtro y buffer de Idempotency-Key
  Infra/Seguridad/                          cabeceras, CSP, control de intentos
  Infra/Tenencia/                           empresa desde el claim, sellado, HusoDeEmpresa
  Modules/Plataforma/                       mitad A — terminada
    Auth/ Catalogos/ Empresas/ Folios/ Clientes/ Productos/ Timbres/ Usuarios/ Tablero/
  Modules/Documentos/                       mitad B — en curso
    Impuestos/ Salidas/ Timbrado/ Pac/ ResumenDocumentos.cs

Facturacion.Client/
  Componentes/Comunes/    17 componentes — revísalo antes de crear cualquiera
  Layout/                 MainLayout único, selector de empresa, selector de tema
  Paginas/                Tablero, Cuenta, Empresas, Usuarios, Clientes, Productos, Timbres, Admin
                          Facturas/, Pagos/, Cancelaciones/ ← vacías, son de la mitad B
  Servicios/{Plataforma,Documentos}/        Documentos/ está vacía

Facturacion.Shared/
  Comun/        Rfc, NombreFiscal, Permiso, Resultado, EstatusComprobante, DobleDePrueba
  Contratos/    frontera congelada — 9 interfaces
  Plataforma/   DTOs
```

**Endpoints:** diez grupos bajo `/api/` — `auth`, `catalogos`, `clientes`, `empresa`, `series`,
`productos`, `timbres`, `perfil`, `usuarios`, más `invitaciones` anónimo. **Ninguno de
documentos todavía.**

**Pruebas:** 88 en `tests/Facturacion.Pruebas/`, contra SQL Server real y no en memoria, porque
lo que prueban son bloqueos de renglón e índices únicos. Las de la mitad A cubren RFC, folios
concurrentes, bolsa de timbres, aislamiento por empresa, `UsoCFDI`, idempotencia, refresh
tokens y enmascarado de logs; las de la B cubren motor de impuestos (32), XML (13),
representación impresa (8) y esquemas del SAT.

**Documentos de la fase 9 que ya existen:** `docs/REPASO-SEGURIDAD.md` (16 KB, hallazgos del
endurecimiento) y `docs/DESPLIEGUE.md` (12 KB). Consúltalos solo si la tarea es de seguridad o
de despliegue.

---

## 7. Entorno y acceso

Levantar el entorno: **`docs/ARRANQUE-LOCAL.md`** (7 KB, la única guía). Resumen: copiar
`appsettings.Development.json.ejemplo`, generar la llave maestra, `dotnet ef database update`,
cargar los catálogos del SAT desde el `.xls`, y `dotnet run --project Facturacion.Server`.

No hay usuarios en el repositorio: los crea el **sembrado de desarrollo**, que solo corre en
`Development` y solo si `Sembrado:Correo` y `Sembrado:Contrasena` están puestos en
`appsettings.Development.json` (gitignoreado). Las credenciales vigentes de esta máquina están
ahí, en la sección `Sembrado`. Es el único usuario, y trae los **seis permisos** y **dos
empresas**, así que la primera pantalla tras iniciar sesión es el selector de empresa.

URL: `https://localhost:7123`. El `Server` sirve la API y los estáticos del `Client`: un solo
origen, un solo proceso.

**Si el inicio de sesión falla:** la contraseña del sembrado necesita 12 caracteres o más con
mayúscula, minúscula y dígito; si no cumple, Identity la rechaza y el sembrado nunca llega a
crear al usuario. Tras varios intentos entra el bloqueo por IP, que crece con cada tanda y vive
en memoria: esperar o reiniciar. **Sin catálogos del SAT cargados, toda validación fiscal
rechaza**, y el síntoma es una clave válida que no se deja guardar.

Los demás usuarios se crean **solo por invitación**. Sin `Correo:Servidor` configurado, la
invitación se escribe en el log de la consola: de ahí se copia el enlace en desarrollo.

---

## 8. Prompt para arrancar la sesión nueva

```
Lee CLAUDE.md completo y docs/ESTADO-Y-CONTINUACION.md.

NO leas: docs/UI_Funcional.md (solo secciones puntuales con grep cuando una fase las
cite), AGENTS.md (es copia de CLAUDE.md), Facturacion.Server/Migrations/*,
PROMPT-FASES-A.md, ni la carpeta "Sistema de Facturacion" (es una bóveda de Obsidian,
no es código).

Trabajo las dos mitades. La A está cerrada. De la B están hechas B0, B1, B3 y B5, y B4
está a medias: falta el proveedor de PAC concreto.

Hay 99 archivos sin commitear. Empieza por ahí: propón cómo partirlos en commits por
fase, corre dotnet build y dotnet test, y dime qué encontraste. Luego espera.
```
