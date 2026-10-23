# Dónde se guarda la configuración

La carpeta pública y la configuración privada están separadas. La ruta privada
la indica `Instalacion__Directorio` dentro del `web.config` del sitio.

Con el instalador y el grupo `FacturaN`, la ruta es:

```text
C:\ProgramData\FacturacionRevision\FacturaN\cnf
```

Si se utiliza otro nombre de grupo o una ruta manual, cambia esa ubicación.

## Conexión SQL

`appsettings.Instalacion.json` guarda la cadena completa, junto con la
versión del formato y la fecha de guardado. En esta revisión se activa
`Instalacion__ConexionSinCifrarRevision=true`, por lo que la conexión se guarda
legible en el campo `Conexion` (formato versión 2). Dentro de la cadena van servidor,
base, método de autenticación y las opciones del certificado. Si se usa acceso
SQL, también van el usuario y la contraseña en texto legible. Cualquier persona
con acceso al archivo puede leerlos. Por eso debe quedar fuera del sitio público
y con permisos solo para el responsable y la identidad de la aplicación.
Con autenticación Windows no se guardan credenciales SQL.

No se escriben esos parámetros en el `appsettings.json` público de la publicación.
Al arrancar, el servidor lee la cadena y la carga en memoria como
`ConnectionStrings:BaseDeDatos`. El navegador no recibe la cadena guardada.

## Otras llaves y archivos

- `appsettings.ArranqueRevision.json`: configuración de revisión cifrada, incluida
  la firma JWT, la llave maestra y las rutas de almacenamiento.
- `llavero-instalacion/`: llaves para descifrar los archivos anteriores.
- `llavero-aplicacion/`: llaves que utiliza la aplicación para proteger sus datos.
- `datos/`: archivos privados del sistema. No debe quedar dentro de `wwwroot`.
- Los datos de cuentas, empresas, operadores y comprobantes se guardan en SQL Server.
  Las contraseñas de acceso se guardan como hash, no como texto recuperable.
- El SMTP configurado en el operador se guarda en la base de datos; las contraseñas
  de correo están protegidas, no se entregan al cliente al consultar la configuración.

La protección usa Data Protection y DPAPI de Windows. Por eso deben conservarse
la carpeta privada, el equipo y la identidad del grupo IIS. Copiar solo los JSON
a otro servidor no garantiza poder descifrarlos.

La excepción de conexión legible no elimina el cifrado de la configuración de
arranque, correo o certificados. Fuera de Staging se rechaza activar esa opción.
Las conexiones cifradas anteriores siguen siendo compatibles; no se convierten
ni se reemplazan automáticamente. La conexión guardada no se cambia desde el asistente.

La clave de instalación del asistente se eliminó. No hay que confundirla con las
llaves de cifrado: estas sí son necesarias. Al arrancar la versión actual se
retira únicamente el antiguo archivo `clave-instalacion.txt`, si existía.
No borrar los llaveros ni los JSON para actualizar la aplicación.

## Datos del servidor IIS

El hostname, puerto, certificado y ruta del sitio se guardan en la configuración
de IIS. `web.config` contiene las opciones del proceso, entorno y ruta privada,
pero no la contraseña SQL capturada en el asistente.

Antes de compartir el sitio hay que completar el asistente y crear al operador.
En esta revisión no hay una clave adicional que limite quién puede configurarlo
durante ese primer acceso.
