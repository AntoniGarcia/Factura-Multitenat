# Plan de fases — Mitad A (Plataforma, identidad y catálogos)

Cada bloque marcado con `▸ PROMPT` describe **tal cual** el alcance de una fase.
Se ejecuta una fase, se revisa, y hasta entonces se arranca la siguiente.

Antes de empezar, la raíz del repositorio debe tener:
`ARQUITECTURA.md`, `REPARTO-EQUIPO.md` y `docs/UI_Funcional.md`.

---

## 0. Calendario y modelo a usar

| Fase | Contenido | Modelo | Horas | Cierra |
|---|---|---|---|---|
| 0 | Fundación, contratos, base de datos | Opus | 30 | 18 ago |
| 1 | Autenticación, refresh, empresa activa | **Opus** | 45 | 29 ago |
| 2 | Layout, temas, PWA, componentes comunes | Sonnet | 50 | 12 sep |
| 3 | Catálogos del SAT | Sonnet | 45 | 26 sep |
| 4 | Empresas, configuración, series, folios, CSD | Opus | 50 | 10 oct |
| 5 | Clientes | Sonnet | 35 | 17 oct |
| 6 | Productos | Sonnet | 30 | 24 oct |
| 7 | Membresías, paquetes y bolsa de timbres | **Opus** | 40 | 7 nov |
| 8 | Usuarios, permisos e invitaciones | Sonnet | 35 | 14 nov |
| 9 | Tablero, integración y endurecimiento | Opus | 40 | 21 nov |
| — | Reserva para integración y corrección | — | — | 5 dic |

Opus donde el costo de equivocarse es alto y difícil de detectar: seguridad, dinero,
concurrencia. Sonnet para el resto.

El orden no es arbitrario. Catálogos, folios, clientes y productos van temprano porque
**desbloquean a la mitad B**. Usuarios va tarde porque es lo único que B no necesita.

---

## Fase 0 — Fundación

▸ PROMPT

```
Lee ARQUITECTURA.md y REPARTO-EQUIPO.md completos antes de escribir nada.

Fase 0: fundación. Construye únicamente lo siguiente.

1. ESTRUCTURA DE LA SOLUCIÓN
   Tres proyectos .NET 10: Facturacion.Client (Blazor WebAssembly con PWA),
   Facturacion.Server (ASP.NET Core Web API que además sirve los estáticos del
   Client) y Facturacion.Shared (biblioteca de clases).
   La plantilla blazorwasm --hosted ya no existe. Verifica con `dotnet new list`
   qué hay disponible antes de decidir cómo armarla; si algo no coincide con lo
   que dice ARQUITECTURA.md, detente y dímelo en vez de improvisar.
   Crea el árbol de carpetas exacto de REPARTO-EQUIPO.md §4, con .gitkeep en las
   vacías. Incluye también las carpetas de la mitad B: existen para que la
   frontera esté marcada desde el primer commit, pero quedan vacías.
   Agrega .gitignore con appsettings.Development.json, *.cer, *.key, *.pfx y la
   carpeta de almacenamiento de archivos.

2. CONTRATOS COMPARTIDOS
   En Shared/Contratos/ define solo las interfaces y los DTOs listados en
   REPARTO-EQUIPO.md §5. Sin implementaciones. Los DTOs son records inmutables.
   Estas interfaces son la frontera entre las dos mitades y se van a congelar:
   si alguna te parece mal diseñada, incompleta o mal ubicada, dímelo ahora.

3. PRIMITIVAS COMUNES
   En Shared/Comun/: el tipo de error basado en Problem Details (RFC 7807) con
   traceId; los enums de estatus de comprobante y de permisos con los valores
   exactos de ARQUITECTURA.md; un tipo Resultado<T> para propagar error de negocio sin
   excepciones.

4. BASE DE DATOS
   AppDbContext con la disciplina de REPARTO-EQUIPO.md §5: sin Fluent API dentro
   del archivo, todo en Data/Configurations/Plataforma/, un archivo por entidad.

   Entidades de esta fase, solo identidad y tenencia:
     Cuenta            el contratante que paga la suscripción
     Empresa           emisora; incluye ya LicNotarios, LicObras, LicComercio,
                       LicINE aunque esos módulos queden fuera del MVP
     Usuario           deriva de IdentityUser<Guid>
     UsuarioEmpresa    a qué empresas de la cuenta accede cada usuario
     Permiso, UsuarioEmpresaPermiso
     RefreshToken      con FamiliaId para poder invalidar una sesión completa
     ClaveIdempotencia clave, respuesta serializada, momento, con purga a 24 h
     RegistroBitacora  usuario, empresa, entidad, acción, antes, después, momento

   Configura ASP.NET Core Identity con Guid como clave, solo como almacén,
   motor de hash y bloqueo por intentos. No uses los endpoints de Identity API.

   Implementa el query filter global por EmpresaId y un IContextoEmpresa que lo
   alimente desde los claims. Las tablas de Identity y los catálogos del SAT no
   llevan EmpresaId: déjalos fuera del filtro y explícame cómo lo resolviste.

   Genera la primera migración con el nombre 20260812_A_Fundacion.

5. INFRAESTRUCTURA MÍNIMA
   Program.cs reducido a llamadas de módulo (AddInfraestructura, AddPlataforma,
   AddDocumentos; MapPlataforma, MapDocumentos). AddDocumentos existe vacío.
   Serilog con depuración de datos sensibles: nunca deben llegar al log RFC
   completos, contraseñas, tokens ni contenido de certificados. Escribe el
   enriquecedor que los enmascara y déjalo probado con un caso.
   Middleware que convierte toda excepción no controlada en Problem Details con
   traceId, sin filtrar el detalle interno.
   Endpoint GET /api/version con la versión compilada.
   Cabeceras de seguridad: CSP con wasm-unsafe-eval, X-Content-Type-Options,
   Referrer-Policy, Permissions-Policy, HSTS.

NO construyas: endpoints de autenticación, pantallas, layout, temas, catálogos,
ni nada de la mitad B.

Al terminar detente y dime: qué quedó construido, qué decisiones tomaste que no
estaban especificadas, y qué te parece riesgoso de lo que viene.
```

---

## Fase 1 — Autenticación y empresa activa · **Opus**

▸ PROMPT

```
Lee ARQUITECTURA.md y REPARTO-EQUIPO.md. Fase 1: autenticación. Construye únicamente
el circuito de identidad, de punta a punta.

SERVER

  POST /auth/iniciar-sesion
    Devuelve el access token en el cuerpo y planta el refresh token en cookie
    HttpOnly, Secure, SameSite=Strict. El parámetro "mantener sesión iniciada"
    cambia solo la duración de la cookie: 12 horas sin marcar, 30 días marcado.
    Si el usuario tiene una sola empresa, el token ya sale con esa empresa activa.

  POST /auth/refresh
    Lee la cookie, ROTA el token (invalida el usado, emite uno nuevo de la misma
    familia) y devuelve un access token nuevo. Si llega un refresh token ya
    consumido, invalida toda la familia, registra el evento como incidente de
    seguridad y responde 401.

  POST /auth/cerrar-sesion
    Invalida la familia completa y limpia la cookie.

  POST /auth/cambiar-empresa
    Recibe el id de empresa, verifica que el usuario tenga acceso a ella dentro
    de su cuenta y que la empresa esté activa, y emite un access token nuevo con
    esa empresa como claim. No toca la cookie.

  GET /auth/sesion
    Devuelve usuario, empresas disponibles, empresa activa y permisos en ella.

  El access token dura 15 minutos. Claims: id de usuario, id de cuenta, id de
  empresa activa, lista de permisos en esa empresa, y el id de familia de sesión.

  Política de autorización por permiso, con los seis valores exactos de ARQUITECTURA.md.
  Cada permiso es una política de ASP.NET Core; los endpoints se anotan con la
  política, nunca con nombres de rol.

CLIENT

  Un AuthenticationStateProvider propio que guarda el access token solo en memoria.
  Un DelegatingHandler que adjunta el token y que ante un 401 intenta refresh una
  sola vez y reintenta la petición original. Cuida el caso de varias peticiones
  fallando a la vez: debe haber UN SOLO refresh en vuelo, no uno por petición, y
  las demás esperan su resultado. Si el refresh falla, se cierra la sesión.
  Un gate de arranque que intenta /auth/refresh antes de renderizar nada,
  mostrando un estado de carga mínimo mientras resuelve.
  Pantallas de inicio de sesión y de selección de empresa. Sin diseño elaborado
  todavía —los temas son la fase 2— pero ya con variables CSS semánticas, nunca
  con colores literales.
  Un componente que muestre u oculte contenido según permiso, con un comentario
  que deje claro que es comodidad visual y no protección.

REQUISITOS QUE QUIERO QUE RESPETES EXPLÍCITAMENTE

  - El Client no envía nunca un identificador de empresa fuera de
    /auth/cambiar-empresa.
  - Toda respuesta de error sale en Problem Details con traceId.
  - El mensaje de credenciales inválidas es idéntico tanto si el usuario no
    existe como si la contraseña es incorrecta, y tarda lo mismo en ambos casos.
  - Límite de intentos por cuenta y por IP, con bloqueo temporal creciente.
  - Ningún dato sensible en los logs; verifica contra el enriquecedor de la fase 0.
  - Toda emisión, rotación e invalidación de token queda en la bitácora.

PRUEBAS OBLIGATORIAS DE ESTA FASE
  Un test que compruebe que reutilizar un refresh token consumido invalida la
  familia completa. Es el único test de la fase, y no es negociable.

NO construyas: layout definitivo, temas, catálogos, ni nada de la mitad B.

Al terminar detente y dime qué construiste, qué decisiones tomaste que no estaban
especificadas, y qué parte del circuito de identidad te parece más frágil.
```

---

## Fase 2 — Layout, temas, PWA y componentes comunes

▸ PROMPT

```
Lee ARQUITECTURA.md, sobre todo §8 y §9. Fase 2: el cascarón visual y los componentes
que va a usar todo el sistema, incluida la mitad B.

1. VARIABLES CSS Y TEMAS
   Un solo archivo de variables semánticas: superficie, superficie-elevada, fondo,
   borde, borde-fuerte, texto, texto-suave, texto-inverso, acento, acento-suave,
   estado-timbrada, estado-borrador, estado-cancelada, estado-advertencia,
   estado-error, foco.
   Tres temas: claro (con los valores exactos de ARQUITECTURA.md §8), oscuro ergonómico
   (grises y azules profundos, jamás negro puro) y cálido/sepia.
   El tema se aplica con un atributo data-tema en el elemento raíz. La preferencia
   del usuario se guarda en el servidor, en su perfil, no en el navegador: el
   contador que cambia de máquina debe encontrar su tema.
   Configura el tema de MudBlazor para que LEA estas variables. Ningún componente
   escribe un color literal; si necesitas uno que no existe, agrégalo al conjunto
   de variables y dímelo.
   Verifica contraste AA en los tres temas y dime cuál falló si alguno falla.

2. MAINLAYOUT
   Uno solo. Barra superior con: nombre del sistema, selector de empresa activa,
   indicador de timbres restantes (por ahora conectado a un doble), selector de
   tema, menú de usuario con cerrar sesión.
   Menú lateral con las secciones, filtradas por permiso.
   El área principal recibe solo el contenido de la página. Las páginas NO aportan
   encabezado propio, ni barra propia, ni esqueleto propio: si una página necesita
   un título, lo declara por parámetro y lo pinta el layout.
   En móvil: el menú lateral se convierte en barra inferior; la barra superior se
   reduce y conserva el selector de empresa.
   El selector de empresa llama a /auth/cambiar-empresa y, al cambiar, limpia todo
   estado de página en memoria. Una lista de clientes de la empresa anterior
   visible un segundo después del cambio es una fuga de datos entre empresas.

3. COMPONENTES COMUNES en Client/Componentes/Comunes/
   Los va a usar también la mitad B, así que son genéricos y están documentados:

   TablaDatos<T>       columnas configurables, orden, paginación en servidor,
                       Virtualize, filas de 40px, estado vacío, estado de carga,
                       y en móvil se transforma en tarjetas apiladas.
   BuscadorCatalogo    autocompletado contra el servidor: mínimo 3 caracteres,
                       debounce 300 ms, muestra "clave — descripción", navegable
                       con flechas, Enter confirma, Esc limpia. Recibe el nombre
                       del catálogo por parámetro. Cancela la petición anterior
                       al escribir de nuevo. Por ahora contra datos falsos.
   ModalCatalogo       la alternativa de rejilla del BuscadorCatalogo, para quien
                       prefiere teclado y lista (sustituye al §31 del legacy).
   CampoRfc            máscara, mayúsculas automáticas, validación de formato y
                       de dígito verificador, tipografía monoespaciada.
   CampoMoneda         decimal, 2 decimales a la vista, cifras tabulares,
                       alineado a la derecha, sin perder precisión interna.
   CampoFecha          con huso horario del lugar de expedición.
   BarraHerramientasCatalogo   nuevo / editar / desactivar / exportar / buscar,
                       el patrón repetido de §5, §7, §9 del documento funcional.
   PanelErrores        pinta un Problem Details con su traceId copiable.
   ConfirmacionAccion  diálogo para operaciones irreversibles.
   IndicadorEstado     la píldora de color por estatus de comprobante.

4. PWA
   Manifiesto con nombre, iconos y color de tema.
   service-worker.published.js: cachea SOLO el app shell. Excluye explícitamente
   toda ruta que empiece con /api/. Escríbelo de forma que sea evidente al leerlo.
   Al arrancar, el Client consulta /api/version y compara con su versión compilada;
   si difieren, muestra un aviso NO descartable y fuerza la recarga.
   Botón de instalación en el menú de usuario, solo cuando el navegador lo permite.

5. RENDIMIENTO
   Configura PublishTrimmed y la compresión Brotli en el Server, y dime cómo
   verificar que los .br se están sirviendo de verdad en producción. No des por
   hecho que funciona porque el proyecto compila.

NO construyas: pantallas de negocio, catálogos reales, ni nada de la mitad B.

Al terminar detente y dime qué construiste, qué variable de color tuviste que
inventar, y qué componente crees que la mitad B va a querer cambiar.
```

---

## Fase 3 — Catálogos del SAT

Esta fase desbloquea a la mitad B. En cuanto cierre, avísale.

▸ PROMPT

```
Lee ARQUITECTURA.md §7. Fase 3: catálogos del SAT.

1. MODELO Y CARGA
   Tabla por catálogo, no una tabla genérica de clave-valor: los catálogos tienen
   columnas propias (vigencias, banderas, matrices de compatibilidad) y aplanarlos
   obliga a interpretar texto en cada consulta.
   Catálogos a cargar: c_ClaveProdServ, c_ClaveUnidad, c_CodigoPostal, c_UsoCFDI,
   c_RegimenFiscal, c_MetodoPago, c_FormaPago, c_Moneda, c_ObjetoImp,
   c_TipoRelacion, c_Periodicidad, c_Meses, c_Exportacion, c_TipoDeComprobante,
   c_Impuesto, c_TipoFactor, c_Pais, c_TasaOCuota.
   Cada catálogo lleva versión publicada por el SAT y fecha de carga.
   Los catálogos NO llevan EmpresaId y quedan fuera del query filter global.

   Comando de carga desde los archivos oficiales del SAT, idempotente: se puede
   volver a correr sin duplicar. Las claves vigentes no se borran nunca, se marcan
   como no vigentes: una factura de 2024 tiene que poder seguir mostrando la
   descripción de una clave que el SAT ya retiró.

2. CONSULTA
   Índice de texto completo en SQL Server sobre descripción de c_ClaveProdServ y
   sobre colonia y municipio de c_CodigoPostal.
   GET /catalogos/{catalogo}/buscar?texto=&tope=   mínimo 3 caracteres, tope 50.
   GET /catalogos/{catalogo}/{clave}               resolución exacta.
   GET /catalogos/precargables                     los chicos de ARQUITECTURA.md §7, en
                                                   una sola respuesta, con ETag.
   GET /catalogos/version                          versión y fecha de cada uno.

   Los grandes NUNCA se devuelven completos. Si alguien pide c_ClaveProdServ sin
   texto de búsqueda, es 400, no una descarga de 50 MB.

3. COMPATIBILIDAD DE USO CFDI
   Carga la matriz de c_UsoCFDI que dice qué usos aplican a qué régimen fiscal y
   a qué tipo de persona. Implementa EsUsoCfdiCompatibleAsync del contrato. Es la
   validación que más timbrados salva y el legacy no la tenía.

4. CLIENT
   Servicio que precarga los catálogos chicos al iniciar sesión y los cachea en
   memoria durante la sesión, revalidando con ETag.
   Conecta el BuscadorCatalogo y el ModalCatalogo de la fase 2 a los endpoints
   reales.
   Una página de administración interna que muestre la versión cargada de cada
   catálogo y cuándo se cargó.

5. IMPLEMENTA IServicioCatalogosSat del contrato compartido y regístralo.
   En cuanto esta fase cierre, la mitad B puede sustituir su doble por el real.

PRUEBA OBLIGATORIA
   Que EsUsoCfdiCompatibleAsync devuelva lo correcto para al menos seis
   combinaciones de régimen y uso, incluyendo el caso 616 + S01 del público en
   general.

Al terminar detente y dime qué construiste, cuánto tarda una búsqueda en
c_ClaveProdServ con la base cargada, y si la matriz de compatibilidad te dio
algún caso ambiguo.
```

---

## Fase 4 — Empresas, configuración, series, folios y CSD · **Opus**

▸ PROMPT

```
Lee ARQUITECTURA.md §4, §5 y §6. Fase 4: la empresa emisora y todo lo que cuelga de
ella. Es la fase con más riesgo de la mitad A: aquí viven los certificados y la
reserva de folios.

1. EMPRESA EMISORA
   Formulario completo con los campos de §4 del documento funcional, EXCEPTO la
   sub-sección de Carta Porte, que queda fuera del MVP.
   Datos fiscales: RFC, nombre o razón social, régimen fiscal (del catálogo),
   código postal del lugar de expedición, domicilio, contacto.
   El nombre se normaliza como exige CFDI 4.0: mayúsculas, sin acentos, sin
   régimen de capital. Muestra al usuario qué se le quitó y por qué.
   Licencias LicNotarios, LicObras, LicComercio, LicINE: se guardan y se muestran,
   pero no habilitan nada todavía porque esos módulos son fase 2. Que quede claro
   en la interfaz que están reservadas.
   Logo: InputFile con vista previa, límite de 2 MB, solo PNG y JPG. Se valida el
   contenido real del archivo, no la extensión. Se guarda FUERA de wwwroot y se
   sirve por endpoint autorizado que verifica que la empresa del solicitante
   coincide con la del archivo.

2. CONFIGURACIÓN DE LA EMPRESA
   Los campos de §3 del documento funcional que sí aplican: tasa de IVA por
   defecto, tasas de retención de IVA e ISR por defecto, días de aviso de
   caducidad del certificado.
   NO implementes la configuración SMTP del cliente. Está descartada por decisión
   de arquitectura, ver REPARTO-EQUIPO.md §1. Si el documento funcional la pide,
   ignórala.

3. CERTIFICADOS CSD
   Carga del .cer y del .key con su contraseña.
   Al cargar, valida en el servidor: que el .cer sea un certificado válido, que
   sea de tipo CSD y no FIEL, que el RFC del certificado coincida con el de la
   empresa, que esté vigente, y que la contraseña abra la llave privada y que la
   llave corresponda al certificado.
   Guarda los archivos y la contraseña CIFRADOS con ASP.NET Core Data Protection,
   fuera de wwwroot. La clave maestra viene de configuración, no del repositorio.
   Nunca en claro, nunca en un log, nunca de vuelta al Client.
   Muestra número de serie y vigencia. Aviso visible cuando falten menos de los
   días configurados para que caduque.
   Un solo certificado vigente por empresa; el anterior se conserva inactivo para
   poder leer comprobantes viejos.
   Implementa IServicioEmpresaEmisora del contrato. ObtenerCsdAsync solo puede
   ser llamado desde el módulo de timbrado: documenta esa restricción y hazla
   cumplir como puedas.

4. SERIES Y FOLIOS
   Series por empresa: prefijo, folio inicial, folio actual, tipo de comprobante
   al que aplica, activa o no.
   Procedimiento almacenado de reserva con UPDATE ... WITH (UPDLOCK) sobre el
   renglón de la serie, dentro de una transacción corta. Devuelve el folio y un
   id de reserva. Nunca SELECT MAX(Folio)+1.
   El folio reservado no se muestra al usuario antes de timbrar.
   Implementa IServicioFolios del contrato.
   Escribe la migración del procedimiento como script, no como código C# que lo
   crea al arrancar.

PRUEBA OBLIGATORIA
   Un test de concurrencia: 50 hilos reservando folio de la misma serie a la vez
   no producen ningún duplicado y ningún hueco no justificado. Si no puedes
   escribirlo contra la base real, dime cómo lo montaste y qué no cubre.

Al terminar detente y dime qué construiste, cómo resolviste la custodia de la
llave privada, y qué pasa si el proceso muere entre la reserva del folio y su
uso.
```

---

## Fase 5 — Clientes

▸ PROMPT

```
Lee ARQUITECTURA.md §7, la parte de reglas de CFDI 4.0 que rompen timbrados.
Fase 5: catálogo de clientes (receptores). Referencia funcional: §7 y §8 del
documento, sin la sub-sección de Carta Porte.

MODELO
   Clave interna consecutiva por empresa (el legacy la usa y los contadores la
   tienen memorizada; no la quites).
   Datos fiscales obligatorios para 4.0: RFC, nombre, régimen fiscal, código
   postal del domicilio fiscal.
   Datos de contacto y domicilio de entrega: opcionales.
   Preferencias que se precargan al facturar: método de pago, forma de pago,
   uso de CFDI.
   RFC único POR EMPRESA con índice filtrado que excluye XAXX010101000 y
   XEXX010101000.
   Baja lógica con Activo = false. Nunca borrado físico.

VALIDACIÓN — es el corazón de esta fase
   RFC: formato del anexo 20 y dígito verificador, calculados EN EL SERVIDOR.
   El Client valida igual para dar respuesta inmediata, pero la que cuenta es la
   del servidor.
   Nombre: se normaliza a mayúsculas, sin acentos y sin régimen de capital
   (S.A. de C.V., S. de R.L., S.C., A.C., etc.). Muestra al usuario el antes y el
   después y explica que debe coincidir con su Constancia de Situación Fiscal.
   Código postal: debe existir en c_CodigoPostal.
   Régimen fiscal: del catálogo, y compatible con el tipo de persona que implica
   la longitud del RFC (12 moral, 13 física).
   Uso de CFDI: valida con EsUsoCfdiCompatibleAsync contra el régimen elegido.
   Si es incompatible, no dejes guardar y di por qué en una frase entendible por
   un contador, no con el código de error.
   Caso especial XAXX010101000: nombre exactamente PUBLICO EN GENERAL, régimen
   616, uso S01. Fíjalos y bloquéalos en la interfaz.
   Caso XEXX010101000 (extranjero): permite el registro con residencia fiscal.

PANTALLAS
   Lista con TablaDatos, búsqueda por nombre y RFC, filtro de activos, orden,
   paginación en servidor, exportación a CSV.
   Alta y edición con el patrón de BarraHerramientasCatalogo.
   Detalle que muestre los datos fiscales tal como van a viajar al CFDI, para que
   el capturista pueda compararlos contra la constancia de un vistazo.

IMPLEMENTA IServicioClientes del contrato. ObtenerParaTimbradoAsync devuelve la
foto fiscal completa lista para congelar en el comprobante: nombre, RFC, régimen,
CP, uso. No devuelve la entidad: devuelve el DTO del contrato.

PRUEBA OBLIGATORIA
   Validación de RFC y dígito verificador, con al menos: dos RFC de persona moral
   válidos, dos de persona física válidos, los dos genéricos, uno con homoclave
   mal calculada y uno con formato inválido.

Al terminar detente y dime qué construiste y qué casos de normalización de nombre
te dejaron dudas.
```

---

## Fase 6 — Productos y servicios

▸ PROMPT

```
Fase 6: catálogo de productos y servicios. Referencia funcional: §5 y §6 del
documento.

MODELO
   Código interno consecutivo por empresa.
   Clave del producto o servicio del SAT (c_ClaveProdServ) — obligatoria, elegida
   con BuscadorCatalogo, nunca escrita a mano.
   Clave de unidad del SAT (c_ClaveUnidad) — obligatoria, igual.
   Unidad de medida en texto libre (la que ve el cliente en el PDF).
   Descripción, precio de venta, peso en kilogramos.
   Objeto de impuesto (c_ObjetoImp).
   Configuración de impuestos del producto: traslado de IVA con su tasa (16 %,
   8 %, 0 %) o exento, y si aplica retención.

   Sobre el legacy: §6 usa un grupo de radios con IVA 16 %, IVA 0 % y exento.
   Eso no basta para 4.0. Modela impuesto, tipo de factor y tasa o cuota como
   campos del catálogo del SAT, y presenta al usuario las tres opciones comunes
   como atajos. Que la interfaz sea simple no obliga a que el modelo sea pobre.

PANTALLAS
   Lista con TablaDatos: código, descripción, unidad, precio, clave SAT.
   Búsqueda en vivo, filtro de activos, exportación a CSV.
   Alta y edición con el patrón de catálogo. Al elegir la clave del SAT se muestra
   su descripción resuelta al lado, como en el legacy.
   Importación desde CSV con vista previa, validación renglón por renglón y
   reporte de errores antes de confirmar. Los contadores van a llegar con un
   Excel de mil productos; sin esto, el sistema no se adopta.

IMPLEMENTA IServicioProductos del contrato.

Al terminar detente y dime qué construiste y cómo modelaste los impuestos del
producto.
```

---

## Fase 7 — Membresías, paquetes y bolsa de timbres · **Opus**

▸ PROMPT

```
Lee ARQUITECTURA.md §4. Fase 7: el dinero. Aquí un error se convierte en cobros
duplicados o en timbres regalados, así que todo lleva idempotencia y bitácora.

1. MODELO
   Paquete: nombre, cantidad de timbres, precio por timbre, precio total, vigencia,
   activo. Ejemplos reales: 500 timbres a 2.00, 1000 a 1.80.
   Membresía: anualidad de suscripción por cuenta, con inicio, fin y estado.
   BolsaTimbres: por EMPRESA, no por cuenta. Saldo disponible y saldo reservado.
   MovimientoTimbre: cada compra, reserva, consumo, devolución y ajuste, con su
   motivo, su usuario y su momento. El saldo es la suma de los movimientos, no un
   número que se edita.
   ReservaTimbre: id, comprobante, estado (reservado, confirmado, devuelto),
   momento de creación y de resolución.

   Decide y explícame: si una cuenta tiene tres empresas, ¿la bolsa es por empresa
   o por cuenta? Mi decisión es POR EMPRESA, para que el gasto de la llantera no
   consuma los timbres de la cementera. Impleméntalo así salvo que veas un
   problema, y si lo ves, dímelo antes.

2. COMPRA
   POST /timbres/comprar recibe solo el id del paquete. El precio lo pone el
   servidor, siempre. Idempotency-Key obligatorio.
   Registra la compra, aumenta el saldo, deja bitácora.
   La pasarela de pago real no está en el MVP: la compra queda en estado
   pendiente_de_pago y un administrador la confirma a mano. Dime si crees que eso
   bloquea algo del negocio.

3. RESERVA Y CONSUMO — implementa IServicioTimbres
   ReservarAsync: transacción CORTA que baja disponible y sube reservado. Si no
   hay saldo, error de negocio claro, no excepción.
   ConfirmarAsync: baja reservado, registra consumo.
   DevolverAsync: baja reservado, sube disponible, registra el motivo.
   Un proceso en segundo plano que devuelve las reservas que llevan más de 30
   minutos sin resolverse, dejando registro. Sin esto, un timbrado que muere a la
   mitad congela timbres para siempre.
   Nunca una llamada HTTP dentro de estas transacciones.

4. PANTALLAS
   Tarjeta de saldo de timbres, visible desde la barra superior.
   Página de compra con los paquetes activos, precio por timbre y total.
   Historial de movimientos con filtros y exportación.
   Estado de la membresía y aviso de vencimiento próximo.
   Todo protegido con el permiso comprar_timbres.

PRUEBAS OBLIGATORIAS
   Aritmética de la bolsa: una secuencia de compra, reserva, confirmación,
   reserva, devolución deja el saldo correcto.
   Idempotencia: dos POST de compra con la misma Idempotency-Key producen una
   sola compra y la misma respuesta.
   Concurrencia: reservar el último timbre desde dos hilos a la vez no deja
   saldo negativo.

Al terminar detente y dime qué construiste, y qué pasa exactamente si el proceso
muere entre ReservarAsync y ConfirmarAsync.
```

---

## Fase 8 — Usuarios, permisos e invitaciones

▸ PROMPT

```
Fase 8: usuarios y permisos. Referencia funcional: §9 del documento, ampliada.

REGLAS DE NEGOCIO
   Cada empresa tiene un administrador que puede invitar hasta 2 auxiliares.
   El límite se valida en el servidor, no solo en la interfaz.
   Los permisos se asignan POR USUARIO Y POR EMPRESA: el mismo usuario puede ser
   administrador en la llantera y auxiliar en la cementera.
   Los seis permisos son los de ARQUITECTURA.md. Un auxiliar típico lleva timbrar y
   nada más.
   Baja lógica con Activo = false. Desactivar a un usuario invalida todas sus
   familias de refresh token de inmediato: no puede seguir trabajando hasta que
   expire su access token.

INVITACIÓN
   El administrador captura correo y nombre. El sistema envía una invitación con
   token de un solo uso, con vigencia de 72 horas. El invitado fija su contraseña
   al aceptar. El administrador nunca conoce la contraseña de nadie.
   La invitación se puede reenviar y revocar.

PANTALLAS
   Lista de usuarios de la empresa activa con su estado y sus permisos.
   Alta por invitación, edición de permisos con casillas, desactivación.
   Perfil propio: nombre, cambio de contraseña, preferencia de tema.
   Todo bajo el permiso administrar_usuarios, salvo el perfil propio.

Al terminar detente y dime qué construiste y cómo manejaste el caso de un usuario
que pertenece a varias empresas con permisos distintos.
```

---

## Fase 9 — Tablero, integración y endurecimiento · **Opus**

▸ PROMPT

```
Fase 9: la última de la mitad A. Cierra el sistema y lo deja listo para
integrarse con la mitad B.

1. TABLERO
   Sustituye el doble de IResumenDocumentos por la implementación real de la
   mitad B. Si todavía no existe, dímelo y no inventes datos.
   Widget de timbres restantes con aviso cuando queden menos de 50.
   Conteo de comprobantes por estatus del periodo.
   Accesos rápidos a nueva factura, clientes y productos.
   Aviso de vencimiento de certificado y de membresía.

2. INTEGRACIÓN
   Revisa que todas las implementaciones de Shared/Contratos estén registradas y
   que ningún doble de prueba quede registrado fuera de Development. Escribe una
   verificación al arrancar que falle ruidosamente si un doble se cuela en
   producción.

3. ENDURECIMIENTO — repasa y corrige lo que encuentres
   Que ningún endpoint reciba empresaId desde el cliente.
   Que toda entidad con datos de empresa esté cubierta por el query filter, y que
   todo uso de IgnoreQueryFilters esté justificado por escrito.
   Que ningún endpoint que muta datos carezca de su política de permiso.
   Que los logs no contengan RFC completos, contraseñas, tokens ni certificados.
   Que los archivos no sean accesibles sin autorización ni adivinando la ruta.
   Que el service worker no cachee /api/.
   Que la CSP no tenga unsafe-inline ni unsafe-eval más allá de wasm-unsafe-eval.
   Que los .br se sirvan de verdad.

   Escribe el resultado de este repaso como docs/REPASO-SEGURIDAD.md, con lo que
   encontraste, lo que corregiste y lo que quedó pendiente. Sé honesto: un repaso
   que dice que todo está bien no sirve para nada.

4. DESPLIEGUE
   Documenta en docs/DESPLIEGUE.md: variables de configuración necesarias, cómo
   se provee la clave maestra de Data Protection, cómo se cargan los catálogos
   del SAT, cómo se corre la migración, y cómo se verifica que Brotli funciona.
   Sin secretos en el documento.

Al terminar detente y dime qué encontraste en el repaso de seguridad que no
esperabas.
```

---

## Cómo revisar cada fase antes de aprobarla

Cuatro preguntas, siempre las mismas:

1. ¿Compila sin advertencias nuevas y corre?
2. ¿Tocó algún archivo de la mitad B? (`git diff --name-only` contra la lista de §4
   del reparto)
3. ¿Hay algún `TODO`, algún método que devuelve datos falsos, o algún catch vacío?
4. ¿Lo que dijo que construyó coincide con lo que hay en el diff?

Si alguna falla, se corrige antes de pasar a la siguiente. Una fase mal cerrada se
paga tres fases después, cuando ya no recuerdas por qué está así.
