# Guía de la base de datos del sistema de facturación

Fecha de revisión: 5 de noviembre de 2026.

Esta guía describe el código y la estructura comprobados de FacturacionDev y Facturacion1.1. No contiene registros de clientes, contraseñas ni cadenas de conexión. No se modificó la base para prepararla. La principal tiene 93 tablas: 91 de aplicación/Identity, una de historial de EF y una de diagramas de SSMS. Facturacion1.1 tiene 92 porque no incluye sysdiagrams. Tener la misma estructura no implica tener los mismos registros ni el mismo historial de EF.

## 1. La explicación que puedes dar al comenzar

«El sistema usa una sola base SQL Server compartida. Cada cuenta representa al contratante y puede tener varias empresas emisoras. Clientes, productos y timbres se comparten dentro de su cuenta; comprobantes, series, configuración fiscal y transporte pertenecen a cada empresa. El servidor valida la sesión y los permisos, filtra los datos por su propietario y guarda los cambios con Entity Framework Core. Para conservar el historial, las facturas guardan sus propios datos fiscales y conceptos, en lugar de depender siempre del catálogo actual.»

## 2. Cuenta, empresa, usuario, cliente y operador

- Cuenta: el contrato y agrupación del dueño del sistema; fija límites de usuarios y empresas y posee la bolsa de timbres.
- Empresa: persona física o moral que emite, con su RFC, régimen, CSD, series, moneda y correo propios.
- Usuario titular: usuario de AspNetUsers que registró o recibió la cuenta; administra usuarios y empresas de esa cuenta.
- Usuario interno: capturista o contador creado por el titular; entra solo a empresas autorizadas de la misma cuenta y realiza las acciones concedidas.
- Cliente: receptor de una factura, registrado en Clientes. No es por ese hecho una persona con acceso al sistema.
- Operador: administrador del SaaS, guardado en OperadoresPlataforma. Tiene sesión y permisos distintos de los del usuario cliente.
- Usuario SQL: credencial técnica con la que el backend se conecta al motor SQL. No equivale a ninguno de los anteriores.

Ejemplo: una cuenta pertenece a un dueño con dos empresas, Llantera y Cementera. Puede reutilizar un cliente o producto y gastar timbres de una bolsa compartida. La factura de Llantera conserva el RFC, folio y certificado de Llantera; no se convierte en una factura de Cementera al cambiar de empresa.

## 3. Cómo se comunica y cómo guarda

Navegador con Blazor → API ASP.NET Core → servicio de negocio → AppDbContext/EF Core → SQL Server.

1. El componente captura datos y envía un DTO por HTTP. El navegador no se conecta directamente a SQL ni conoce la contraseña SQL.
2. El servidor valida sesión, permiso, cuenta, empresa, licencias cuando apliquen y datos de negocio. Las comprobaciones del navegador ayudan a capturar, pero no sustituyen las del servidor.
3. El servicio consulta entidades con EF Core. AppDbContext representa una unidad de trabajo de esa operación, no otra base ni otra copia de la información.
4. EF sigue las altas, modificaciones y bajas. SaveChangesAsync produce las instrucciones SQL parametrizadas correspondientes.
5. El interceptor sella CuentaId/EmpresaId para nuevas entidades y bloquea cambios de propietario. El filtro global actúa sobre lecturas ordinarias.
6. Cuando una operación usa una transacción, sus cambios de base se confirman juntos o se revierten. Eso no significa que toda llamada HTTP del sistema sea una única transacción ni que SQL y un servidor SMTP se confirmen de manera atómica.
7. El servicio devuelve el resultado y la interfaz lo muestra. Una vista previa puede calcularse sin guardar; ver un dato en pantalla no prueba que ya exista persistido.

El SQL manual, ExecuteSqlRaw y las consultas que omiten filtros necesitan controles explícitos: el filtro de EF no es una política de seguridad interna de SQL Server. El usuario SQL puede tener acceso técnico mayor que un capturista. No se afirma aislamiento por base separada ni Row-Level Security de SQL.

## 4. Claves y relaciones

- Id: identificador interno, normalmente Guid/uniqueidentifier. Distingue registros; no es el UUID fiscal.
- CuentaId: dueño de los datos compartidos entre las empresas de esa cuenta.
- EmpresaId: emisor dueño de datos que no se comparten entre empresas.
- EmpresaAltaId: procedencia de un cliente/producto compartido; sirve para consultar dónde se dio de alta.
- ComprobanteId: une una cabecera de documento con sus conceptos o datos específicos.
- Clave foránea: relación que SQL hace cumplir cuando está declarada. Una propiedad que termina en Id no necesariamente tiene una restricción física; algunas referencias se validan en el servicio.
- Índice único: evita duplicados incluso con dos solicitudes simultáneas. Por ejemplo, el RFC de cliente se restringe por cuenta, con excepción de los RFC genéricos.
- Activo/Activa: desactivación, que conserva el registro y su historial.
- Version rowversion: detecta ediciones concurrentes donde se utiliza. No es la versión del SAT ni el número de migración.

Relaciones principales:

Cuenta → Empresas; Cuenta → Usuarios; Usuario ↔ Empresa mediante UsuariosEmpresas; Usuario+Empresa → Permisos mediante UsuariosEmpresasPermisos.

Cuenta → Clientes/Productos/Bolsa/Compras. Empresa → Comprobantes/Series/Certificados/Correo/Vehículos. Comprobante → Conceptos → ConceptosImpuestos. Comprobante → complemento específico.

## 5. Aislamiento y permisos

La empresa activa y la cuenta viajan en claims del token. Cambiar la empresa exige validación del servidor y emitir un token nuevo. Tener acceso a una empresa no autoriza una empresa de otra cuenta. Los filtros globales comparan CuentaId o EmpresaId con el contexto autenticado; el sellado protege las escrituras de entidades que usan esas interfaces. Las tablas globales, las pertenencias y los servicios del operador aplican sus controles por separado.

Los usuarios internos tienen el mismo juego de permisos en las empresas asignadas. Aunque la tabla de asignaciones conserva EmpresaId, ServicioDeUsuarios mantiene sincronizadas sus claves por empresa. El titular obtiene sus permisos al construir el token. Ocultar un botón mejora la interfaz; el endpoint y el servicio deben rechazar igualmente la operación no autorizada.

## 6. Qué se guarda al hacer una factura

1. Se crea Comprobantes con Estatus=borrador, empresa y variante. Todavía no hay folio fiscal ni timbre.
2. Se elige Cliente y se capturan conceptos. Se copian RFC, nombre, régimen y CP al comprobante. Se copian descripción, claves SAT, precio y unidades a Conceptos.
3. El servidor recalcula cantidades, descuentos e impuestos y guarda el detalle en ConceptosImpuestos y los totales en Comprobantes. No confía en un total definitivo que envíe el navegador.
4. Si corresponde, guarda DatosNotaria y sus inmuebles/partes, DatosObra, DatosComercioExterior o la rama Carta Porte.
5. Guardar/editar un borrador no crea automáticamente un nuevo producto en Productos. Elegir o cambiar el precio de un concepto tampoco modifica por sí solo el precio del catálogo.
6. Reabrir consulta el documento y sus detalles. Un borrador puede modificarse con las reglas de autoría y permisos; un documento emitido conserva los datos de su emisión.
7. PDF y XML se producen con los generadores del servidor. Un PDF es una representación; no sustituye a los datos ni al XML del CFDI.

Ejemplo: producto a 100, cantidad 2, descuento 20 e IVA 16 %. Importe 200; base 180; IVA 28.80; total 208.80. Productos guarda el precio de referencia. Conceptos guarda 2, 100, 20 y 200; ConceptosImpuestos guarda base 180 e impuesto 28.80; Comprobantes resume los totales. Cambiar mañana Productos a 120 no cambia esa operación.

Los decimales se almacenan generalmente con precisión decimal(18,6); la presentación habitual es de dos decimales. Se utiliza decimal en C#, no float/double para dinero. Los campos DateTime de operación se guardan en UTC y se convierten para mostrar; algunos datos civiles, como la fecha del instrumento notarial, usan date y no representan un instante UTC.

## 7. Timbres, folios y emisión real

Timbre: unidad de saldo comercial. Folio: número interno de una serie. UUID: identificador fiscal devuelto al certificar. Son tres cosas distintas.

En el flujo de emisión preparado se validan los datos, se reservan folio y saldo, se registra un intento, se genera y sella el XML y se llama al PAC fuera de la transacción SQL. Después se registra la respuesta y se resuelven las reservas. Un fallo de comunicación no equivale a un rechazo fiscal: puede necesitar conciliación para no duplicar la operación. No se recicla un folio reservado por un intento fallido.

En la entrega actual el PAC real y los CSD reales están fuera de alcance. El modo Staging permite PDF/XML y correo de borradores con certificado sintético, sin UUID, sin reservar folio y sin consumir timbres. Que el XML pase validaciones de estructura no lo vuelve un CFDI timbrado con validez fiscal. Los tipos N/E del catálogo o las tablas técnicas no implican módulos nuevos habilitados en la entrega.

## 8. Pagos y variantes

- Pago: Comprobantes tipo P → Pagos → PagosDocumentos → PagosDocumentosImpuestos. El monto real está en Pagos; la cabecera fiscal lleva cero. Para pagar una factura debe existir una referencia emitida PPD admitida por el servicio. Los borradores de revisión no se vuelven facturas pagables.
- Notaría: Comprobantes + Conceptos de honorarios + DatosNotaria + InmueblesNotariales + PartesNotariales. El monto de la compraventa no se confunde con el total de honorarios.
- Constructoras: Comprobantes + Conceptos + DatosObra. Se separan total fiscal, estimación comercial y líquido. Los importes calculados pueden reconstruirse a partir de datos y parámetros; no todo resultado visual tiene necesariamente una columna del mismo nombre.
- Comercio Exterior: Comprobantes + Conceptos + DatosComercioExterior.Contenido. El JSON guarda su estructura específica y los servicios validan las reglas y generan el complemento XML.
- Carta Porte: Comprobantes tipo T + TrasladosCartaPorte + ubicaciones/mercancías/remolques. Vehículos y figuras son catálogos; el traslado conserva la información utilizada. El alcance revisado es autotransporte nacional propio.

## 9. Compra y factura del operador

El usuario elige Paquetes; el servidor obtiene sus precios y crea ComprasTimbres con copias históricas. La compra permanece pendiente hasta su acreditación autorizada; no basta pulsar comprar para sumar saldo. Acreditar registra bolsa/movimiento y los datos comerciales relacionados. La pasarela automática no está integrada en la entrega.

La factura de la venta del paquete utiliza ConfiguracionFiscalProveedor y su propio CSD. FacturasDeCompras conserva los datos de proveedor y comprador. SeriesFacturasProveedor e IntentosFacturasProveedor controlan folios, emisión y recuperación en esta rama. La compra, el saldo y la factura son entidades relacionadas, pero tienen estados distintos.

## 10. Qué vive en SQL y qué vive en carpetas

SQL conserva usuarios, cuentas, empresas, catálogos, datos de documentos, importes, estados, bitácora e historial de operaciones. Algunas columnas contienen JSON y algunos intentos conservan XML. CertificadosProveedor guarda CER/KEY cifrados en la propia base.

El almacén de archivos fuera de wwwroot conserva archivos del sistema, incluidos los CSD de empresas y el XML emitido protegido. CertificadosCsd guarda sus rutas y contraseña protegida. El PDF se genera desde los datos y salidas del documento; no hay una tabla universal que almacene todos los PDF como imágenes. Las descargas pasan por endpoints autorizados.

En la entrega IIS preparada, cnf/appsettings.Instalacion.json guarda la conexión capturada por el asistente. El modo explícito de revisión admite esa conexión legible; eso no elimina el cifrado de CSD o contraseñas SMTP. cnf/appsettings.ArranqueRevision.json y los llaveros conservan parámetros internos y material de protección. La ruta real depende de la configuración del alojamiento, y existe compatibilidad con instalaciones anteriores. web.config configura el proceso IIS, entorno y hostname; no es la tabla de usuarios ni el lugar general de los datos de facturación.

Respaldar la base no sustituye respaldar cnf, los llaveros y el almacén. Perder la clave de protección puede impedir leer secretos aunque se restaure SQL correctamente. Reemplazar la publicación no debe borrar los datos privados del servidor.

## 11. Cambios y bajas

Desactivar conserva un registro para historial y posible reactivación. Eliminar físicamente solo procede donde lo permite el negocio: por ejemplo borradores nunca timbrados y catálogos sin uso comprobado, con contraseña cuando se exige y bitácora. SQL tiene relaciones y restricciones, pero las condiciones de negocio adicionales se comprueban en los servicios. No se recomienda borrar filas manualmente desde SSMS para operar el sistema.

Cambiar una interfaz no siempre cambia SQL. Agregar una propiedad persistente, tabla, relación o índice sí requiere sincronizar modelo y esquema. Un script de actualización modifica una base existente; no debe reemplazarla por una copia vacía. __EFMigrationsHistory y la estructura real pueden diferir cuando la entrega se actualiza con un script independiente: no inventar filas de historial ni ejecutar migraciones iniciales sin revisar.

## 12. Diccionario completo de tablas

El inventario siguiente incluye todas las tablas de la principal. Las relaciones listadas son claves foráneas físicas verificadas; cuando no se listan, puede haber referencias validadas por el código. No hay registros privados en esta guía. Los tipos y la nulabilidad proceden del esquema inspeccionado.

### Cuenta y usuarios

#### 1. Cuentas

**Qué guarda:** Representa al contratante del SaaS: nombre, correo de contacto, estado y límites de usuarios y empresas.

**Cómo se usa y por qué:** Es la raíz comercial. Una cuenta puede tener varias empresas; no es una factura ni un usuario de SQL. Sus límites los administra el operador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Activa [bit, obligatorio]; CorreoContacto [nvarchar(254), obligatorio]; FechaAltaUtc [datetime2, obligatorio]; Id [uniqueidentifier, obligatorio]; LimiteDeEmpresas [int, obligatorio]; LimiteDeUsuarios [int, obligatorio]; Nombre [nvarchar(254), obligatorio].

#### 2. Empresas

**Qué guarda:** Guarda al emisor: RFC, nombre fiscal, régimen, código postal, zona horaria, domicilios, contacto, logo y licencias.

**Cómo se usa y por qué:** CuentaId indica a qué contratante pertenece. Cada empresa mantiene comprobantes, series, vehículos y configuración propios. Las licencias habilitan variantes; no equivalen a permisos de usuario.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas.

**Columnas verificadas:** Activa [bit, obligatorio]; Calle [nvarchar(128), admite nulo]; CartaPorteCalle [nvarchar(128), admite nulo]; CartaPorteCodigoPostal [nvarchar(5), admite nulo]; CartaPorteEstado [nvarchar(3), admite nulo]; CartaPorteMunicipio [nvarchar(3), admite nulo]; CartaPorteNumeroExterior [nvarchar(32), admite nulo]; CartaPorteNumeroInterior [nvarchar(32), admite nulo]; CodigoPostal [nvarchar(5), admite nulo]; CodigoPostalExpedicion [nvarchar(5), obligatorio]; Colonia [nvarchar(128), admite nulo]; CorreoContacto [nvarchar(254), admite nulo]; CuentaId [uniqueidentifier, obligatorio]; Estado [nvarchar(128), admite nulo]; FechaAltaUtc [datetime2, obligatorio]; Id [uniqueidentifier, obligatorio]; LicComercio [bit, obligatorio]; LicINE [bit, obligatorio]; LicNotarios [bit, obligatorio]; LicObras [bit, obligatorio]; Localidad [nvarchar(128), admite nulo]; LogoNombreOriginal [nvarchar(256), admite nulo]; LogoRuta [nvarchar(256), admite nulo]; LogoTipoMime [nvarchar(64), admite nulo]; Municipio [nvarchar(128), admite nulo]; NombreFiscal [nvarchar(254), obligatorio]; NumeroExterior [nvarchar(32), admite nulo]; NumeroInterior [nvarchar(32), admite nulo]; Pais [nvarchar(64), admite nulo]; Referencia [nvarchar(256), admite nulo]; RegimenFiscal [nvarchar(3), obligatorio]; Rfc [nvarchar(13), obligatorio]; Telefono [nvarchar(32), admite nulo]; ZonaHoraria [nvarchar(64), obligatorio].

#### 3. AspNetUsers

**Qué guarda:** Almacena usuarios del cliente mediante ASP.NET Core Identity: identidad, PasswordHash, bloqueos y campos CuentaId, Nombre, Activo y EsTitular.

**Cómo se usa y por qué:** El titular es el dueño de la cuenta; los internos pertenecen a la misma cuenta. La contraseña se verifica con hash, no se recupera descifrándola. No son usuarios SQL ni operadores de plataforma.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas.

**Columnas verificadas:** AccessFailedCount [int, obligatorio]; Activo [bit, obligatorio]; BloqueosConsecutivos [int, obligatorio]; ConcurrencyStamp [nvarchar(max), admite nulo]; CreadoPorAdministrador [bit, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Email [nvarchar(256), admite nulo]; EmailConfirmed [bit, obligatorio]; EsTitular [bit, obligatorio]; FechaAltaUtc [datetime2, obligatorio]; Id [uniqueidentifier, obligatorio]; LockoutEnabled [bit, obligatorio]; LockoutEnd [datetimeoffset, admite nulo]; Nombre [nvarchar(254), obligatorio]; NormalizedEmail [nvarchar(256), admite nulo]; NormalizedUserName [nvarchar(256), admite nulo]; PasswordHash [nvarchar(max), admite nulo]; PhoneNumber [nvarchar(max), admite nulo]; PhoneNumberConfirmed [bit, obligatorio]; SecurityStamp [nvarchar(max), admite nulo]; TemaPreferido [nvarchar(16), admite nulo]; TwoFactorEnabled [bit, obligatorio]; UserName [nvarchar(256), admite nulo].

#### 4. UsuariosEmpresas

**Qué guarda:** Relaciona un usuario con cada empresa a la que tiene acceso, con estado y fecha de alta.

**Cómo se usa y por qué:** Resuelve la relación muchos a muchos: un usuario entra a varias empresas y una empresa admite varios usuarios. El servidor valida que las empresas elegidas pertenezcan a su cuenta.

**Relaciones físicas:** UsuarioId=Id → dbo.AspNetUsers; EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; FechaAltaUtc [datetime2, obligatorio]; UsuarioId [uniqueidentifier, obligatorio].

#### 5. Permisos

**Qué guarda:** Catálogo de las 66 acciones asignables del cliente, con clave y descripción.

**Cómo se usa y por qué:** Define qué acciones existen; no guarda directamente quién puede ejecutarlas. No es una tabla de roles ni el catálogo de permisos del operador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(32), obligatorio]; Descripcion [nvarchar(128), obligatorio].

#### 6. UsuariosEmpresasPermisos

**Qué guarda:** Asigna una clave de permiso a un usuario en una empresa; guarda quién y cuándo la otorgó.

**Cómo se usa y por qué:** El esquema conserva EmpresaId, pero el servicio sincroniza el mismo juego de permisos en las empresas asignadas al usuario. Los permisos implícitos y los del titular se calculan al emitir el token, no todos necesitan filas aquí.

**Relaciones físicas:** PermisoClave=Clave → dbo.Permisos; UsuarioId=UsuarioId|EmpresaId=EmpresaId → dbo.UsuariosEmpresas.

**Columnas verificadas:** EmpresaId [uniqueidentifier, obligatorio]; OtorgadoPorUsuarioId [uniqueidentifier, admite nulo]; OtorgadoUtc [datetime2, obligatorio]; PermisoClave [nvarchar(32), obligatorio]; UsuarioId [uniqueidentifier, obligatorio].

#### 7. AltasPendientes

**Qué guarda:** Guarda solicitudes de registro aún sin confirmar: correo, nombre, cuenta solicitada, hash del código, caducidad e intentos.

**Cómo se usa y por qué:** Permite comprobar el código enviado antes de completar el registro. El código se compara por hash; una solicitud pendiente no significa que ya exista una cuenta activa.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** ConsumidoUtc [datetime2, admite nulo]; Correo [nvarchar(254), obligatorio]; CreadoUtc [datetime2, obligatorio]; ExpiraUtc [datetime2, obligatorio]; HashCodigo [nvarchar(256), obligatorio]; Id [uniqueidentifier, obligatorio]; Intentos [int, obligatorio]; IpCreacion [nvarchar(45), admite nulo]; Nombre [nvarchar(128), obligatorio]; NombreCuenta [nvarchar(200), obligatorio].

#### 8. AspNetUserClaims

**Qué guarda:** Declaraciones persistentes opcionales asociadas a usuarios.

**Cómo se usa y por qué:** Son tablas auxiliares creadas por Identity. Su existencia no demuestra que haya inicio de sesión con Google u otro proveedor implementado. No confundirlas con los JWT en memoria ni con la rotación de RefreshTokens.

**Relaciones físicas:** UserId=Id → dbo.AspNetUsers.

**Columnas verificadas:** ClaimType [nvarchar(max), admite nulo]; ClaimValue [nvarchar(max), admite nulo]; Id [int, obligatorio]; UserId [uniqueidentifier, obligatorio].

#### 9. AspNetUserLogins

**Qué guarda:** Asociaciones opcionales a proveedores externos de inicio de sesión.

**Cómo se usa y por qué:** Son tablas auxiliares creadas por Identity. Su existencia no demuestra que haya inicio de sesión con Google u otro proveedor implementado. No confundirlas con los JWT en memoria ni con la rotación de RefreshTokens.

**Relaciones físicas:** UserId=Id → dbo.AspNetUsers.

**Columnas verificadas:** LoginProvider [nvarchar(450), obligatorio]; ProviderDisplayName [nvarchar(max), admite nulo]; ProviderKey [nvarchar(450), obligatorio]; UserId [uniqueidentifier, obligatorio].

#### 10. AspNetUserTokens

**Qué guarda:** Tokens de proveedores y mecanismos de Identity, identificados por usuario, proveedor y nombre.

**Cómo se usa y por qué:** Son tablas auxiliares creadas por Identity. Su existencia no demuestra que haya inicio de sesión con Google u otro proveedor implementado. No confundirlas con los JWT en memoria ni con la rotación de RefreshTokens.

**Relaciones físicas:** UserId=Id → dbo.AspNetUsers.

**Columnas verificadas:** LoginProvider [nvarchar(450), obligatorio]; Name [nvarchar(450), obligatorio]; UserId [uniqueidentifier, obligatorio]; Value [nvarchar(max), admite nulo].

### Sesiones y operador

#### 11. RefreshTokens

**Qué guarda:** Guarda el hash de renovación de sesión del cliente, usuario, familia, empresa activa, vencimiento, consumo, reemplazo y revocación.

**Cómo se usa y por qué:** El token sin hash viaja en la cookie; no es la contraseña. Cada uso consume uno y crea otro. La familia permite revocar la sesión completa ante reutilización. El access token JWT no se almacena aquí.

**Relaciones físicas:** UsuarioId=Id → dbo.AspNetUsers.

**Columnas verificadas:** AgenteUsuario [nvarchar(256), admite nulo]; ConsumidoUtc [datetime2, admite nulo]; CreadoUtc [datetime2, obligatorio]; EmpresaActivaId [uniqueidentifier, admite nulo]; ExpiraUtc [datetime2, obligatorio]; FamiliaId [uniqueidentifier, obligatorio]; HashToken [nvarchar(64), obligatorio]; Id [uniqueidentifier, obligatorio]; IpCreacion [nvarchar(45), admite nulo]; MotivoRevocacion [nvarchar(128), admite nulo]; ReemplazadoPorId [uniqueidentifier, admite nulo]; RevocadoUtc [datetime2, admite nulo]; UsuarioId [uniqueidentifier, obligatorio].

#### 12. OperadoresPlataforma

**Qué guarda:** Identidad administrativa separada: correo, hash de contraseña, Activo, EsPrincipal, intentos fallidos y bloqueo.

**Cómo se usa y por qué:** El operador administra el SaaS. No es el titular de una empresa. El indicador de principal distingue al administrador de los operadores secundarios.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** AccesosFallidos [int, obligatorio]; Activo [bit, obligatorio]; BloqueadoHastaUtc [datetime2, admite nulo]; BloqueosConsecutivos [int, obligatorio]; Correo [nvarchar(254), obligatorio]; CorreoNormalizado [nvarchar(254), obligatorio]; EsPrincipal [bit, obligatorio]; FechaAltaUtc [datetime2, obligatorio]; HashContrasena [nvarchar(256), obligatorio]; Id [uniqueidentifier, obligatorio]; Nombre [nvarchar(128), obligatorio]; UltimoAccesoUtc [datetime2, admite nulo].

#### 13. OperadoresPermisos

**Qué guarda:** Guarda acciones concedidas a un operador secundario.

**Cómo se usa y por qué:** La clave es la combinación de operador y permiso. Mantiene separada la autorización de administración del catálogo de permisos de los clientes.

**Relaciones físicas:** OperadorId=Id → dbo.OperadoresPlataforma.

**Columnas verificadas:** OperadorId [uniqueidentifier, obligatorio]; Permiso [nvarchar(64), obligatorio].

#### 14. RefreshTokensOperador

**Qué guarda:** Sesiones renovables de operadores con hash, familia, fechas y revocaciones.

**Cómo se usa y por qué:** Aplica rotación de forma separada a las sesiones del cliente; iniciar sesión como operador no concede por sí mismo una sesión de cliente.

**Relaciones físicas:** OperadorId=Id → dbo.OperadoresPlataforma.

**Columnas verificadas:** AgenteUsuario [nvarchar(256), admite nulo]; ConsumidoUtc [datetime2, admite nulo]; CreadoUtc [datetime2, obligatorio]; ExpiraUtc [datetime2, obligatorio]; FamiliaId [uniqueidentifier, obligatorio]; HashToken [nvarchar(64), obligatorio]; Id [uniqueidentifier, obligatorio]; IpCreacion [nvarchar(45), admite nulo]; MotivoRevocacion [nvarchar(128), admite nulo]; OperadorId [uniqueidentifier, obligatorio]; ReemplazadoPorId [uniqueidentifier, admite nulo]; RevocadoUtc [datetime2, admite nulo].

### Configuración y certificados

#### 15. ConfiguracionDelSistema

**Qué guarda:** Configuración global del SaaS: SMTP del sistema, remitente, nombre del sistema y plantillas de registro y recuperación.

**Cómo se usa y por qué:** Se administra desde operador. El SMTP de esta tabla envía correos del sistema; los comprobantes de clientes utilizan CorreosDeEmpresa. La contraseña SMTP se guarda cifrada.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** ActualizadaPorOperadorId [uniqueidentifier, obligatorio]; ActualizadaUtc [datetime2, obligatorio]; AsuntoContrasena [nvarchar(128), obligatorio]; AsuntoVerificacion [nvarchar(128), obligatorio]; ContrasenaSmtpCifrada [nvarchar(2048), admite nulo]; CuerpoContrasena [nvarchar(max), obligatorio]; CuerpoVerificacion [nvarchar(max), obligatorio]; Id [tinyint, obligatorio]; NombreDelSistema [nvarchar(128), obligatorio]; PuertoSmtp [int, obligatorio]; RemitenteCorreo [nvarchar(254), obligatorio]; RemitenteNombre [nvarchar(128), obligatorio]; ServidorSmtp [nvarchar(253), obligatorio]; UsarTls [bit, obligatorio]; UsuarioSmtp [nvarchar(254), obligatorio].

#### 16. ConfiguracionesEmpresa

**Qué guarda:** Valores iniciales de IVA, IEPS, retenciones, ISH y aviso de vencimiento del CSD.

**Cómo se usa y por qué:** Es configuración por empresa. Precarga capturas, pero no cambia retroactivamente los impuestos ya guardados en un comprobante.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** CodigoCorreoRespuestaExpiraUtc [datetime2, admite nulo]; CodigoCorreoRespuestaSolicitadoUtc [datetime2, admite nulo]; CorreoRespuestaPendiente [nvarchar(254), admite nulo]; CorreoRespuestaVerificado [nvarchar(254), admite nulo]; DiasAvisoCaducidadCertificado [int, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; HashCodigoCorreoRespuesta [nvarchar(256), admite nulo]; IntentosCodigoCorreoRespuesta [int, obligatorio]; SolicitudesCorreoRespuestaEnVentana [int, obligatorio]; TasaIepsPorDefecto [decimal(18,6), obligatorio]; TasaIshPorDefecto [decimal(18,6), obligatorio]; TasaIvaPorDefecto [decimal(18,6), obligatorio]; TasaRetencionIepsPorDefecto [decimal(18,6), obligatorio]; TasaRetencionIsrPorDefecto [decimal(18,6), obligatorio]; TasaRetencionIvaPorDefecto [decimal(18,6), obligatorio]; VentanaSolicitudesCorreoRespuestaUtc [datetime2, admite nulo]; VersionFila [timestamp, obligatorio].

#### 17. MonedasDeEmpresa

**Qué guarda:** Lista de monedas SAT habilitadas en cada empresa, con Activa y fecha de cambio.

**Cómo se usa y por qué:** SatMoneda contiene el catálogo general; esta tabla indica cuáles usa una empresa. Desactivar una moneda no cambia la moneda de documentos anteriores.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activa [bit, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; ModificadaUtc [datetime2, obligatorio]; Moneda [varchar(3), obligatorio].

#### 18. CorreosDeEmpresa

**Qué guarda:** Servidor SMTP, puerto, usuario, remitente y contraseña cifrada para enviar los documentos del emisor.

**Cómo se usa y por qué:** Una empresa no toma prestado en silencio el correo del SaaS. Si falta configuración o falla la conexión, el envío informa el fallo. La contraseña no vuelve al navegador.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** ContrasenaCifrada [nvarchar(2048), admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; Puerto [int, obligatorio]; RemitenteCorreo [nvarchar(254), admite nulo]; RemitenteNombre [nvarchar(128), admite nulo]; Servidor [nvarchar(253), admite nulo]; Usuario [nvarchar(254), admite nulo].

#### 19. CertificadosCsd

**Qué guarda:** Metadatos del CSD del emisor: número de serie, vigencia, rutas CER/KEY, contraseña cifrada y estado.

**Cómo se usa y por qué:** Los archivos del certificado de empresa se guardan cifrados en el almacén privado; la tabla conserva sus referencias. Es el CSD de la empresa, no una credencial del PAC.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; CargadoPorUsuarioId [uniqueidentifier, obligatorio]; ContrasenaCifrada [nvarchar(1024), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; FechaCargaUtc [datetime2, obligatorio]; Id [uniqueidentifier, obligatorio]; NumeroSerie [nvarchar(20), obligatorio]; RutaCer [nvarchar(256), obligatorio]; RutaKey [nvarchar(256), obligatorio]; VigenciaDesdeUtc [datetime2, obligatorio]; VigenciaHastaUtc [datetime2, obligatorio].

### Clientes y productos

#### 20. Clientes

**Qué guarda:** Catálogo de receptores de la cuenta: RFC, nombre, régimen, CP fiscal, contacto, dirección y preferencias de pago.

**Cómo se usa y por qué:** CuentaId permite compartirlos entre empresas de la misma cuenta. EmpresaAltaId identifica el origen para el filtro de alta, no la propiedad exclusiva. Modificar este catálogo cambia futuras capturas, no las copias históricas de facturas emitidas.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas; EmpresaAltaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; Calle [nvarchar(128), admite nulo]; ClaveInterna [int, obligatorio]; CodigoPostal [nvarchar(5), admite nulo]; Colonia [nvarchar(128), admite nulo]; CorreoPrincipal [nvarchar(254), admite nulo]; CuentaId [uniqueidentifier, obligatorio]; DomicilioFiscalCp [nvarchar(5), obligatorio]; EmpresaAltaId [uniqueidentifier, admite nulo]; Estado [nvarchar(128), admite nulo]; FechaAltaUtc [datetime2, obligatorio]; FechaModificacionUtc [datetime2, admite nulo]; FormaPagoPreferida [nvarchar(4), admite nulo]; Id [uniqueidentifier, obligatorio]; Localidad [nvarchar(128), admite nulo]; MetodoPagoPreferido [nvarchar(3), admite nulo]; Municipio [nvarchar(128), admite nulo]; Nombre [nvarchar(254), obligatorio]; NumeroExterior [nvarchar(32), admite nulo]; NumeroInterior [nvarchar(32), admite nulo]; NumRegIdTrib [nvarchar(40), admite nulo]; Pais [nvarchar(64), admite nulo]; Referencia [nvarchar(256), admite nulo]; RegimenFiscal [nvarchar(3), obligatorio]; ResidenciaFiscal [nvarchar(3), admite nulo]; Rfc [nvarchar(13), obligatorio]; Telefono [nvarchar(32), admite nulo]; UsoCfdiPreferido [nvarchar(4), admite nulo].

#### 21. Productos

**Qué guarda:** Catálogo compartido de productos y servicios: código, nombre facturable, detalle, claves SAT, unidad, precio, peso y objeto de impuesto.

**Cómo se usa y por qué:** Descripción es el nombre facturable; Detalle amplía el texto. El precio del catálogo precarga el concepto, cuyo precio puede ajustarse en la factura. No representa existencias: no se implementó inventario.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas; EmpresaAltaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; ClaveProdServ [nvarchar(8), obligatorio]; ClaveUnidad [nvarchar(20), obligatorio]; CodigoInterno [int, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Descripcion [nvarchar(1000), obligatorio]; Detalle [nvarchar(1000), admite nulo]; EmpresaAltaId [uniqueidentifier, admite nulo]; FechaAltaUtc [datetime2, obligatorio]; FechaModificacionUtc [datetime2, admite nulo]; Id [uniqueidentifier, obligatorio]; IdInterno [nvarchar(100), admite nulo]; ObjetoImp [nvarchar(2), obligatorio]; PesoKg [decimal(18,6), admite nulo]; UnidadTexto [nvarchar(64), obligatorio]; ValorUnitario [decimal(18,6), obligatorio].

#### 22. ProductosImpuestos

**Qué guarda:** Configuración de los impuestos predeterminados de cada producto: impuesto, factor, tasa y retención.

**Cómo se usa y por qué:** Un producto admite varios impuestos. Son reglas iniciales sin los importes definitivos de cada operación; los impuestos calculados de una factura se guardan en ConceptosImpuestos.

**Relaciones físicas:** ProductoId=Id → dbo.Productos.

**Columnas verificadas:** CuentaId [uniqueidentifier, obligatorio]; EsRetencion [bit, obligatorio]; Id [uniqueidentifier, obligatorio]; Impuesto [nvarchar(3), obligatorio]; ProductoId [uniqueidentifier, obligatorio]; TasaOCuota [decimal(18,6), admite nulo]; TipoFactor [nvarchar(16), obligatorio].

### Documentos

#### 23. Comprobantes

**Qué guarda:** Cabecera común: empresa emisora, tipo, variante, estado, fechas, receptor copiado, importes, serie/folio, UUID y datos de certificación.

**Cómo se usa y por qué:** Factura estándar, Notaría, Obra y Comercio Exterior comparten esta cabecera. Pagos y traslados también son comprobantes con ramas propias. TipoDeComprobante no es lo mismo que Variante. La tabla guarda datos, no una imagen del PDF.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** CadenaOriginalSat [nvarchar(max), admite nulo]; ClienteId [uniqueidentifier, admite nulo]; CondicionesDePago [nvarchar(1000), admite nulo]; CreadoPorUsuarioId [uniqueidentifier, obligatorio]; CreadoUtc [datetime2, obligatorio]; Descuento [decimal(18,6), obligatorio]; EmisorNombre [nvarchar(254), obligatorio]; EmisorRegimenFiscal [nvarchar(3), obligatorio]; EmisorRfc [nvarchar(13), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estatus [nvarchar(16), obligatorio]; Exportacion [nvarchar(2), obligatorio]; FechaEmisionUtc [datetime2, obligatorio]; FechaTimbradoUtc [datetime2, admite nulo]; Folio [int, admite nulo]; FormaPago [nvarchar(2), admite nulo]; GlobalAnio [int, admite nulo]; GlobalMeses [nvarchar(2), admite nulo]; GlobalPeriodicidad [nvarchar(2), admite nulo]; Id [uniqueidentifier, obligatorio]; LugarExpedicion [nvarchar(5), obligatorio]; MetodoPago [nvarchar(3), admite nulo]; ModificadoUtc [datetime2, admite nulo]; Moneda [nvarchar(3), obligatorio]; NoCertificadoEmisor [nvarchar(20), admite nulo]; NoCertificadoSat [nvarchar(20), admite nulo]; Observaciones [nvarchar(2000), admite nulo]; ReceptorDomicilioFiscal [nvarchar(5), obligatorio]; ReceptorNombre [nvarchar(254), obligatorio]; ReceptorNumRegIdTrib [nvarchar(40), admite nulo]; ReceptorRegimenFiscal [nvarchar(3), obligatorio]; ReceptorResidenciaFiscal [nvarchar(3), admite nulo]; ReceptorRfc [nvarchar(13), obligatorio]; ReceptorUsoCfdi [nvarchar(4), obligatorio]; RutaXml [nvarchar(400), admite nulo]; SelloCfd [nvarchar(max), admite nulo]; SelloSat [nvarchar(max), admite nulo]; Serie [nvarchar(25), admite nulo]; SerieId [uniqueidentifier, admite nulo]; SubTotal [decimal(18,6), obligatorio]; TasaIsh [decimal(18,6), admite nulo]; TipoCambio [decimal(18,6), admite nulo]; TipoDeComprobante [nvarchar(1), obligatorio]; Total [decimal(18,6), obligatorio]; TotalImpuestosRetenidos [decimal(18,6), obligatorio]; TotalImpuestosTrasladados [decimal(18,6), obligatorio]; Uuid [uniqueidentifier, admite nulo]; Variante [nvarchar(20), admite nulo].

#### 24. Conceptos

**Qué guarda:** Renglones del comprobante: descripción, claves SAT, cantidad, precio, descuento, importe, objeto de impuesto y orden.

**Cómo se usa y por qué:** ProductoId sirve para rastrear el origen. Los valores del renglón son copias de esa operación; cambiar Productos no reescribe el documento. DatosFiscalesJson es un campo opcional de compatibilidad cuya captura adicional se retiró de esta entrega.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** AplicaIsh [bit, obligatorio]; Cantidad [decimal(18,6), obligatorio]; ClaveProdServ [nvarchar(8), obligatorio]; ClaveUnidad [nvarchar(20), obligatorio]; ComprobanteId [uniqueidentifier, obligatorio]; DatosFiscalesJson [nvarchar(max), admite nulo]; Descripcion [nvarchar(1000), obligatorio]; Descuento [decimal(18,6), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; Importe [decimal(18,6), obligatorio]; NoIdentificacion [nvarchar(100), admite nulo]; ObjetoImp [nvarchar(2), obligatorio]; Orden [int, obligatorio]; ProductoId [uniqueidentifier, admite nulo]; UnidadTexto [nvarchar(64), admite nulo]; ValorUnitario [decimal(18,6), obligatorio].

#### 25. ConceptosImpuestos

**Qué guarda:** Impuestos calculados de cada renglón: base, tasa/cuota, importe, factor y si es retención.

**Cómo se usa y por qué:** Se relaciona con Conceptos. Se separa porque un renglón puede tener más de un impuesto. Exento puede tener tasa e importe nulos; no equivale a una tasa numérica cero.

**Relaciones físicas:** ConceptoId=Id → dbo.Conceptos.

**Columnas verificadas:** Base [decimal(18,6), obligatorio]; ConceptoId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EsRetencion [bit, obligatorio]; Id [uniqueidentifier, obligatorio]; Importe [decimal(18,6), admite nulo]; Impuesto [nvarchar(3), obligatorio]; TasaOCuota [decimal(18,6), admite nulo]; TipoFactor [nvarchar(16), obligatorio].

#### 26. ComprobantesRelacionados

**Qué guarda:** Tipo de relación y UUID de otros CFDI que se relacionan con el comprobante.

**Cómo se usa y por qué:** Se guarda el UUID porque el documento relacionado puede provenir de otro sistema y no existir en esta base. No todos esos UUID tienen una llave foránea a Comprobantes.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** ComprobanteId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; TipoRelacion [nvarchar(2), obligatorio]; UuidRelacionado [uniqueidentifier, obligatorio].

#### 27. Series

**Qué guarda:** Series por empresa y tipo de comprobante: prefijo, folio inicial, folio actual y estado.

**Cómo se usa y por qué:** Las empresas comparten timbres, pero no su numeración. El folio interno no es el UUID fiscal. La asignación concurrente se resuelve en SQL con bloqueo, no buscando MAX+1.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activa [bit, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; FechaAltaUtc [datetime2, obligatorio]; FolioActual [int, obligatorio]; FolioInicial [int, obligatorio]; Id [uniqueidentifier, obligatorio]; Prefijo [nvarchar(25), obligatorio]; TipoComprobante [nvarchar(1), obligatorio].

#### 28. ReservasFolio

**Qué guarda:** Registra el folio apartado, su serie, empresa, comprobante y resolución.

**Cómo se usa y por qué:** Explica por qué un número ya usado no debe reciclarse si falla la emisión. Reservado, confirmado y abandonado describen la reserva, no el estado completo del CFDI.

**Relaciones físicas:** SerieId=Id → dbo.Series.

**Columnas verificadas:** ComprobanteId [uniqueidentifier, admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(16), obligatorio]; Folio [int, obligatorio]; Id [uniqueidentifier, obligatorio]; MomentoResolucionUtc [datetime2, admite nulo]; MomentoUtc [datetime2, obligatorio]; SerieId [uniqueidentifier, obligatorio].

#### 29. IntentosTimbrado

**Qué guarda:** Historial de cada intento: número, resultado, tiempos, clave de idempotencia, XML enviado y errores.

**Cómo se usa y por qué:** Un comprobante puede tener varios intentos. Se conserva el XML exacto enviado para recuperación; regenerarlo con otro CSD podría producir un archivo distinto. El PAC real sigue pendiente de integración en esta entrega.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** ClaveIdempotencia [nvarchar(128), admite nulo]; CodigoError [nvarchar(32), admite nulo]; ComprobanteId [uniqueidentifier, obligatorio]; DuracionMs [int, admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; IniciadoUtc [datetime2, obligatorio]; MensajeError [nvarchar(2000), admite nulo]; Numero [int, obligatorio]; Resultado [nvarchar(32), obligatorio]; TerminadoUtc [datetime2, admite nulo]; XmlEnviado [nvarchar(max), admite nulo].

#### 30. SolicitudesCancelacion

**Qué guarda:** Motivo, sustitución cuando corresponda, estado, fechas y respuesta de la solicitud de cancelación.

**Cómo se usa y por qué:** Solicitar cancelar y haber cancelado no son lo mismo. La solicitud tiene su seguimiento propio y el comprobante conserva su estado. La operación real depende del PAC/SAT, fuera de la demostración actual.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** CodigoRespuesta [nvarchar(32), admite nulo]; ComprobanteId [uniqueidentifier, obligatorio]; ConsultadaUtc [datetime2, admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; EsCancelableSat [nvarchar(64), admite nulo]; Estado [nvarchar(32), obligatorio]; EstadoCfdiSat [nvarchar(32), admite nulo]; EstatusCancelacionSat [nvarchar(64), admite nulo]; Id [uniqueidentifier, obligatorio]; MensajeRespuesta [nvarchar(2000), admite nulo]; Motivo [nvarchar(2), obligatorio]; ResueltaUtc [datetime2, admite nulo]; SolicitadaPorUsuarioId [uniqueidentifier, obligatorio]; SolicitadaUtc [datetime2, obligatorio]; UuidSustituye [uniqueidentifier, admite nulo].

#### 31. EnviosDeCorreo

**Qué guarda:** Historial de envíos: comprobante, destinatarios, copia oculta, asunto, XML incluido, resultado, usuario y fecha.

**Cómo se usa y por qué:** Se registran éxitos y fallos. Exitoso indica que el servidor SMTP aceptó el envío; la entrega final al buzón se confirma aparte. No guarda la contraseña SMTP ni sustituye al archivo XML.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** Asunto [nvarchar(200), obligatorio]; ComprobanteId [uniqueidentifier, obligatorio]; CopiaOculta [nvarchar(254), admite nulo]; Destinatarios [nvarchar(2600), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EnviadoPorUsuarioId [uniqueidentifier, obligatorio]; EnviadoUtc [datetime2, obligatorio]; Error [nvarchar(64), admite nulo]; Exitoso [bit, obligatorio]; Id [uniqueidentifier, obligatorio]; IncluyoXml [bit, obligatorio].

### Complemento de pagos

#### 32. Pagos

**Qué guarda:** Datos del pago dentro de un comprobante tipo P: fecha, forma, moneda, tipo de cambio, monto, operación y datos bancarios/SPEI opcionales.

**Cómo se usa y por qué:** El dinero pagado se guarda aquí. En el CFDI raíz de pago el subtotal y total son cero y la moneda es XXX; eso no significa que el abono sea cero. Es distinto de una pasarela que cobra compras del SaaS.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** CadPago [nvarchar(max), admite nulo]; CertPago [nvarchar(max), admite nulo]; ComprobanteId [uniqueidentifier, obligatorio]; CtaBeneficiario [nvarchar(50), admite nulo]; CtaOrdenante [nvarchar(50), admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; FechaPagoUtc [datetime2, obligatorio]; FormaDePagoP [nvarchar(2), obligatorio]; Id [uniqueidentifier, obligatorio]; MonedaP [nvarchar(3), obligatorio]; Monto [decimal(18,6), obligatorio]; NomBancoOrdExt [nvarchar(300), admite nulo]; NumOperacion [nvarchar(100), admite nulo]; RfcEmisorCtaBen [nvarchar(13), admite nulo]; RfcEmisorCtaOrd [nvarchar(13), admite nulo]; SelloPago [nvarchar(max), admite nulo]; TipoCadPago [nvarchar(2), admite nulo]; TipoCambioP [decimal(18,6), admite nulo].

#### 33. PagosDocumentos

**Qué guarda:** Facturas a las que se aplica un pago: UUID, moneda, equivalencia, parcialidad, saldo anterior, importe pagado y saldo insoluto.

**Cómo se usa y por qué:** Un pago puede aplicarse a varios documentos. Conserva los saldos de esa parcialidad, no una consulta cambiante al saldo actual. La referencia interna al comprobante puede ser nula para documentos externos.

**Relaciones físicas:** PagoId=Id → dbo.Pagos.

**Columnas verificadas:** ComprobantePagadoId [uniqueidentifier, admite nulo]; DecimalesMonedaDR [int, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EquivalenciaDR [decimal(28,10), obligatorio]; Folio [nvarchar(40), admite nulo]; Id [uniqueidentifier, obligatorio]; IdDocumento [uniqueidentifier, obligatorio]; ImpPagado [decimal(18,6), obligatorio]; ImpSaldoAnt [decimal(18,6), obligatorio]; ImpSaldoInsoluto [decimal(18,6), obligatorio]; MonedaDR [nvarchar(3), obligatorio]; NumParcialidad [int, obligatorio]; ObjetoImpDR [nvarchar(2), obligatorio]; PagoId [uniqueidentifier, obligatorio]; Serie [nvarchar(25), admite nulo].

#### 34. PagosDocumentosImpuestos

**Qué guarda:** Desglose proporcional de los impuestos correspondientes al documento abonado.

**Cómo se usa y por qué:** Se calcula a partir de los impuestos de la factura y del importe de la parcialidad. No se vuelve a consultar el precio actual del producto para calcular el impuesto de un pago antiguo.

**Relaciones físicas:** DocumentoPagadoId=Id → dbo.PagosDocumentos.

**Columnas verificadas:** Base [decimal(18,6), obligatorio]; DocumentoPagadoId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EsRetencion [bit, obligatorio]; Id [uniqueidentifier, obligatorio]; Importe [decimal(18,6), admite nulo]; Impuesto [nvarchar(3), obligatorio]; TasaOCuota [decimal(18,6), admite nulo]; TipoFactor [nvarchar(16), obligatorio].

### Notaría y obra

#### 35. ConfiguracionesNotario

**Qué guarda:** Perfil del notario por empresa: CURP, número de notaría, estado y adscripción.

**Cómo se usa y por qué:** Es el perfil reutilizable. No equivale a una operación notarial concreta y sus cambios no deben alterar los datos copiados de una operación emitida.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Adscripcion [nvarchar(255), admite nulo]; Curp [nvarchar(18), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(3), obligatorio]; ModificadoUtc [datetime2, obligatorio]; NumeroNotaria [int, obligatorio].

#### 36. DatosNotaria

**Qué guarda:** Información de una operación: instrumento, fecha, monto, subtotal, IVA y banderas de copropiedad.

**Cómo se usa y por qué:** Se vincula a Comprobantes. Los importes de la operación inmobiliaria se distinguen de los honorarios que forman los conceptos de la factura.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** AdquirentesEnCopropiedad [bit, obligatorio]; ComprobanteId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EnajenantesEnCopropiedad [bit, obligatorio]; FechaInstrumentoNotarial [date, obligatorio]; Id [uniqueidentifier, obligatorio]; IvaOperacion [decimal(18,6), obligatorio]; MontoOperacion [decimal(18,6), obligatorio]; NumeroInstrumentoNotarial [int, obligatorio]; SubtotalOperacion [decimal(18,6), obligatorio].

#### 37. InmueblesNotariales

**Qué guarda:** Inmuebles de la operación: orden, tipo y domicilio.

**Cómo se usa y por qué:** Depende de DatosNotaria. Una operación puede incluir varios inmuebles, por eso no se pusieron todos como columnas repetidas en la cabecera.

**Relaciones físicas:** DatosNotariaId=Id → dbo.DatosNotaria.

**Columnas verificadas:** Calle [nvarchar(150), obligatorio]; CodigoPostal [nvarchar(5), obligatorio]; Colonia [nvarchar(100), admite nulo]; DatosNotariaId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(3), obligatorio]; Id [uniqueidentifier, obligatorio]; Localidad [nvarchar(100), admite nulo]; Municipio [nvarchar(100), obligatorio]; NumeroExterior [nvarchar(55), admite nulo]; NumeroInterior [nvarchar(30), admite nulo]; Orden [int, obligatorio]; Pais [nvarchar(3), obligatorio]; Referencia [nvarchar(100), admite nulo]; TipoInmueble [nvarchar(2), obligatorio].

#### 38. PartesNotariales

**Qué guarda:** Personas que intervienen: rol de enajenante/adquirente, nombres, RFC, CURP y porcentaje.

**Cómo se usa y por qué:** Depende de DatosNotaria. Permite múltiples vendedores y compradores y guardar la distribución de copropiedad cuando aplica.

**Relaciones físicas:** DatosNotariaId=Id → dbo.DatosNotaria.

**Columnas verificadas:** ApellidoMaterno [nvarchar(200), admite nulo]; ApellidoPaterno [nvarchar(200), admite nulo]; Curp [nvarchar(18), admite nulo]; DatosNotariaId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; Nombre [nvarchar(254), obligatorio]; Orden [int, obligatorio]; Porcentaje [decimal(5,2), admite nulo]; Rfc [nvarchar(13), obligatorio]; Rol [nvarchar(12), obligatorio].

#### 39. DatosObra

**Qué guarda:** Parámetros de la estimación: tipo de obra, amortización, retenciones, devoluciones, IVA y deducciones con nombre y porcentaje.

**Cómo se usa y por qué:** La estimación comercial, el líquido y el total fiscal son magnitudes diferentes. No toda deducción contractual se convierte en impuesto del CFDI. ServicioParcialJson se conserva como dato opcional de compatibilidad, sin captura adicional habilitada en esta entrega.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** ComprobanteId [uniqueidentifier, obligatorio]; Devoluciones [decimal(18,6), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; NombreDeduccion1 [nvarchar(60), obligatorio]; NombreDeduccion2 [nvarchar(60), obligatorio]; NombreDeduccion3 [nvarchar(60), obligatorio]; NombreDeduccion4 [nvarchar(60), obligatorio]; PorcentajeAmortizacion [decimal(8,4), obligatorio]; PorcentajeDeduccion1 [decimal(8,4), obligatorio]; PorcentajeDeduccion2 [decimal(8,4), obligatorio]; PorcentajeDeduccion3 [decimal(8,4), obligatorio]; PorcentajeDeduccion4 [decimal(8,4), obligatorio]; PorcentajeDevoluciones [decimal(8,4), admite nulo]; PorcentajeIva [decimal(8,4), obligatorio]; PorcentajeRetenciones [decimal(8,4), admite nulo]; Retenciones [decimal(18,6), obligatorio]; ServicioParcialJson [nvarchar(max), admite nulo]; TipoObra [nvarchar(10), admite nulo].

### Comercio Exterior

#### 40. DatosComercioExterior

**Qué guarda:** Contenido JSON del complemento vinculado a un comprobante: datos de comercio, domicilios y mercancías.

**Cómo se usa y por qué:** No hay una tabla independiente por cada propiedad anidada. El servicio deserializa el JSON tipado y valida su contenido antes de producir el XML del complemento. Es flexible, pero exige validar en el servidor: SQL no comprueba por sí solo las reglas internas del JSON.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes.

**Columnas verificadas:** ComprobanteId [uniqueidentifier, obligatorio]; Contenido [nvarchar(max), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio].

### Carta Porte

#### 41. Vehiculos

**Qué guarda:** Catálogo de cada empresa: unidad, placa, modelo, configuración, peso, aseguradora, póliza y permiso.

**Cómo se usa y por qué:** Se selecciona al crear el traslado. Sus datos se copian al documento para conservar lo utilizado en el viaje, aunque después se actualice el vehículo.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; AnioModelo [int, obligatorio]; Aseguradora [nvarchar(150), obligatorio]; Clave [nvarchar(30), obligatorio]; ConfiguracionAutotransporte [nvarchar(10), obligatorio]; Descripcion [nvarchar(250), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; FechaAltaUtc [datetime2, obligatorio]; FechaModificacionUtc [datetime2, admite nulo]; Id [uniqueidentifier, obligatorio]; NumeroPermiso [nvarchar(50), obligatorio]; PesoBrutoVehicular [decimal(18,6), obligatorio]; Placa [nvarchar(20), obligatorio]; Poliza [nvarchar(50), obligatorio]; TipoPermiso [nvarchar(10), obligatorio].

#### 42. FigurasTransporte

**Qué guarda:** Personas que participan en transporte: tipo de figura, RFC, nombre, licencia y domicilio.

**Cómo se usa y por qué:** Es el catálogo reutilizable de la empresa. SatFiguraTransporte es el catálogo de tipos permitidos; no son las mismas entidades.

**Relaciones físicas:** EmpresaId=Id → dbo.Empresas.

**Columnas verificadas:** Activo [bit, obligatorio]; Calle [nvarchar(150), obligatorio]; Clave [nvarchar(30), obligatorio]; CodigoPostal [nvarchar(5), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(3), obligatorio]; FechaAltaUtc [datetime2, obligatorio]; FechaModificacionUtc [datetime2, admite nulo]; Id [uniqueidentifier, obligatorio]; Municipio [nvarchar(3), obligatorio]; Nombre [nvarchar(254), obligatorio]; NumeroExterior [nvarchar(55), obligatorio]; NumeroInterior [nvarchar(55), admite nulo]; NumeroLicencia [nvarchar(50), admite nulo]; Rfc [nvarchar(13), obligatorio]; TipoFigura [nvarchar(10), obligatorio].

#### 43. TrasladosCartaPorte

**Qué guarda:** Rama de transporte de Comprobantes: vehículo, figura, destino, IdCCP, fechas, distancia, peso y copias de la unidad y del conductor.

**Cómo se usa y por qué:** El alcance revisado es autotransporte nacional propio. No afirmar que tener esta tabla implementa transporte marítimo, aéreo, ferroviario o internacional.

**Relaciones físicas:** ComprobanteId=Id → dbo.Comprobantes; FiguraTransporteId=Id → dbo.FigurasTransporte; VehiculoId=Id → dbo.Vehiculos.

**Columnas verificadas:** AseguraMedAmbiente [nvarchar(50), admite nulo]; ClienteDestinoId [uniqueidentifier, admite nulo]; ComprobanteId [uniqueidentifier, obligatorio]; DistanciaRecorridaKm [decimal(18,6), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; FechaLlegadaUtc [datetime2, obligatorio]; FechaSalidaUtc [datetime2, obligatorio]; FiguraNombre [nvarchar(254), admite nulo]; FiguraNumeroLicencia [nvarchar(50), admite nulo]; FiguraRfc [nvarchar(13), admite nulo]; FiguraTipo [nvarchar(10), admite nulo]; FiguraTransporteId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; IdCcp [nvarchar(36), admite nulo]; PesoBrutoTotalKg [decimal(18,6), obligatorio]; PolizaMedAmbiente [nvarchar(30), admite nulo]; TotalMercancias [decimal(18,6), obligatorio]; VehiculoAnioModelo [int, admite nulo]; VehiculoAseguradora [nvarchar(150), admite nulo]; VehiculoConfiguracionAutotransporte [nvarchar(10), admite nulo]; VehiculoId [uniqueidentifier, obligatorio]; VehiculoNumeroPermiso [nvarchar(50), admite nulo]; VehiculoPesoBruto [decimal(18,6), admite nulo]; VehiculoPlaca [nvarchar(20), admite nulo]; VehiculoPoliza [nvarchar(50), admite nulo]; VehiculoTipoPermiso [nvarchar(10), admite nulo].

#### 44. UbicacionesCartaPorte

**Qué guarda:** Orígenes y destinos del traslado: tipo, orden, remitente/destinatario y domicilio.

**Cómo se usa y por qué:** Depende de TrasladosCartaPorte. Son datos del viaje; no se obtiene siempre el destino leyendo la dirección actual del cliente.

**Relaciones físicas:** TrasladoCartaPorteId=Id → dbo.TrasladosCartaPorte.

**Columnas verificadas:** Calle [nvarchar(150), obligatorio]; CodigoPostal [nvarchar(5), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(3), obligatorio]; Id [uniqueidentifier, obligatorio]; Municipio [nvarchar(3), obligatorio]; NombreRemitenteDestinatario [nvarchar(254), admite nulo]; NumeroExterior [nvarchar(55), obligatorio]; NumeroInterior [nvarchar(55), admite nulo]; Orden [int, obligatorio]; RfcRemitenteDestinatario [nvarchar(13), admite nulo]; Tipo [nvarchar(8), obligatorio]; TrasladoCartaPorteId [uniqueidentifier, obligatorio].

#### 45. MercanciasCartaPorte

**Qué guarda:** Mercancías transportadas: clave, descripción, cantidad, unidad, pesos, dimensiones y clasificación peligrosa cuando aplica.

**Cómo se usa y por qué:** Depende del traslado. Representa bienes transportados, no necesariamente conceptos cobrados en una factura. Conserva su propia descripción y peso. ProductoId es una referencia opcional al catálogo de la cuenta para la captura; el documento conserva descripción, claves y pesos propios.

**Relaciones físicas:** TrasladoCartaPorteId=Id → dbo.TrasladosCartaPorte.

**Columnas verificadas:** Cantidad [decimal(18,6), obligatorio]; ClaveProdServ [nvarchar(8), obligatorio]; ClaveUnidad [nvarchar(20), obligatorio]; CveMaterialPeligroso [nvarchar(4), admite nulo]; Descripcion [nvarchar(1000), obligatorio]; DescripEmbalaje [nvarchar(100), admite nulo]; Dimensiones [nvarchar(14), admite nulo]; Embalaje [nvarchar(5), admite nulo]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; MaterialPeligroso [nvarchar(2), admite nulo]; Orden [int, obligatorio]; PesoEnKg [decimal(18,6), obligatorio]; PesoUnitarioKg [decimal(18,6), admite nulo]; ProductoId [uniqueidentifier, admite nulo]; TrasladoCartaPorteId [uniqueidentifier, obligatorio]; Unidad [nvarchar(20), admite nulo].

#### 46. RemolquesCartaPorte

**Qué guarda:** Remolques utilizados: orden, subtipo y placa.

**Cómo se usa y por qué:** Depende del traslado y admite una colección; SatSubTipoRemolque aporta las claves válidas. Son remolques de ese documento, no un catálogo global de vehículos.

**Relaciones físicas:** TrasladoCartaPorteId=Id → dbo.TrasladosCartaPorte.

**Columnas verificadas:** EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; Orden [int, obligatorio]; Placa [nvarchar(7), obligatorio]; SubTipoRem [nvarchar(6), obligatorio]; TrasladoCartaPorteId [uniqueidentifier, obligatorio].

### Timbres y compras

#### 47. Paquetes

**Qué guarda:** Ofertas de timbres: cantidad, precio, vigencia, claves SAT, orden y estado.

**Cómo se usa y por qué:** CuentaId nulo identifica paquetes generales; con cuenta identifica ofertas exclusivas. La compra toma el precio del servidor, no acepta un precio arbitrario enviado por el navegador.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas.

**Columnas verificadas:** Activo [bit, obligatorio]; CantidadTimbres [int, obligatorio]; ClaveProdServ [nvarchar(8), admite nulo]; ClaveUnidad [nvarchar(3), admite nulo]; CuentaId [uniqueidentifier, admite nulo]; Id [uniqueidentifier, obligatorio]; Nombre [nvarchar(100), obligatorio]; Orden [int, obligatorio]; PrecioPorTimbre [decimal(18,6), obligatorio]; PrecioTotal [decimal(18,6), obligatorio]; VigenciaMeses [int, obligatorio].

#### 48. ComprasTimbres

**Qué guarda:** Compra de un paquete por una cuenta: empresa de origen, usuario, cantidad, precios, IVA incluido, vigencia, estado y copias de los datos comprados.

**Cómo se usa y por qué:** Comprar y acreditar son pasos distintos. Pendiente de pago no significa saldo disponible. Cambiar un paquete después no cambia el precio histórico de esta compra; la acreditación confirmada alimenta la bolsa.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** AcreditadaUtc [datetime2, admite nulo]; CanceladaUtc [datetime2, admite nulo]; CantidadTimbres [int, obligatorio]; ClaveProdServAlComprar [nvarchar(8), admite nulo]; ClaveUnidadAlComprar [nvarchar(3), admite nulo]; CreadaUtc [datetime2, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; EmpresaNombreAlComprar [nvarchar(254), obligatorio]; EmpresaRfcAlComprar [nvarchar(13), obligatorio]; Estado [nvarchar(20), obligatorio]; Id [uniqueidentifier, obligatorio]; Iva [decimal(18,6), obligatorio]; MotivoCancelacion [nvarchar(300), admite nulo]; NombrePaquete [nvarchar(100), obligatorio]; PaqueteId [uniqueidentifier, obligatorio]; PrecioPorTimbre [decimal(18,6), obligatorio]; PrecioTotal [decimal(18,6), obligatorio]; Subtotal [decimal(18,6), obligatorio]; TasaIva [decimal(18,6), obligatorio]; UsuarioId [uniqueidentifier, obligatorio]; VenceUtc [datetime2, admite nulo]; VigenciaMeses [int, obligatorio].

#### 49. BolsasTimbres

**Qué guarda:** Saldo actual de una cuenta separado en disponibles y reservados.

**Cómo se usa y por qué:** Lo comparten sus empresas; reservar evita que dos emisiones gasten el mismo timbre. En la revisión de borradores no se consume saldo. No guarda el detalle de todos los movimientos: ese detalle está en MovimientosTimbre.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas.

**Columnas verificadas:** ActualizadaUtc [datetime2, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Disponibles [int, obligatorio]; Id [uniqueidentifier, obligatorio]; Reservados [int, obligatorio].

#### 50. ReservasTimbre

**Qué guarda:** Timbre apartado para un comprobante y empresa, con estado y resolución.

**Cómo se usa y por qué:** Aunque la bolsa es por cuenta, la reserva conserva la empresa y el documento causantes. Se confirma o devuelve según el resultado del proceso.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** ComprobanteId [uniqueidentifier, obligatorio]; CreadaUtc [datetime2, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(20), obligatorio]; Id [uniqueidentifier, obligatorio]; MotivoResolucion [nvarchar(300), admite nulo]; ResueltaUtc [datetime2, admite nulo].

#### 51. MovimientosTimbre

**Qué guarda:** Libro de movimientos de la cuenta: compra, reserva, consumo, devolución o ajuste, variaciones y saldos posteriores.

**Cómo se usa y por qué:** Conserva empresa de origen, usuario y referencias de compra/reserva. Permite explicar cómo se llegó al saldo actual; no es otra bolsa independiente por empresa.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** CompraId [uniqueidentifier, admite nulo]; CuentaId [uniqueidentifier, obligatorio]; DeltaDisponible [int, obligatorio]; DeltaReservado [int, obligatorio]; DisponiblesDespues [int, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Id [uniqueidentifier, obligatorio]; MomentoUtc [datetime2, obligatorio]; Motivo [nvarchar(300), admite nulo]; ReservadosDespues [int, obligatorio]; ReservaId [uniqueidentifier, admite nulo]; Tipo [nvarchar(20), obligatorio]; UsuarioId [uniqueidentifier, admite nulo].

#### 52. Membresias

**Qué guarda:** Periodos de servicio de una cuenta, con inicio, fin y estado.

**Cómo se usa y por qué:** Representa la vigencia del acceso comercial. No debe confundirse con la cantidad de timbres ni con una factura de compra de paquetes.

**Relaciones físicas:** CuentaId=Id → dbo.Cuentas.

**Columnas verificadas:** CreadaUtc [datetime2, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Estado [nvarchar(20), obligatorio]; FinUtc [datetime2, obligatorio]; Id [uniqueidentifier, obligatorio]; InicioUtc [datetime2, obligatorio].

### Facturas del operador

#### 53. ConfiguracionFiscalProveedor

**Qué guarda:** Identidad fiscal del proveedor del SaaS: RFC, nombre, régimen, CP, zona horaria, serie y versión.

**Cómo se usa y por qué:** Es el emisor que factura la venta de timbres. No es la configuración fiscal de cada empresa cliente ni la identidad del operador que inicia sesión.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** ActualizadaPorOperadorId [uniqueidentifier, obligatorio]; ActualizadaUtc [datetime2, obligatorio]; CodigoPostalExpedicion [nvarchar(5), obligatorio]; Id [tinyint, obligatorio]; NombreFiscal [nvarchar(254), obligatorio]; RegimenFiscal [nvarchar(3), obligatorio]; Rfc [nvarchar(13), obligatorio]; Serie [nvarchar(25), obligatorio]; Version [timestamp, obligatorio]; ZonaHoraria [nvarchar(64), obligatorio].

#### 54. CertificadosProveedor

**Qué guarda:** CSD del proveedor del SaaS con CER, KEY y contraseña cifrados, vigencia y estado.

**Cómo se usa y por qué:** Aquí los archivos cifrados están en columnas binarias, a diferencia de CertificadosCsd que referencia rutas del almacén. Ninguno de esos certificados es la contraseña de acceso al PAC.

**Relaciones físicas:** ConfiguracionId=Id → dbo.ConfiguracionFiscalProveedor.

**Columnas verificadas:** Activo [bit, obligatorio]; CargadoPorOperadorId [uniqueidentifier, obligatorio]; CargadoUtc [datetime2, obligatorio]; CerCifrado [varbinary(max), obligatorio]; ConfiguracionId [tinyint, obligatorio]; ContrasenaCifrada [nvarchar(3000), obligatorio]; Id [uniqueidentifier, obligatorio]; KeyCifrada [varbinary(max), obligatorio]; NumeroSerie [nvarchar(40), obligatorio]; Rfc [nvarchar(13), obligatorio]; VigenciaDesdeUtc [datetime2, obligatorio]; VigenciaHastaUtc [datetime2, obligatorio].

#### 55. FacturasDeCompras

**Qué guarda:** Factura asociada a una compra: emisor y receptor copiados, concepto del paquete, claves, subtotal, IVA, total, estado y certificación.

**Cómo se usa y por qué:** Se separa de los comprobantes emitidos por las empresas: el emisor es el proveedor del sistema. Incluye versión para detectar cambios concurrentes. Tener una compra pagada no significa que su factura ya esté timbrada.

**Relaciones físicas:** CompraId=Id → dbo.ComprasTimbres.

**Columnas verificadas:** ActualizadaPorOperadorId [uniqueidentifier, obligatorio]; ActualizadaUtc [datetime2, obligatorio]; CantidadTimbres [int, obligatorio]; ClaveProdServ [nvarchar(8), obligatorio]; ClaveUnidad [nvarchar(3), obligatorio]; CompraId [uniqueidentifier, obligatorio]; CreadaPorOperadorId [uniqueidentifier, obligatorio]; CreadaUtc [datetime2, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Descripcion [nvarchar(1000), obligatorio]; EmisorCodigoPostal [nvarchar(5), obligatorio]; EmisorNombre [nvarchar(254), obligatorio]; EmisorRegimen [nvarchar(3), obligatorio]; EmisorRfc [nvarchar(13), obligatorio]; EmisorZonaHoraria [nvarchar(64), obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Estado [nvarchar(20), obligatorio]; FechaTimbradoUtc [datetime2, admite nulo]; Folio [int, admite nulo]; FormaPago [nvarchar(2), obligatorio]; Id [uniqueidentifier, obligatorio]; Iva [decimal(18,6), obligatorio]; ReceptorCodigoPostal [nvarchar(5), obligatorio]; ReceptorNombre [nvarchar(254), obligatorio]; ReceptorRegimen [nvarchar(3), obligatorio]; ReceptorRfc [nvarchar(13), obligatorio]; Serie [nvarchar(25), obligatorio]; Subtotal [decimal(18,6), obligatorio]; TasaIva [decimal(18,6), obligatorio]; Total [decimal(18,6), obligatorio]; UsoCfdi [nvarchar(3), obligatorio]; Uuid [uniqueidentifier, admite nulo]; Version [timestamp, obligatorio]; VersionProveedor [varbinary(8), obligatorio].

#### 56. IntentosFacturasProveedor

**Qué guarda:** Intentos de emisión del proveedor, con folio, certificado, operador, clave de solicitud, XML enviado/timbrado y resultado.

**Cómo se usa y por qué:** Permite recuperar intentos sin duplicar la factura de una compra. En esta rama hay XML guardado en la base; no afirmar que todos los XML del sistema viven únicamente en carpetas.

**Relaciones físicas:** CertificadoId=Id → dbo.CertificadosProveedor; FacturaId=Id → dbo.FacturasDeCompras.

**Columnas verificadas:** ActualizadaUtc [datetime2, obligatorio]; CadenaOriginal [nvarchar(max), admite nulo]; CadenaOriginalSat [nvarchar(max), admite nulo]; CertificadoId [uniqueidentifier, obligatorio]; ClaveSolicitud [nvarchar(128), obligatorio]; CodigoError [nvarchar(100), admite nulo]; Control [uniqueidentifier, obligatorio]; CuentaId [uniqueidentifier, obligatorio]; Estado [nvarchar(20), obligatorio]; FacturaId [uniqueidentifier, obligatorio]; FechaEmisionUtc [datetime2, obligatorio]; FechaTimbradoUtc [datetime2, admite nulo]; Folio [int, obligatorio]; Id [uniqueidentifier, obligatorio]; NoCertificado [nvarchar(40), admite nulo]; NoCertificadoSat [nvarchar(40), admite nulo]; OperadorId [uniqueidentifier, obligatorio]; Sello [nvarchar(max), admite nulo]; SelloSat [nvarchar(max), admite nulo]; Uuid [uniqueidentifier, admite nulo]; VersionSolicitada [varbinary(8), obligatorio]; XmlEnviado [nvarchar(max), admite nulo]; XmlTimbrado [nvarchar(max), admite nulo].

#### 57. SeriesFacturasProveedor

**Qué guarda:** Último folio por RFC y serie del proveedor.

**Cómo se usa y por qué:** Separa la numeración de las facturas que vende el SaaS de las series de cada empresa cliente. No es una bolsa de timbres ni el UUID fiscal.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Rfc [nvarchar(13), obligatorio]; Serie [nvarchar(25), obligatorio]; UltimoFolio [int, obligatorio].

### Control técnico

#### 58. Bitacora

**Qué guarda:** Registros de cambios con entidad, acción, valores anterior/nuevo, usuario u operador, cuenta/empresa cuando corresponda, momento y traza.

**Cómo se usa y por qué:** Permite seguir quién cambió un dato. Se escribe desde los servicios de negocio; no es una grabación automática de cualquier UPDATE hecho a mano en SSMS. Los secretos se excluyen del contenido registrado.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Accion [nvarchar(64), obligatorio]; CuentaId [uniqueidentifier, admite nulo]; EmpresaId [uniqueidentifier, admite nulo]; Entidad [nvarchar(128), obligatorio]; EntidadId [nvarchar(64), admite nulo]; Id [bigint, obligatorio]; IpOrigen [nvarchar(45), admite nulo]; MomentoUtc [datetime2, obligatorio]; OperadorId [uniqueidentifier, admite nulo]; TraceId [nvarchar(64), admite nulo]; UsuarioId [uniqueidentifier, admite nulo]; ValorAnterior [nvarchar(max), admite nulo]; ValorNuevo [nvarchar(max), admite nulo].

#### 59. ClavesIdempotencia

**Qué guarda:** Clave de una operación, endpoint, usuario, empresa, hash de petición, código HTTP y respuesta JSON con caducidad.

**Cómo se usa y por qué:** Si se repite una operación con la misma clave y contenido, se reutiliza la respuesta. No equivale a bloquear todos los dobles clics del sistema ni a deduplicar por precio o nombre.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(128), obligatorio]; CodigoEstado [int, obligatorio]; CreadoUtc [datetime2, obligatorio]; EmpresaId [uniqueidentifier, obligatorio]; Endpoint [nvarchar(256), obligatorio]; ExpiraUtc [datetime2, obligatorio]; HashPeticion [nvarchar(64), obligatorio]; Id [uniqueidentifier, obligatorio]; RespuestaJson [nvarchar(max), obligatorio]; UsuarioId [uniqueidentifier, obligatorio].

#### 60. CatalogoVersion

**Qué guarda:** Versión, fecha y datos de control de la carga de catálogos.

**Cómo se usa y por qué:** Permite distinguir qué catálogo se cargó y coordinar su actualización. No guarda las miles de claves: cada catálogo SAT tiene su tabla.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Catalogo [nvarchar(32), obligatorio]; FechaCargaUtc [datetime2, obligatorio]; FechaPublicacion [date, admite nulo]; RenglonesVigentes [int, obligatorio]; RevisionCatalogo [nvarchar(16), obligatorio]; VersionCatalogo [nvarchar(16), obligatorio].

#### 61. __EFMigrationsHistory

**Qué guarda:** Historial técnico de migraciones ejecutadas por Entity Framework.

**Cómo se usa y por qué:** No contiene facturas ni se usa como permiso del cliente. La base actualizada por el script de entrega puede tener este historial vacío aunque sus tablas ya estén actualizadas; no ejecutar EF database update a ciegas sobre ella.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** MigrationId [nvarchar(150), obligatorio]; ProductVersion [nvarchar(32), obligatorio].

#### 62. sysdiagrams

**Qué guarda:** Diagramas creados por SQL Server Management Studio.

**Cómo se usa y por qué:** Es auxiliar de SSMS; no pertenece al funcionamiento de la aplicación. Puede faltar en la base del ingeniero sin afectar facturación. Los procedimientos sp_*diagram y fn_diagramobjects acompañan esta función.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** definition [varbinary(max), admite nulo]; diagram_id [int, obligatorio]; name [sysname, obligatorio]; principal_id [int, obligatorio]; version [int, admite nulo].

### Catálogos SAT

#### 63. SatClaveProdServ

**Qué guarda:** Claves generales de productos y servicios para los conceptos del CFDI.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(8), obligatorio]; ComplementoQueDebeIncluir [nvarchar(128), admite nulo]; Descripcion [nvarchar(512), obligatorio]; EstimuloFranjaFronteriza [bit, obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; IncluirIepsTrasladado [nvarchar(16), admite nulo]; IncluirIvaTrasladado [nvarchar(16), admite nulo]; PalabrasSimilares [nvarchar(1024), admite nulo]; Vigente [bit, obligatorio].

#### 64. SatClaveProdServCartaPorte

**Qué guarda:** Clasificación de los bienes transportados para Carta Porte, distinta de la clave del servicio facturado.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(8), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; MaterialPeligroso [nvarchar(3), admite nulo]; Vigente [bit, obligatorio].

#### 65. SatClaveUnidad

**Qué guarda:** Claves oficiales de unidad, como pieza o unidad de servicio.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(20), obligatorio]; Descripcion [nvarchar(1024), admite nulo]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Nombre [nvarchar(254), obligatorio]; Nota [nvarchar(512), admite nulo]; Simbolo [nvarchar(32), admite nulo]; Vigente [bit, obligatorio].

#### 66. SatCodigoPostal

**Qué guarda:** Códigos postales y referencias territoriales para resolver direcciones.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(5), obligatorio]; ClaveEstado [nvarchar(3), obligatorio]; ClaveLocalidad [nvarchar(4), admite nulo]; ClaveMunicipio [nvarchar(3), admite nulo]; EstimuloFranjaFronteriza [bit, obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 67. SatColonia

**Qué guarda:** Colonias asociadas a códigos postales; un CP puede tener varias colonias.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(4), obligatorio]; ClaveCodigoPostal [nvarchar(5), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; IdInterno [int, obligatorio]; Nombre [nvarchar(254), obligatorio]; Vigente [bit, obligatorio].

#### 68. SatConfiguracionAutotransporte

**Qué guarda:** Configuración vehicular y datos de clasificación necesarios para el autotransporte.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(10), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Remolque [nvarchar(3), admite nulo]; Vigente [bit, obligatorio].

#### 69. SatEstado

**Qué guarda:** Entidades federativas y referencia a país.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(3), obligatorio]; ClavePais [nvarchar(3), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Nombre [nvarchar(254), obligatorio]; Vigente [bit, obligatorio].

#### 70. SatExportacion

**Qué guarda:** Claves que identifican la condición de exportación en el CFDI.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(2), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 71. SatFiguraTransporte

**Qué guarda:** Tipos oficiales de figura de transporte, no los conductores capturados.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(10), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 72. SatFormaPago

**Qué guarda:** Medio de pago: efectivo, transferencia, etc.; distinto de PUE/PPD.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(4), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 73. SatFraccionArancelaria

**Qué guarda:** Fracciones de clasificación arancelaria y datos de cruce aplicables al complemento de comercio.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(12), obligatorio]; Descripcion [nvarchar(2000), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; UnidadMedida [nvarchar(2), admite nulo]; Vigente [bit, obligatorio].

#### 74. SatImpuesto

**Qué guarda:** Identificadores oficiales de impuestos federales, como ISR, IVA e IEPS.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(3), obligatorio]; Descripcion [nvarchar(64), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; LocalOFederal [nvarchar(16), obligatorio]; Retencion [bit, obligatorio]; Traslado [bit, obligatorio]; Vigente [bit, obligatorio].

#### 75. SatIncoterm

**Qué guarda:** Términos comerciales internacionales usados en Comercio Exterior.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(10), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 76. SatMaterialPeligroso

**Qué guarda:** Clasificación oficial de materiales peligrosos para mercancías.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(4), obligatorio]; Descripcion [nvarchar(max), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 77. SatMes

**Qué guarda:** Meses o agrupaciones de meses aplicables a información global.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(2), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 78. SatMetodoPago

**Qué guarda:** PUE o PPD: cómo se liquida la operación; distinto del medio de pago.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(3), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 79. SatMoneda

**Qué guarda:** Catálogo general de monedas, decimales y datos de tipo de cambio.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(3), obligatorio]; Decimales [int, obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 80. SatMunicipio

**Qué guarda:** Municipios; se identifican junto con su estado para evitar confundir claves iguales.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(3), obligatorio]; ClaveEstado [nvarchar(3), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; IdInterno [int, obligatorio]; Vigente [bit, obligatorio].

#### 81. SatObjetoImp

**Qué guarda:** Clasifica si el concepto es objeto de impuesto y las condiciones de desglose.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(2), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 82. SatPais

**Qué guarda:** Claves de países y datos de validación aplicables.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Agrupaciones [nvarchar(500), admite nulo]; Clave [nvarchar(3), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; FormatoCodigoPostal [nvarchar(2000), admite nulo]; FormatoRegistroTributario [nvarchar(2000), admite nulo]; ValidacionRegistroTributario [nvarchar(2000), admite nulo]; Vigente [bit, obligatorio].

#### 83. SatPeriodicidad

**Qué guarda:** Periodicidad de la factura global.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(2), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 84. SatRegimenFiscal

**Qué guarda:** Regímenes fiscales y compatibilidad por tipo de persona.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** AplicaFisica [bit, obligatorio]; AplicaMoral [bit, obligatorio]; Clave [nvarchar(3), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 85. SatSubTipoRemolque

**Qué guarda:** Subtipos oficiales de remolque.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(6), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 86. SatTasaOCuota

**Qué guarda:** Tasas/cuotas permitidas, rangos, impuesto y condición de traslado/retención.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(160), obligatorio]; Factor [nvarchar(16), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Impuesto [nvarchar(64), obligatorio]; RangoOFijo [nvarchar(16), obligatorio]; Retencion [bit, obligatorio]; Traslado [bit, obligatorio]; ValorMaximo [decimal(18,6), obligatorio]; ValorMinimo [decimal(18,6), admite nulo]; Vigente [bit, obligatorio].

#### 87. SatTipoDeComprobante

**Qué guarda:** Tipos oficiales como ingreso, egreso, traslado y pago. Una clave disponible no implica que todos sus módulos estén habilitados.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(1), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; ValorMaximo [decimal(18,6), admite nulo]; Vigente [bit, obligatorio].

#### 88. SatTipoEmbalaje

**Qué guarda:** Tipos de embalaje para las mercancías que requieren esa clasificación.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(5), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 89. SatTipoFactor

**Qué guarda:** Tasa, Cuota o Exento; define cómo se expresa el impuesto.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(16), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 90. SatTipoPermiso

**Qué guarda:** Permisos de transporte y su clasificación.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(10), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 91. SatTipoRelacion

**Qué guarda:** Relación entre CFDI, por ejemplo sustitución o aplicación de anticipo.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(2), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 92. SatUnidadAduana

**Qué guarda:** Unidades aduaneras usadas en las mercancías de Comercio Exterior.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** Clave [nvarchar(10), obligatorio]; Descripcion [nvarchar(500), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; Vigente [bit, obligatorio].

#### 93. SatUsoCfdi

**Qué guarda:** Uso del CFDI y compatibilidad con regímenes y personas.

**Cómo se usa y por qué:** Es información común, sin propiedad de una cuenta o empresa. Sus claves y vigencias sirven para validar la captura y el XML. Los catálogos grandes se buscan en el servidor; no se envían completos al navegador.

**Relaciones:** revisar las referencias indicadas en la explicación; no se encontraron claves foráneas salientes en esta tabla.

**Columnas verificadas:** AplicaFisica [bit, obligatorio]; AplicaMoral [bit, obligatorio]; Clave [nvarchar(4), obligatorio]; Descripcion [nvarchar(254), obligatorio]; FechaFinVigencia [date, admite nulo]; FechaInicioVigencia [date, obligatorio]; RegimenesFiscalesAplicables [nvarchar(512), obligatorio]; Vigente [bit, obligatorio].


## 13. Preguntas que puede hacer el ingeniero

**¿Cuántas bases usa la aplicación?** Una base de negocio compartida por cuentas y empresas. FacturacionDev y Facturacion1.1 son dos bases de entornos/pruebas; no son dos mitades necesarias para una misma instalación.

**¿Por qué no una base por empresa?** El esquema compartido simplifica despliegue, actualización, catálogos comunes y datos compartidos por cuenta. A cambio exige aplicar rigurosamente los filtros y permisos. No implica que SQL bloquee por sí solo al usuario técnico de la aplicación por empresa.

**¿Qué pasa al cambiar de empresa?** Se verifica acceso dentro de la cuenta y cambia el contexto/token. Se consultan los documentos y configuraciones de la nueva empresa; los clientes y productos siguen disponibles porque son de la misma cuenta. No se mueve información de una empresa a otra.

**¿Qué evita repetir folios?** Una reserva atómica en SQL y restricciones, no MAX(Folio)+1. Las series están separadas por empresa y tipo; las del proveedor tienen su propio control.

**¿Qué evita timbrar dos veces?** Estados, idempotencia, registro de intentos y recuperación. Un timeout no autoriza reenviar con una nueva clave sin revisar el intento anterior.

**¿Qué sucede si cambia el nombre o precio de un producto?** Afecta futuras capturas del catálogo. La factura guarda su descripción, precio e impuestos propios; no lee siempre los valores actuales para representar un documento emitido.

**¿Se guarda el producto al guardar una factura?** Se guarda el concepto en Conceptos. No se da de alta automáticamente en Productos por ese solo hecho.

**¿La cuenta puede facturar con el CSD de otra empresa suya?** No por compartir cuenta. Cada emisor debe usar su propio CSD válido. El CSD del proveedor del SaaS es otra identidad y sirve para sus ventas de paquetes.

**¿Cuál es la diferencia entre CSD y PAC?** El CSD identifica y permite sellar al emisor. El PAC es el servicio externo que certifica/timbra. El proveedor entrega credenciales de acceso al PAC aparte del certificado del emisor.

**¿Dónde está la contraseña del usuario?** Como hash verificable en Identity o en el operador, no como contraseña recuperable. SMTP/CSD son secretos operativos que deben poder usarse, por eso se protegen con cifrado y llaveros, no con el mismo mecanismo de hash de una contraseña de inicio de sesión.

**¿La cuenta SMTP del operador manda las facturas de las empresas?** No. Los correos de registro y recuperación son del sistema; los comprobantes se envían por la configuración de su empresa.

**¿Un correo exitoso garantiza que ya llegó?** Garantiza la aceptación por el servidor SMTP en el envío registrado. La recepción real, filtros y spam se comprueban aparte.

**¿Cancelar elimina la factura?** No. Se conserva el documento y su historia de cancelación. Una solicitud pendiente y una cancelación definitiva no son el mismo estado.

**¿Un pago es la pasarela?** No. El complemento documenta un abono a facturas; la pasarela cobraría las compras del SaaS. Son módulos diferentes.

**¿Se pueden usar las claves SAT como texto libre?** La interfaz ayuda a elegir; el servidor debe validar la clave, vigencia y compatibilidad. Cambiar un texto visible no crea por sí mismo una clave oficial válida.

**¿Por qué JSON en Comercio Exterior?** Agrupa un complemento anidado cambiante sin multiplicar tablas por cada propiedad. Conserva la relación y el aislamiento del documento, con validación tipada en servidor. Es una decisión con compromisos: consultar campos internos directamente con SQL es menos sencillo, especialmente en SQL Server 2014.

**¿Es obligatorio tener historial de EF para que funcione?** La aplicación necesita el esquema correcto. La base entregada por script puede funcionar con historial distinto/vacío; las actualizaciones futuras deben respetar ese mecanismo y su punto de partida.

**¿Todo está probado para producción?** No. La entrega comprobó casos funcionales y actualización de esquema. No acredita todas las combinaciones posibles, PAC real, certificados reales, cobros reales ni configuración particular del servidor del ingeniero.

## 14. Archivos del código para respaldar tus respuestas

- Facturacion.Server/Data/AppDbContext.cs: entidades y configuración del contexto.
- Facturacion.Server/Data/Entidades y Data/Configurations: propiedades, relaciones, índices y restricciones del modelo.
- Facturacion.Server/Data/FiltroDeEmpresa.cs: filtros de cuenta/empresa.
- Facturacion.Server/Infra/Tenencia/SelladoDeEmpresaInterceptor.cs: sello y verificación de propiedad al escribir.
- Facturacion.Server/Modules/Plataforma/Usuarios/ServicioDeUsuarios.cs: accesos y sincronización de permisos.
- Facturacion.Server/Modules/Documentos/Emision/ServicioDeEmision.cs: borradores, conceptos y persistencia.
- Facturacion.Server/Modules/Documentos/Salidas: XML, PDF y correo.
- Facturacion.Server/Infra/Instalacion: conexión del asistente y rutas privadas.
- docs/ENTREGA_IIS_20261105.md: alcance probado de la revisión y script acumulado.

Una forma honesta de responder es: «Eso está implementado así en el servicio y en estas tablas; para esta entrega comprobamos estos casos. La conexión al PAC real se integrará después». No hace falta prometer que una tabla disponible implica una funcionalidad ya habilitada o validada.
