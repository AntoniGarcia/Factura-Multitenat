# Dónde se guarda la configuración

La carpeta pública y la configuración privada están separadas. La ruta privada
la indica `Instalacion__Directorio` dentro del `web.config` del sitio.

Con el instalador y el grupo `FacturaN`, la ruta es:

```text
C:\ProgramData\FacturacionRevision\FacturaN\cnf
```

Si se utiliza otro nombre de grupo o una ruta manual, cambia esa ubicación.

## Conexión SQL

`appsettings.Instalacion.json` guarda la cadena completa cifrada en
`ConexionProtegida`, junto con la versión y la fecha de guardado. Incluye el servidor,
la base, el método de autenticación y, cuando se usa acceso SQL, el usuario y la contraseña.
La carpeta debe quedar fuera de `wwwroot` y con permisos solo para el responsable y
la identidad de la aplicación.

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

Al arrancar en Staging, la aplicación convierte el formato anterior, versión 2,
en un archivo cifrado en la misma ubicación antes de usar la cadena. Si esa versión
anterior está en la raíz del sitio y la raíz es de solo lectura, detén el sitio y
muévela a `cnf` antes del primer arranque actualizado. El grupo IIS necesita permiso
Modificar en `cnf` y su perfil de Windows cargado. Una conexión cifrada anterior
conserva su archivo y llavero.

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
