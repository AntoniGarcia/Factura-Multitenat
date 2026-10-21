# Instalacion en un sitio IIS existente

Para una instalacion NUEVA con el sitio IIS ya creado:

1. El sitio debe tener una carpeta fisica vacia, un grupo exclusivo con identidad
   ApplicationPoolIdentity y un enlace HTTPS con certificado valido.
2. Extraer TODO el ZIP en una carpeta de trabajo fuera de cualquier sitio IIS.
   No copiar manualmente `sitio/` al destino antes de ejecutar el instalador.
3. Clic derecho en **INSTALAR.cmd** -> **Ejecutar como administrador**.
4. Elegir el sitio de la lista, revisar rutas y confirmar. El instalador copia la
   publicacion, prepara la carpeta privada y permisos, y arranca ese sitio.
5. Abrir la direccion que muestra y completar el wizard con los datos de la base
   existente. Si falta el operador principal, crearlo desde el formulario.

No crea ni modifica bases, logins o permisos SQL; no ejecuta el script de estructura.
No crea sitios, certificados, enlaces ni reglas de firewall. No cambia otros sitios.
No reemplaza una publicacion o carpeta privada existente: esta fase NO actualiza.
Si el destino ya tiene archivos, detenerse y conservarlos; no vaciarlo para forzar una instalacion nueva.

La carpeta privada queda en `%ProgramData%\FacturacionRevision\NOMBRE_DEL_GRUPO\cnf`.
El asistente no pide clave desde localhost en el propio servidor; desde otra PC
conserva la clave, generada al primer acceso en `clave-instalacion.txt` dentro de `cnf`.
El certificado debe corresponder a la direccion y ser confiable en el navegador.
Si Windows bloquea los scripts, revisar el origen y la politica del servidor;
el lanzador no desactiva ni evita la politica de ejecucion.

Para comprobar el plan sin modificar el servidor, desde PowerShell como administrador:

```powershell
.\INSTALAR.ps1 -Sitio 'NOMBRE_REAL_DEL_SITIO' -WhatIf
```

## Preparacion manual alternativa

Requiere IIS con su consola y herramientas de administración, Hosting Bundle .NET 10
y una base con el esquema del sistema y su usuario. Este paquete es de revisión, sin PAC
ni pasarela. No ejecuta scripts SQL automáticamente ni instala servicios.

1. Extraer TODO el ZIP y dejarlo en su ubicación definitiva. No ejecutar dentro del ZIP.
2. Abrir **Windows PowerShell como administrador**, entrar a la carpeta extraída donde
   están este archivo y `PREPARAR-IIS.ps1` y ejecutar:

```powershell
.\PREPARAR-IIS.ps1 -Grupo 'Marpa' -NombreHost 'localhost'
```

`Marpa` será el grupo exclusivo de esta aplicación. Para un dominio, sustituir
`localhost` por el nombre DNS real, sin https:// ni puerto. Se conserva localhost
como alternativa para completar el asistente sin clave desde el servidor.
Puede consultar previamente el plan, sin modificar nada, añadiendo `-WhatIf`.

3. En IIS crear el sitio (o seleccionar el que apunta a ESTA publicación), elegir
   el grupo `Marpa`, indicar la ruta física de `sitio` que imprime el comando y
   configurar su enlace **HTTPS con certificado válido**. No apuntar a `wwwroot`.
4. Abrir `/operador` desde el navegador del propio servidor mediante localhost y
   el puerto HTTPS configurado. Capturar servidor SQL, base y usuario/contraseña;
   guardar y crear al primer operador si no existe. Los catálogos faltantes se cargan solos.

Si solo se configura un enlace por dominio, o se entra desde otra PC, el asistente
seguirá pidiendo la clave: está en `clave-instalacion.txt` dentro de la carpeta
privada `cnf` que imprime el preparador. El modo sin clave es únicamente para localhost.

## Qué hace el comando

- Crea el grupo si falta, sin código administrado, de 64 bits y con perfil habilitado.
- Crea `%ProgramData%\FacturacionRevision\Marpa` y restringe su acceso a SYSTEM,
  Administradores y la identidad `IIS APPPOOL\Marpa`.
- Da a esa identidad lectura/ejecución sobre `sitio`.
- Ajusta el `web.config` de revisión, AllowedHosts y rutas; guarda un respaldo privado.
- No reinicia otros sitios, no configura SQL, no crea certificados ni abre puertos.

Puede elegir otra carpeta privada con `-DirectorioPrivado 'D:\FacturacionDatos\Marpa'`.
No puede quedar dentro del sitio ni contener archivos de otra instalación. La preparación
es repetible para el mismo grupo, publicación y carpeta identificada; no regenera llaves.
No es un migrador: rechaza cambiar la ruta privada de una aplicación ya configurada.

No ejecutar el preparador de una instalación NUEVA sobre la instalación anterior de Luis
sin revisar sus rutas. Para actualizar una instalación existente conservar web.config,
identidad y carpeta privada; seguir LEEME-IIS.md.

Si el usuario SQL ya está creado, capturarlo en el asistente. Si se usa autenticación
Windows, SQL debe reconocer a `IIS APPPOOL\Marpa` en la misma máquina, o a la cuenta
de dominio apropiada si SQL es remoto. Dar acceso a SQL sigue siendo tarea de su administrador.

Si Windows bloquea un script descargado, revisar primero su origen y contenido y seguir
la política de ejecución del servidor. No desactivar globalmente esa política.

Para detalles de certificado, diagnóstico, SQL y actualizaciones: `LEEME-IIS.md`.
