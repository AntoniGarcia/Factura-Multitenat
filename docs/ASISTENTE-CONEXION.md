# Asistente de conexión a SQL Server

Instalación inicial, 20 de octubre de 2026. Permite comprobar y guardar la conexión
a una base existente y crear el primer operador principal. No ejecuta migraciones,
no crea la base y no habilita el PAC.

## Cómo se abre

Cuando no existe `ConnectionStrings:BaseDeDatos`, el servidor abre un modo de
instalación antes de registrar los módulos normales. La entrada es `/operador`:
solo en ese modo sirve el asistente en lugar del panel. `/`, `/instalacion` y
`/operador/iniciar-sesion` redirigen temporalmente a `/operador`.
No requiere conexión SQL, SMTP, JWT ni certificados fiscales para mostrar el formulario.
Se ejecuta con Kestrel; IIS no es necesario para probarlo.

Si la aplicación ya tiene conexión, se comprueba si existen operadores antes del
arranque web. Si no hay ninguno, se abre el segundo paso. Una caída de SQL o un
esquema incompleto bloquean el alta con un aviso; nunca se interpretan como una base
sin operadores. Los comandos de mantenimiento conservan su recorrido de consola.

Para abrir la publicación en Windows desde su carpeta:

```powershell
dotnet .\Facturacion.Server.dll --environment Staging --urls https://localhost:7245
```

HTTPS debe estar configurado en ese equipo. La aplicación necesita el runtime
ASP.NET Core 10 compatible con la publicación. En el equipo de desarrollo se puede
usar el certificado de desarrollo existente. Un acceso remoto sin HTTPS es rechazado;
HTTP solo se admite desde loopback para una comprobación local.

Para probar el wizard desde el código sin cargar `appsettings.Development.json`:

```powershell
dotnet run --project .\Facturacion.Server\Facturacion.Server.csproj --configuration Release --no-launch-profile -- --environment Staging --urls https://localhost:7245 --Instalacion:Directorio=C:\FacturacionPruebas\cnf
```

Usar un directorio propio y privado para cada instalación de prueba. El comando no
oculta una conexión recibida por variables de entorno: estas conservan prioridad.
`Instalacion:SoloConexion=true` mantiene el modo de instalación aun con una conexión
configurada, pero no habilita reemplazarla desde la interfaz.

## Formulario

1. Abrir `cnf/clave-instalacion.txt` en el servidor e introducir esa clave en el wizard.
   Se genera una vez. No es la contraseña del FTP ni de SQL. No publicarla ni enviarla
   por un canal sin cifrado. La aplicación registra su ubicación, nunca su contenido.
2. Seleccionar autenticación e introducir servidor o instancia y base. Con
   **Usuario y contraseña SQL**, capturar las credenciales de SQL. Con
   **Autenticación de Windows**, se usa la cuenta del proceso ASP.NET Core y no se
   solicitan ni se guardan credenciales SQL. Esta opción requiere Windows y permisos
   de esa cuenta sobre la base; no utiliza la identidad del navegador.
3. Pulsar **Probar conexión**. El servidor intenta abrir SQL con un tiempo de espera
   acotado y consulta el número de tablas de usuario visibles. No lee documentos ni
   modifica registros; contar tablas no acredita que las migraciones estén completas.
4. Pulsar **Guardar configuración**. El servidor vuelve a comprobar la conexión y
   crea el archivo una única vez. Editar datos en pantalla exige volver a probar.

La conexión sale del proceso ASP.NET Core hacia SQL Server. Probarla desde una
computadora de desarrollo no acredita la conectividad desde el servidor remoto.

### Prueba en la computadora de Luis

Abrir `http://127.0.0.1:5245/operador` mientras esté ejecutándose el proceso de
pruebas. Usar la clave de `tmp/wizard-20261020/cnf/clave-instalacion.txt`, seleccionar
**Autenticación de Windows**, servidor `.` y base `FacturacionDev`. No capturar un
usuario o contraseña SQL.

Desde el 24 de octubre de 2026 el asistente ya no ofrece confiar en el certificado de
SQL Server: la conexión siempre valida su identidad, y un certificado autofirmado se
rechaza. La opción para el certificado se agregará después en la interfaz.

Se probó la conexión integrada contra la base local con cifrado y confianza explícita
en su certificado: conexión correcta, 83 tablas de usuario visibles. La consulta no
modificó registros ni guardó la configuración. El formulario queda listo para que el
usuario complete su propia prueba y decida si guarda.

## Archivos y protección

- `cnf/appsettings.Instalacion.json`: versión, conexión cifrada y fecha de guardado.
  No contiene una cadena de conexión en claro y no se edita manualmente.
- `cnf/llavero-instalacion/`: llaves de Data Protection, protegidas con DPAPI de Windows
  para la identidad que ejecuta el proceso. Deben conservarse con la configuración.
- `cnf/clave-instalacion.txt`: clave que autoriza las pruebas y el primer guardado.

La aplicación lee y descifra el JSON antes del registro de los servicios normales.
La conexión explícita de la configuración existente tiene prioridad sobre este archivo.
Al finalizar, el proceso carga la conexión y abre la aplicación normal automáticamente.

La carpeta debe estar fuera de `wwwroot`, sin exposición web y accesible únicamente
al responsable del alojamiento y a la identidad de la aplicación, con escritura para
esta última. El código rechaza ubicarla dentro de `wwwroot`; el modo de instalación
solo publica sus tres recursos y endpoints. El SDK excluye `cnf` del paquete publicado
y Git la ignora. Al actualizar, no borrar ni reemplazar la carpeta del servidor.

El cifrado corresponde a Windows y a la identidad original del proceso. Cambiar de
usuario, mover la instalación o pasar de Kestrel interactivo a otra identidad de IIS
requiere preparar la transición antes: copiar solo el JSON no basta. No eliminar el
llavero para resolver un error de descifrado.

## Después de guardar

El formulario de conexión queda cerrado, incluso ante peticiones directas o concurrentes.
Al recargar se comprueba el operador, sin devolver servidor, usuario o contraseña.
Un archivo dañado también bloquea el reemplazo y muestra un aviso para el responsable.

Si ya se completó la configuración normal, el siguiente arranque utiliza esa conexión.
Si faltan JWT, almacenamiento, correo o modo de PAC, se conserva la pantalla de
conexión guardada y se enumeran las claves de arranque pendientes. No se crean valores
ficticios ni se activa Development para eludir las validaciones.

## Segundo paso: operador principal

Después de guardar SQL, el asistente consulta la tabla de operadores de esa misma base.
No necesita reiniciar para mostrar el formulario de nombre, correo, contraseña y
confirmación. Si se cierra el navegador, el segundo paso reaparece al volver a entrar.
Se exige nuevamente la clave de instalación si ya no está en el formulario.

`POST /api/instalacion/operador` comprueba la clave, valida los campos en el servidor
y solo permite crear si no existe ningún operador, incluidos los inactivos. No basta
con que falte uno marcado como principal: si existen otros, corresponde al responsable
revisar esa situación, no crear un administrador desde una pantalla de primera instalación.

El servidor asigna `Activo=true`, `EsPrincipal=true` y todos los permisos del panel;
el navegador no puede elegirlos. La contraseña tiene entre 12 y 128 caracteres y se
guarda mediante `PasswordHasher<OperadorPlataforma>`, igual que el acceso habitual.
No se devuelve, no se manda por correo y no entra en la bitácora. El alta, sus permisos
y la bitácora se guardan en una sola transacción SQL con bloqueo exclusivo para evitar
dos administradores creados por solicitudes o procesos concurrentes.

Una vez creado, el alta inicial queda cerrada también por API y después de reiniciar.
Con la configuración completa, el asistente cambia al inicio de sesión sin reinicio
manual. No autentica al usuario: debe ingresar su correo y contraseña.
`POST /api/instalacion/finalizar` exige la clave, una conexión válida y un operador
principal. Envía la respuesta antes de cerrar el host de instalación; el mismo proceso
reconstruye los servicios con la configuración guardada. La página espera la
disponibilidad del servidor y abre `/operador/iniciar-sesion`. Si se recarga la
pantalla final sin la clave en memoria, solicita esa clave al pulsar **Entrar**.
Si hay parámetros pendientes o está forzado `Instalacion:SoloConexion`, no finaliza.
Guardar SQL y crear al administrador no sustituye configurar JWT, almacenamiento,
correo o el modo de PAC. Para revisión sin PAC se utiliza Staging con el modo
`Deshabilitado`, no credenciales ficticias.

La prueba local actual apunta a `Facturacion1.1`, con autenticación de Windows y
confianza explícita en el certificado SQL local. El script instaló 82 tablas, 59
migraciones y cinco procedimientos. Los catálogos SAT se preparan automáticamente
al abrir la aplicación normal, después del alta del operador.

## Carga automática de catálogos SAT

La publicación incluye cinco libros `.xls` en `CatalogosSAT/`, fuera de `wwwroot`.
El equipo que genera el publish debe tener los archivos en la carpeta `CatalogosSAT`
del proyecto: no se descargan al iniciar y no están versionados en Git.

Al iniciar el servidor, una tarea prepara los 28 catálogos utilizados por el sistema:
21 de CFDI, cuatro de Carta Porte 3.1 y tres de Comercio Exterior. Solo importa los
que no tienen versión registrada y datos; una carga interrumpida retoma los faltantes
en el siguiente arranque. Un bloqueo de sesión SQL evita dos cargas simultáneas sobre
la misma base. No se borran ni se actualizan datos de empresas.

Mientras se preparan, las páginas muestran el avance y las operaciones de negocio
devuelven 503. El estado se consulta en `GET /api/instalacion/catalogos`, sin datos
personales ni credenciales. Al terminar, la pantalla se recarga y abre el sistema.
Si falla, muestra una referencia que corresponde al error registrado en el servidor.
Los archivos ausentes o una base sin el esquema requerido no se consideran una carga
completada. Se puede configurar otra carpeta privada con `CatalogosSat:Directorio`.

Esto es carga inicial automática, no una actualización diaria contra el SAT. Se usan
las versiones incluidas en la publicación; una actualización fiscal posterior requiere
validar sus archivos y recargar los catálogos afectados.

Verificación local: 28 catálogos preparados, 52 513 claves de productos y servicios,
95 748 códigos postales y 145 366 colonias. La interfaz pasó de la pantalla de avance
al acceso normal. No se configuraron PAC ni pasarela de pagos.
La publicación de prueba incluyó los cinco libros fuera de `wwwroot`; una petición
HTTP al libro devolvió 404. En un segundo arranque del artefacto publicado se
reconocieron los 28 catálogos y se importaron cero, sin repetir la carga inicial.

## Comprobaciones de esta fase

### Arranque de revisión sin PAC ni pasarela

La configuración técnica se prepara una sola vez, en Windows y Staging, sin poner
credenciales ficticias de correo o PAC. Desde la carpeta de la publicación:

```powershell
dotnet .\Facturacion.Server.dll --environment Staging --Instalacion:PrepararRevision=true --Instalacion:Directorio=C:\Facturacion\cnf
```

En desarrollo, después de compilar, ejecutar el mismo comando contra
`Facturacion.Server/bin/Release/net10.0/Facturacion.Server.dll`, desde la carpeta
`Facturacion.Server`, utilizando el directorio privado elegido para esa instalación.
Después arrancar sin `Instalacion:PrepararRevision`, con Staging y el mismo directorio.

Se crea `cnf/appsettings.ArranqueRevision.json`, protegido con Data Protection y DPAPI,
con una clave JWT aleatoria, certificado nuevo para proteger el llavero, rutas absolutas
de archivos/llavero y `Pac:Modo=Deshabilitado`. Repetir el comando no rota ni reemplaza
secretos. La configuración explícita del alojamiento conserva prioridad. Esta
configuración de revisión se rechaza fuera de Staging. Hay que respaldar toda la carpeta
privada y conservar la identidad de Windows que la creó; nunca subirla al repositorio.

El ajuste `Correo:PermitirConfiguracionPosterior=true` solo es válido en Staging:
permite entrar al panel sin SMTP inicial y configurarlo después desde el operador.
Se utiliza el servicio SMTP real, no un simulador ni un envío por consola. Mientras
no exista servidor y remitente, el servicio rechaza el envío; registro con verificación,
recuperación de contraseñas y otras funciones que dependan de correo no quedan
validadas hasta completar y probar el SMTP. Production sigue exigiendo configuración.

El PAC deshabilitado no registra proveedor real ni simulado: timbrar devuelve error
antes de reservar folio o timbre. No se configura ninguna pasarela de pagos y no se
confunde esa integración con el complemento fiscal de pagos.

Se añadieron tres pruebas de persistencia/cifrado, prioridad de configuración y rechazo
en Production. En esta revisión pasaron quince pruebas y se omitió la prueba SQL de
alta concurrente porque no se proporcionó una base temporal; esa prueba había pasado
en la fase anterior.

Pruebas automatizadas de validación, construcción segura de la cadena, autorización,
cifrado, lectura en otro arranque, guardado único concurrente, archivo dañado y rechazo
de rutas públicas: once aprobadas, incluidas las dos comprobaciones de autenticación
Windows y obligatoriedad de credenciales SQL. La compilación Release terminó con
cero advertencias y cero errores.

Se comprobó el formulario en el navegador y por HTTP: recursos, CSP y no-store;
denegación de archivos privados y de módulos normales; 403 sin clave en probar y
guardar; validación de campos; rechazo controlado de una conexión inaccesible y ausencia
de JSON tras una prueba fallida. El arranque normal con la configuración local existente
también se verificó en puertos separados, conservando los procesos anteriores.

En la segunda fase pasaron trece pruebas: las once de conexión y dos del operador.
La prueba SQL usa una base temporal cuyo nombre debe empezar por
`FacturacionWizardPruebas_` y requiere la variable
`FACTURACION_PRUEBAS_INSTALACION_SQL`; se omite si no se proporciona. Comprueba dos
altas concurrentes, hash compatible, todos los permisos, bitácora sin contraseña ni
hash y rechazo de otra alta aun tras desactivar al operador y quitar su marca principal.
No crea administradores en la base de trabajo del usuario.

El cifrado y el guardado único se probaron con cadenas sintéticas en directorios de
pruebas. No se dispone de usuario y contraseña SQL del alojamiento para acreditar aún
el recorrido positivo del formulario contra esa base. No se modificaron sus datos ni
se actualizó la publicación del FTP en esta fase.
