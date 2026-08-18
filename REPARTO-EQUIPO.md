# Reparto del trabajo — Sistema de facturación CFDI 4.0

Acuerdo entre los dos desarrolladores. Va en la raíz del repositorio.
Versión del 11 de agosto de 2026.

---

## 1. Alcance del MVP — decidido y cerrado

El documento `docs/UI_Funcional.md` describe **31 pantallas del sistema legacy completo**.
No es el MVP. Portarlo entero para el 5 de diciembre no es posible: Carta Porte 3.1,
Comercio Exterior 2.0, el complemento de Notarios y el de INE son complementos del SAT
con su propio esquema, su propia validación y su propio catálogo, y cada uno cuesta
entre 80 y 200 horas bien hechas.

**Dentro del MVP:**

multiempresa · catálogos del SAT · clientes · productos · emisión de factura estándar ·
motor de impuestos · timbrado · cancelación 01–04 · PDF con QR y cadena original ·
envío por correo · **complemento de pagos** · bolsa de timbres y membresías

**Fuera del MVP, fase 2:**

Carta Porte y sus catálogos (§10, §11, §26) · Comercio Exterior (§16–§19) ·
Notaría (§20–§25) · Constructoras (§15) · Cotizaciones · Addendas · complemento INE ·
inventario

Se modela desde ahora el campo de licencias por empresa (`LicNotarios`, `LicObras`,
`LicComercio`, `LicINE`) para que la fase 2 sea agregar módulos, no rehacer el esquema.

### Por qué el complemento de pagos entra al MVP aunque estaba en fase 2

CFDI 4.0 obliga: si se emite una factura con método de pago **PPD**, hay que emitir
después el complemento de pagos por cada pago recibido. Un sistema que permite emitir
PPD y no puede emitir el complemento deja a su usuario en incumplimiento con el SAT.

Las únicas dos salidas honestas eran meter pagos al MVP o bloquear PPD por completo.
Bloquear PPD es inviable comercialmente: los contadores lo usan a diario. **Entra pagos.**

### Por qué el SMTP del cliente no se implementa

El sistema viejo (§3) pide servidor, usuario y contraseña de correo por empresa. En un
SaaS multiempresa eso significa que la base guarda contraseñas de correo reutilizables
de decenas de empresas: una fuga y el daño es de ellas, no tuyo, y la responsabilidad
sí es tuya.

**Decisión:** todo sale de un remitente propio del SaaS con dominio verificado (SPF,
DKIM, DMARC), con `Reply-To` al correo de la empresa emisora. Se ve profesional, llega
mejor a bandeja de entrada, y no se custodia ningún secreto ajeno. SMTP propio por
empresa queda como fase 2 opcional.

---

## 2. Criterio del reparto

El corte **no** es por capa (uno frontend, otro backend). Es por **rebanada vertical de
módulo**: cada quien es dueño de sus pantallas, sus endpoints, sus entidades y sus
migraciones, de punta a punta.

Un corte por capa obliga a que los dos toquen los mismos archivos todos los días y a
que uno espere al otro para probar cualquier cosa. Un corte vertical permite que cada
quien compile, pruebe y avance sin bloquearse.

La dependencia entre las dos mitades es **unidireccional**:

```
   Mitad A — Plataforma        ──produce──▶      Mitad B — Documentos
   identidad, empresas,                          emisión, impuestos,
   catálogos SAT, clientes,                      timbrado, PDF, correo,
   productos, folios, timbres                    cancelación, pagos
```

B consume lo que A produce, nunca al revés. Por eso **A arranca primero** y B trabaja
contra contratos, no contra implementaciones.

---

## 3. Quién toma qué

> **Absorción activada el 17 de agosto de 2026.** Luis toma también la mitad B. La
> cláusula de contingencia de §6 fijaba el 15 de octubre como punto de control; se
> adelantó por decisión, no por incumplimiento de esa fecha.
>
> Consecuencia práctica: quedan unas quince semanas al 5 de diciembre, más del doble de
> las siete que suponía el plan de emergencia de `PROMPT-FASES-B.md`. Los recortes que
> ese plan propone —bloquear `PPD`, cancelación manual, correo manual— **no están
> decididos**; se decidirán con el avance real, no por adelantado.
>
> El reparto por mitades se conserva en este documento tal como se acordó. No es
> arqueología: la frontera de `Shared/Contratos/` sigue congelada y la dependencia sigue
> siendo unidireccional, que es lo que permitiría volver a repartir el trabajo si entra
> alguien más.

### Mitad A — Plataforma, identidad y catálogos → **Luis**

| Módulo | Referencia en `UI_Funcional.md` |
|---|---|
| Autenticación, refresh, cambio de empresa activa | nuevo |
| Usuarios, permisos, invitación de auxiliares | §9 |
| Alta de cuenta, empresas emisoras, multiempresa | §4 |
| Configuración de la empresa (tasas, logo, avisos) | §3 |
| Series, folios y su procedimiento de reserva | §4 parcial |
| Certificados CSD: carga, validación y custodia cifrada | §4 parcial |
| Catálogos del SAT: carga, consulta y autocompletado | §31 |
| Clientes | §7, §8 |
| Productos y servicios | §5, §6 |
| Membresías, paquetes y bolsa de timbres | nuevo |
| `MainLayout`, temas, PWA, componentes comunes | nuevo |
| Dashboard | §1 parcial |

### Mitad B — Documentos, timbrado y salidas → **compañero**

| Módulo | Referencia en `UI_Funcional.md` |
|---|---|
| Formulario de emisión de factura estándar | §12 |
| CFDI relacionados | §13 |
| Información global (público en general) | §14 |
| Motor de cálculo de impuestos y totales | §12.4, anexo |
| Orquestación del timbrado en tres pasos | nuevo |
| Integración con la API del PAC | nuevo |
| Generación del XML y del PDF con QR y cadena original | §1.4 |
| Envío por correo | §1.3 |
| Listado de documentos y filtros | §1.1, §1.2 |
| Cancelación y solicitudes de cancelación | §30 |
| Complemento de pagos | §27, §28, §29 |

### Por qué Luis toma Plataforma y no Documentos

Documentos es lo llamativo —el timbrado, el PDF— pero depende de todo lo que produce
Plataforma. Si Luis toma Documentos y el compañero se atrasa, Luis queda bloqueado desde
octubre sin poder avanzar. Al revés no ocurre: Plataforma no depende de nadie, se puede
terminar completa y sola, y si hay que absorber la otra mitad, se absorbe con una base
ya estable. Dado que el plan explícito es guardar copia en Git por si el compañero no
termina, este es el único reparto compatible con esa previsión.

---

## 4. Fronteras de archivos

Regla dura: **si un archivo no está en tu lista, no lo editas.** Si necesitas un cambio
en territorio ajeno, se pide por issue; no se hace directo.

### Server

```
Facturacion.Server/
  Modules/
    Plataforma/                    ← A, exclusivo
      Auth/
      Empresas/
      Usuarios/
      Catalogos/
      Clientes/
      Productos/
      Folios/
      Timbres/
      PlataformaModule.cs          (AddPlataforma / MapPlataforma)
    Documentos/                    ← B, exclusivo
      Emision/
      Impuestos/
      Timbrado/
      Pac/
      Salidas/                     (XML, PDF, correo)
      Cancelacion/
      Pagos/
      DocumentosModule.cs          (AddDocumentos / MapDocumentos)
  Data/
    AppDbContext.cs                ← COMPARTIDO, ver §5
    Configurations/
      Plataforma/                  ← A, un IEntityTypeConfiguration por entidad
      Documentos/                  ← B, idem
  Migrations/                      ← COMPARTIDO, ver §5
  Infra/                           ← A, exclusivo (Data Protection, almacén de archivos,
                                     idempotencia, cabeceras, Serilog, Sentry)
  Program.cs                       ← COMPARTIDO, ver §5
```

### Client

```
Facturacion.Client/
  Layout/                          ← A, exclusivo
  Theme/                           ← A, exclusivo
  Componentes/
    Comunes/                       ← A, exclusivo
    Documentos/                    ← B, exclusivo
  Paginas/
    Cuenta/ Empresas/ Usuarios/
    Clientes/ Productos/ Timbres/
    Tablero/                       ← A
    Facturas/ Pagos/ Cancelaciones/ ← B
  Servicios/
    Plataforma/                    ← A
    Documentos/                    ← B
  wwwroot/
    css/                           ← A
    service-worker.published.js    ← A
```

### Shared

```
Facturacion.Shared/
  Contratos/                       ← COMPARTIDO, congelado tras la fase 0
  Plataforma/                      ← A, exclusivo
  Documentos/                      ← B, exclusivo
  Comun/                           ← A, exclusivo
```

---

## 5. Los cuatro archivos compartidos

Son los únicos puntos donde los dos escriben. Cada uno tiene su regla.

**`Program.cs`** — no crece. Solo llama a los módulos:

```csharp
builder.Services.AddInfraestructura(builder.Configuration);
builder.Services.AddPlataforma(builder.Configuration);
builder.Services.AddDocumentos(builder.Configuration);
// …
app.MapPlataforma();
app.MapDocumentos();
```

Todo registro nuevo va dentro de tu propio `*Module.cs`, nunca aquí.

**`AppDbContext.cs`** — solo los `DbSet<>` y la línea que aplica configuraciones desde
el ensamblado. Los `DbSet` de A en un bloque marcado, los de B en otro. **Nunca** Fluent
API dentro de este archivo. Así los conflictos de Git se reducen a líneas adyacentes de
`DbSet`, que se resuelven en segundos.

**`Migrations/`** — nombre obligatorio: `AAAAMMDD_A_LoQueSea` o `AAAAMMDD_B_LoQueSea`.
Nunca dos migraciones en vuelo al mismo tiempo: quien va a generar una avisa, la genera
contra `develop` recién actualizada, y la sube el mismo día. Si dos chocan, se borra la
de B y se regenera encima de la de A — nunca al revés, porque B depende de A.

**`Shared/Contratos/`** — el contrato entre las dos mitades. Se define en la fase 0 y
después **se congela**. Cambiarlo requiere acuerdo explícito de los dos y un commit
dedicado que no traiga nada más.

### Contenido de `Shared/Contratos/`

```csharp
// ── Lo que B necesita de A ───────────────────────────────────────────────

IContextoEmpresa
    Guid EmpresaId { get; }
    Guid UsuarioId { get; }
    bool Tiene(string permiso);

IServicioCatalogosSat
    Task<ClaveSatDto?> ResolverAsync(string catalogo, string clave, CancellationToken ct);
    Task<IReadOnlyList<ClaveSatDto>> BuscarAsync(string catalogo, string texto, int tope, CancellationToken ct);
    Task<bool> EsUsoCfdiCompatibleAsync(string usoCfdi, string regimenReceptor, bool esPersonaMoral, CancellationToken ct);

IServicioClientes
    Task<ReceptorFiscalDto?> ObtenerParaTimbradoAsync(Guid clienteId, CancellationToken ct);
    // devuelve la foto fiscal completa lista para congelar en el comprobante

IServicioProductos
    Task<ProductoParaConceptoDto?> ObtenerParaConceptoAsync(Guid productoId, CancellationToken ct);

IServicioEmpresaEmisora
    Task<EmisorFiscalDto> ObtenerParaTimbradoAsync(CancellationToken ct);
    Task<CsdDescifradoDto> ObtenerCsdAsync(CancellationToken ct);   // uso exclusivo del timbrado

IServicioFolios
    Task<FolioReservadoDto> ReservarAsync(Guid serieId, CancellationToken ct);
    Task LiberarSiNoUsadoAsync(Guid reservaId, CancellationToken ct);

IServicioTimbres
    Task<ReservaTimbreDto> ReservarAsync(Guid comprobanteId, CancellationToken ct);
    Task ConfirmarAsync(Guid reservaId, CancellationToken ct);
    Task DevolverAsync(Guid reservaId, string motivo, CancellationToken ct);
    Task<int> DisponiblesAsync(CancellationToken ct);

// ── Lo que A necesita de B (solo lectura, para el tablero) ───────────────

IResumenDocumentos
    Task<ResumenDocumentosDto> ObtenerAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
```

Los DTOs correspondientes viven junto a las interfaces. Son `record` inmutables.

---

## 6. Git

- Ramas: `main` (protegida, solo release) ← `develop` ← `feat/a-<modulo>` / `feat/b-<modulo>`.
- Nadie hace push directo a `develop`. Todo entra por pull request revisado por el otro.
- Cada quien actualiza su rama desde `develop` **al inicio de cada sesión de trabajo**.
  Un PR de cinco días sin rebasar es un conflicto garantizado.
- Un PR que toca archivos de la mitad ajena se rechaza sin discusión. No es un juicio
  sobre el código: es la única forma de que la frontera sirva de algo.
- Etiqueta al cierre de cada fase: `fase-1-a`, `fase-1-b`, etc. Es lo que permite
  retomar la mitad del otro si no la termina.
- `.gitignore` incluye `appsettings.Development.json`, `*.cer`, `*.key`, `*.pfx` y la
  carpeta de almacenamiento de archivos. **Ningún certificado entra al repositorio, ni
  de prueba.**

### Plan de contingencia — fecha dura

Si al **15 de octubre de 2026** la mitad B no timbra contra el sandbox del PAC, la mitad
A la absorbe y el complemento de pagos se recorta a fase 2, bloqueando `PPD` en el MVP.

Esa fecha deja siete semanas para integrar y probar antes del 5 de diciembre. Ponerla
por escrito hoy, cuando nadie está molesto, es lo que permite invocarla en octubre sin
que sea un reclamo personal.

---

## 7. Cómo se prueba sin el otro

Cada mitad implementa **dobles de prueba** de las interfaces ajenas, registrados solo
en `Development`:

- A implementa `IResumenDocumentos` con datos falsos para su tablero.
- B implementa `IServicioCatalogosSat`, `IServicioClientes`, `IServicioProductos`,
  `IServicioEmpresaEmisora`, `IServicioFolios` y `IServicioTimbres` con datos falsos.

Cuando la implementación real está lista, se cambia una línea de registro.

**Esto es lo más importante del documento y lo más fácil de saltarse.** Sin dobles, B
no puede probar el formulario de emisión hasta que A termine clientes y productos, y se
pierde un mes de calendario que no existe.

---

## 8. Calendario

16 semanas hábiles entre el 12 de agosto y el 5 de diciembre. El plan de A reserva las
últimas dos para integración y corrección; ver `PROMPT-FASES-A.md` §0.

| Hito | Fecha |
|---|---|
| Fundación y contratos congelados | 18 de agosto |
| Autenticación funcionando | 29 de agosto |
| Catálogos del SAT disponibles para B | 26 de septiembre |
| Clientes y productos disponibles para B | 24 de octubre |
| **Punto de control de contingencia** | **15 de octubre** |
| Timbres y usuarios terminados | 14 de noviembre |
| Congelamiento de funcionalidad | 21 de noviembre |
| Entrega | 5 de diciembre |

### Sobre el presupuesto

Las 500 horas se presupuestaron cuando Luis solo hacía frontend. Ahora hace frontend y
backend de media aplicación, más el complemento de pagos que antes estaba fuera. El plan
de fases suma entre 380 y 430 horas para la mitad A sola, sin contar integración ni
corrección de errores ajenos.

Con 16 semanas eso son 25 a 27 horas por semana sostenidas. Es alcanzable, pero no tiene
holgura para imprevistos. Si hay que recortar algo, el orden es: reportes primero, luego
el tercer tema visual (sepia), luego la carga diferida de ensamblados. **Nunca se recorta
autenticación, aislamiento por empresa ni la reserva de folios.**
