# Facturación: prueba en otra PC Windows

Este paquete incluye backend y cliente publicados. No necesita Visual Studio ni IIS.
Es una revisión local, no una instalación de producción. El PAC está deshabilitado y
la pasarela de pagos no está integrada. No permite emitir comprobantes timbrados.

## 1. Requisitos

- Windows x64 y .NET 10 SDK para Windows: https://dotnet.microsoft.com/download/dotnet/10.0
  El SDK incluye los runtimes y la herramienta para preparar HTTPS local.
- Acceso a SQL Server con búsqueda de texto completo (Full-Text Search) instalada.
- SQL Server Management Studio (SSMS) o una persona que pueda ejecutar el script SQL.
- Un navegador actualizado.

## 2. Preparar la base una sola vez

1. Extrae TODO el ZIP en una carpeta local. No ejecutes desde el ZIP.
2. En SSMS conecta al servidor SQL y crea una base vacía, por ejemplo `FacturacionRevision`.
3. Abre `base-datos/Facturacion.Instalacion.sql` y selecciona esa base en el desplegable
   de SSMS antes de ejecutar el script. El script crea las tablas, no la base.
4. Ejecuta el script completo. Si hay errores, corrígelos antes de seguir.
5. Conserva el nombre del servidor, la base y el método de autenticación.

No uses una base de producción ni una copia con datos reales para esta prueba.
La base NO viene dentro del ZIP y SQL Server NO se instala automáticamente.

## 3. Iniciar

1. Haz doble clic en `INICIAR.cmd`. Si Windows solicita confiar en el certificado
   HTTPS local, confirma solo si corresponde a tu equipo y a localhost.
2. Cuando aparezca `Now listening`, abre https://localhost:7245/operador en ESA PC.
3. Para el asistente, copia la clave de este archivo local (puedes pegar la ruta en el Explorador):
   `%LOCALAPPDATA%\FacturacionRevisionPC\cnf\clave-instalacion.txt`.
4. Captura servidor y base. Para SQL Server local puedes usar autenticación de Windows
   si tu usuario tiene acceso; no necesitas inventar un usuario o contraseña SQL.
   Para autenticación SQL usa las credenciales proporcionadas por el administrador.
5. Prueba y guarda la conexión. Si usas un SQL local con certificado autofirmado,
   la opción de confiar en su certificado es explícita; no la actives para servidores desconocidos.
6. Si no existe operador principal, el asistente permite crearlo.
7. Finaliza. El sistema cargará los catálogos SAT que falten y abrirá el acceso del operador.
   No cierres la consola mientras carga. Los siguientes arranques reutilizan la configuración.

Mantén la consola abierta mientras uses el sistema. Para detenerlo: Ctrl+C.
Para volver a iniciarlo: ejecuta otra vez `INICIAR.cmd`.
Para abrir el cliente después de instalar: https://localhost:7245/.

## 4. Qué contiene y dónde se guarda

- `sitio/`: backend, dependencias, esquemas y catálogos SAT.
- `sitio/wwwroot/`: cliente Blazor; no se ejecuta separado del backend.
- `base-datos/`: script de estructura para SQL Server; no se publica como contenido web.
- `%LOCALAPPDATA%\FacturacionRevisionPC\cnf/`: configuración cifrada, llaveros y archivos
  de esta instalación. La cadena de conexión NO se guarda en claro en appsettings.json.

Las claves se generan en la PC receptora y están vinculadas a su usuario de Windows.
Usa siempre el mismo usuario para arrancar. No copies `cnf` de otra PC, no lo compartas,
no borres sus llaves y no intentes usarlo después en IIS con otra identidad sin una migración.
Este ZIP no contiene las credenciales ni los datos de la instalación de Luis.

## 5. Correo y límites de la prueba

Configura el SMTP del sistema desde el panel del operador si probarás registro y envío
de códigos. No se incluyen credenciales de Gmail; mientras falten, esos envíos no funcionarán.
El SMTP de una empresa es independiente y se usa para sus comprobantes.

El puerto solo escucha en localhost: esto no expone la aplicación a Internet ni sustituye
la posterior configuración del servidor/IIS. El FTP solo transfiere archivos, no ejecuta el backend.

Si el puerto 7245 está ocupado, detén la otra instancia o abre CMD en esta carpeta y ejecuta:

```bat
set FACTURACION_REVISION_PUERTO=7249
INICIAR.cmd
```

Después abre https://localhost:7249/operador.
`INICIAR.cmd /comprobar` revisa requisitos básicos sin arrancar ni crear configuración.
