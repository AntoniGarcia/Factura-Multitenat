# Estado del proyecto y cómo continuar

Traspaso de contexto para retomar el proyecto en una sesión nueva.
**Corte: 19 de agosto de 2026, rama `develop`.**

Este documento reemplaza cualquier versión anterior. Si encuentras otro documento de
traspaso, está viejo.

Va acompañado de `docs/AUDITORIA-2026-08-19.md`, que es la lista de defectos, huecos de
campo y limpieza pendiente. **Este documento dice dónde estamos; el otro dice qué está
mal.** Léelos en ese orden.

---

## 1. Respuesta corta: ¿en qué fase se quedó?

**Mitad A: terminada y commiteada.** Fases 0 a 9.

**Mitad B: nueve de diez fases construidas. Cinco commiteadas, cuatro no.**

| | |
|---|---|
| **Commiteadas** | **B0** entidades · **B1** motor de impuestos · **B3** XML CFDI 4.0 · **B4** timbrado + PAC SW Sapien (sandbox) · **B5** PDF con QR |
| **Construidas SIN commitear** | **B2** formulario de emisión · **B7** listado de documentos · **B8** cancelación y estatus SAT · **B9** complemento de pagos |
| **Sin empezar** | **B6** envío por correo |

Último commit: `e1f7436 B4b: Timbrado contra PAC (SW Sapien sandbox)`, 18 ago 2026.

**Hay 111 archivos en el árbol de trabajo sin commitear**, 30 de ellos nuevos. Lo primero
es commitear, partido por fase (B2, B7, B8, B9), no en un solo bulto.

> `git` falla desde el entorno Linux del sandbox con `unable to unlink .git/index.lock`.
> Los commits hay que hacerlos desde Windows.

**Aviso importante: «construida» no es «terminada».** B2, B7, B8 y B9 compilan y tienen
pantalla, pero les faltan piezas del alcance acordado y arrastran cuatro defectos, dos de
ellos de datos. Están en `docs/AUDITORIA-2026-08-19.md` §A y §B. **No las des por cerradas
sin leer eso.**

---

## 2. Qué NO leer — esto es lo que gasta tokens sin devolver nada

| No leer | Por qué |
|---|---|
| **`docs/UI_Funcional.md`** — 41 KB, 1 133 líneas | Especificación del sistema de escritorio viejo. Solo se consulta **una sección puntual** cuando un prompt la cita por número. **Usa `grep -n "^## §27" docs/UI_Funcional.md` y lee solo ese bloque.** Nunca completo, nunca «para tener contexto». |
| **`AGENTS.md`** — 17 KB | Copia literal de `ARQUITECTURA.md` salvo la primera línea. Leer uno de los dos, jamás los dos. |
| **`Sistema anterior/`** — 31 capturas, 2.2 MB | Ya están inventariadas en `docs/AUDITORIA-2026-08-19.md` §B. **No abras las imágenes**: cada una cuesta más que la tabla que las resume. Solo si una tarea pide un detalle visual que la tabla no cubre, y entonces esa captura, no la carpeta. Las 10, 11, 15 a 26 son de módulos fuera del MVP (carta porte, constructoras, comercio exterior, notaría): no se abren nunca. |
| **`Facturacion.Server/Migrations/*`** — 1 MB, 33 archivos | Autogenerado. `AppDbContextModelSnapshot.cs` solo son ~2 700 líneas. No se leen ni se editan a mano: se generan con `dotnet ef migrations add`. Para el esquema, lee `Data/Entidades/` y `Data/Configurations/`, que son cortos y están comentados. |
| **`PROMPT-FASES-A.md`** — 31 KB | La mitad A está cerrada. Solo sirve para auditar por qué algo quedó así. |
| **`PROMPT-FASES-B.md`** — leer **solo la fase en curso** | El resto son fases futuras, y `ARQUITECTURA.md` §10 prohíbe adelantarlas. |
| **`CatalogosSAT/`** — 56 MB | Datos y esquemas del SAT, fuera del repositorio (gitignoreados). Binarios: no hay nada que leer. |
| **`Sistema de Facturacion/`** | Bóveda de Obsidian vacía que se coló en la carpeta. **No es código.** Está marcada para borrarse. |
| **`Facturacion.Client/wwwroot/css/*`** — 1 083 líneas | Solo si la tarea es de estilos. |
| **`tests/**/*Pruebas.cs`** | Se corren con `dotnet test`; no se leen salvo que una falle. |
| **`bin/`, `obj/`, `.vs/`, `.git/`, `almacenamiento/`** | Nunca. `almacenamiento/` además tiene material criptográfico. |

### Lo que sí hay que leer, y nada más

1. **`ARQUITECTURA.md`** — completo, siempre, antes de escribir una línea. Es la única lectura no negociable.
2. **Este documento.**
3. **`docs/AUDITORIA-2026-08-19.md`** — completo la primera vez; después, solo la sección que toca la tarea.
4. **La sección de la fase en curso** de `PROMPT-FASES-B.md`.
5. **Los archivos que la tarea toca**, localizados con `grep`, no explorando carpetas.

### Cómo orientarse sin abrir archivos

```bash
# Inventario de endpoints con su política de permiso
grep -rn "Map\(Get\|Post\|Put\|Delete\|Group\)(\"" Facturacion.Server/Modules --include=*.cs

# La frontera congelada entre las dos mitades
ls Facturacion.Shared/Contratos/

# Componentes comunes que ya existen — revísalo ANTES de crear uno nuevo (ARQUITECTURA.md §10)
ls Facturacion.Client/Componentes/Comunes/

# Qué hay construido de la mitad B
find Facturacion.Server/Modules/Documentos -name "*.cs"
```

---

## 3. Detalle de lo construido en la mitad B

Todo vive en `Facturacion.Server/Modules/Documentos/`, `Data/{Entidades,Configurations}/Documentos/`
y, del lado del cliente, en `Paginas/{Facturas,Pagos,Documentos}/` y `Servicios/Documentos/`.

**B0 — base.** Siete entidades: `Comprobante`, `Concepto`, `ImpuestoConcepto`,
`ComprobanteRelacionado`, `IntentoTimbrado`, `SolicitudCancelacion` y `Pago`. El
`Comprobante` lleva **copias congeladas** de los datos fiscales del emisor y del receptor
(`EmisorRfc`, `ReceptorNombre`, `ReceptorRegimenFiscal`, `ReceptorDomicilioFiscal`…), no
llaves foráneas: reimprimir una factura de hace dos años tiene que dar el mismo papel.
`ResumenDocumentos` es el único contrato que va de B hacia A, y ya alimenta el tablero.

**B1 — motor de impuestos.** `Impuestos/MotorDeImpuestos.cs` y `CalculoDeImpuestos.cs`.
Clase pura, sin EF ni HTTP. Maneja Tasa, Cuota y Exento de forma genérica, así que soporta
cualquier impuesto federal del catálogo (001 ISR, 002 IVA, 003 IEPS). **32 pruebas.**

**B3 — XML.** `Salidas/GeneradorDeXmlCfdi.cs`, `ServicioDeXmlCfdi.cs`, `EsquemasSat.cs` y
`GeneradorDeXmlPago.cs`. El XSD y el XSLT del SAT se compilan **una sola vez**
(`EsquemasSat` es singleton). Validación contra esquema antes de mandar nada.

**B4 — timbrado, cerrado contra sandbox.** `Timbrado/ServicioDeTimbrado.cs` implementa los
**tres pasos**: transacción corta que aparta folio y timbre → llamada al PAC **fuera de
toda transacción** → transacción corta que confirma o revierte. `CierreDeTimbrado.cs`
resuelve el cierre; `ConciliacionDeTimbrados.cs` es un `HostedService` que rescata los
comprobantes que quedaron en `timbrando` tras un corte de red.
`Pac/ProveedorPacSwSapien.cs` (566 líneas) implementa `IProveedorPac`.

> **Pendiente de la regla del plan:** producción solo cuando el sandbox pase **50 timbrados
> seguidos**. No hay constancia de que se haya corrido esa tanda. Antes de tocar producción,
> córrela y deja el resultado por escrito.

**B5 — PDF.** `Salidas/GeneradorDePdfCfdi.cs` con QuestPDF (licencia Community declarada en
`DocumentosModule`, así que no hay marca de agua) y QRCoder. Todo sale del comprobante
congelado: ni una consulta al catálogo.

**B2 — emisión (sin commitear).** `Paginas/Facturas/FormularioDeEmision.razor` (542 líneas),
`Modules/Documentos/Emision/`. Cabecera, cliente, rejilla de conceptos, CFDI relacionados,
información global condicionada a `XAXX010101000`, borradores que no consumen folio ni
timbre. **Le faltan los totales en vivo y la captura por teclado que pedía el prompt.**

**B7 — listado (sin commitear).** `Paginas/Documentos/ListadoDeDocumentos.razor`. Filtros de
texto, estatus y rango de fechas, con `Virtualize`. **Le faltan el filtro por cliente y tipo,
la exportación a CSV y la descarga de XML y PDF** — que era la mitad del alcance de la fase.

**B8 — cancelación (sin commitear).** `Modules/Documentos/Cancelacion/`, motivos 01 a 04 con
UUID sustituto obligatorio en el 01, y consulta de estatus ante el SAT. **Falta la pantalla
dedicada de solicitudes de cancelación (§30).**

**B9 — pagos (sin commitear).** `Modules/Documentos/Pagos/`, `Paginas/Pagos/FormularioDePago.razor`.
Cabecera, datos bancarios, rejilla de documentos con saldo y parcialidad recalculados en
servidor. **Falta la pestaña SPEI (§29) y los relacionados del pago.**

**B6 — correo.** Sin empezar. La infraestructura existe (`Infra/Correo/IServicioDeCorreo`,
`ServicioDeCorreoSmtp`, `ServicioDeCorreoConsola`) y ya la usa el flujo de invitaciones, así
que la fase es enganchar XML y PDF como adjuntos, no montar correo desde cero.

---

## 4. Lo siguiente, en orden

1. **Commitear B2, B7, B8 y B9**, en cuatro commits separados. `dotnet build` sin
   advertencias nuevas y `dotnet test` en verde antes de cada uno.
2. **Arreglar los cuatro defectos** de `docs/AUDITORIA-2026-08-19.md` §A. Dos son de datos
   (serie que se pierde, importe que no cuadra con el XML): van antes que cualquier función
   nueva.
3. **Cerrar B7**: descarga autorizada de XML y PDF, filtro por cliente y tipo, exportación a
   CSV. Sin la descarga no hay forma de entregarle la factura al cliente.
4. **B6 — correo.** Con la descarga hecha, es enganchar los adjuntos. Campos exactos del
   sistema viejo en §B.6 de la auditoría.
5. **Pruebas de B2, B7, B8 y B9.** Las 88 pruebas actuales no cubren ni una línea de esas
   cuatro fases.
6. **Huecos de campo y usabilidad**, por prioridad, según §B y §C de la auditoría.

**Fecha límite del MVP: 5 de diciembre de 2026.** Quedan ~15 semanas.

---

## 5. Reglas de trabajo del proyecto

Están en `ARQUITECTURA.md` §10, pero son las que más se incumplen:

- **Por fases, con parada y revisión entre cada una.** Al terminar una fase te detienes, dices
  qué construiste y qué quedó pendiente, y **esperas**. No empiezas la siguiente sin que te lo
  pidan.
- **No generes código de fases futuras.** Ni «de una vez», ni «para dejarlo listo».
- **Si algo de lo que se pide tiene un problema, dilo antes de construirlo.** Vale más una
  objeción de tres líneas que una corrección de tres horas.
- **Nada de `TODO` ni de métodos que devuelven datos falsos** en código que se sube. Hoy el
  repositorio está limpio de ambos: mantenlo así.
- **`Facturacion.Shared/Contratos/` está congelada.** Cambiar una firma o un DTO de ahí exige
  acuerdo explícito y un commit dedicado sin ningún otro cambio.
- **La mitad B consume lo que produce la A, nunca al revés.** Si Documentos necesita un cambio
  en Plataforma, el contrato quedó corto: **avisar**, no cruzar la frontera por dentro.
- **Al cerrar cada fase:** `dotnet build` sin advertencias nuevas y la aplicación corriendo.
  Una fase que no compila no está terminada.

Tres reglas específicas de esta mitad que cuestan caro al romperse:

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
  Infra/Correo/                             IServicioDeCorreo — ya existe, lo usa invitaciones
  Infra/Idempotencia/                       filtro y buffer de Idempotency-Key
  Infra/Seguridad/                          cabeceras, CSP, control de intentos
  Infra/Tenencia/                           empresa desde el claim, sellado, HusoDeEmpresa
  Modules/Plataforma/                       mitad A — cerrada
    Auth/ Catalogos/ Empresas/ Folios/ Clientes/ Productos/ Timbres/ Usuarios/ Tablero/
  Modules/Documentos/                       mitad B
    Impuestos/ Salidas/ Timbrado/ Pac/ Emision/ Cancelacion/ Pagos/ ResumenDocumentos.cs

Facturacion.Client/
  Componentes/Comunes/    20 componentes — revísalo antes de crear cualquiera
  Layout/                 MainLayout único, selector de empresa, selector de tema
  Paginas/                Tablero, Cuenta, Empresas, Usuarios, Clientes, Productos, Timbres,
                          Admin, Facturas, Pagos, Documentos
                          Cancelaciones/ ← sigue vacía (§30 pendiente)
  Servicios/{Plataforma,Documentos}/

Facturacion.Shared/
  Comun/        Rfc, NombreFiscal, Permiso, Resultado, EstatusComprobante, DobleDePrueba
  Contratos/    frontera congelada — 9 interfaces
  {Plataforma,Documentos}/   DTOs
```

**Endpoints:** trece grupos bajo `/api/` — `auth`, `catalogos`, `clientes`, `empresa`,
`series`, `productos`, `timbres`, `perfil`, `usuarios`, `tablero`, `documentos` (emisión,
timbrado y cancelación comparten el grupo), `pagos`, más `invitaciones` anónimo.

**Pruebas:** 88 métodos en `tests/Facturacion.Pruebas/`, contra SQL Server real y no en
memoria, porque lo que prueban son bloqueos de renglón e índices únicos. Cubren RFC, folios
concurrentes, bolsa de timbres, aislamiento por empresa, `UsoCFDI`, idempotencia, refresh
tokens, enmascarado de logs, motor de impuestos (32), XML (13), representación impresa (8) y
esquemas del SAT. **No cubren emisión, listado, cancelación ni pagos.**

**Otros documentos:** `docs/REPASO-SEGURIDAD.md` (16 KB) y `docs/DESPLIEGUE.md` (12 KB).
Consúltalos solo si la tarea es de seguridad o de despliegue.

---

## 7. Entorno y acceso

Levantar el entorno: **`docs/ARRANQUE-LOCAL.md`** (7 KB, la única guía). Resumen: copiar
`appsettings.Development.json.ejemplo`, generar la llave maestra, `dotnet ef database update`,
cargar los catálogos del SAT desde el `.xls`, y `dotnet run --project Facturacion.Server`.

No hay usuarios en el repositorio: los crea el **sembrado de desarrollo**, que solo corre en
`Development` y solo si `Sembrado:Correo` y `Sembrado:Contrasena` están puestos en
`appsettings.Development.json` (gitignoreado). Es el único usuario, trae los **seis permisos**
y **dos empresas**, así que la primera pantalla tras iniciar sesión es el selector de empresa.

URL: `https://localhost:7123`. El `Server` sirve la API y los estáticos del `Client`: un solo
origen, un solo proceso.

**Si el inicio de sesión falla:** la contraseña del sembrado necesita 12 caracteres o más con
mayúscula, minúscula y dígito; si no cumple, Identity la rechaza y el sembrado nunca crea al
usuario. Tras varios intentos entra el bloqueo por IP, que crece con cada tanda y vive en
memoria: esperar o reiniciar. **Sin catálogos del SAT cargados, toda validación fiscal
rechaza**, y el síntoma es una clave válida que no se deja guardar.

**El PAC (SW Sapien) apunta a sandbox.** Sus credenciales van en `appsettings.Development.json`,
sección del PAC. No hay credenciales de producción en ninguna parte, y así debe quedarse hasta
que pase la tanda de 50.

---

## 8. Prompt para arrancar la sesión nueva

```
Lee ARQUITECTURA.md completo, docs/ESTADO-Y-CONTINUACION.md y docs/AUDITORIA-2026-08-19.md.

NO leas: docs/UI_Funcional.md (solo secciones puntuales con grep cuando una fase las
cite), AGENTS.md (es copia de ARQUITECTURA.md), Facturacion.Server/Migrations/*,
PROMPT-FASES-A.md, la carpeta "Sistema anterior" (las capturas ya están inventariadas
en la auditoría; abrirlas cuesta más que leer la tabla), ni la carpeta "Sistema de
Facturacion" (bóveda de Obsidian, no es código).

Trabajo las dos mitades. La A está cerrada. De la B están commiteadas B0, B1, B3, B4 y
B5. B2, B7, B8 y B9 están construidas pero sin commitear, y con los pendientes que
describe la auditoría. B6 no ha empezado.

Empieza por commitear: hay 111 archivos en el árbol de trabajo. Propón cómo partirlos
en cuatro commits por fase, corre dotnet build y dotnet test, y dime qué encontraste.
Luego espera.
```
