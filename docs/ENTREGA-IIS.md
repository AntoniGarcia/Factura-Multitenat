# Entrega para revisión en Windows / IIS

## Contenido del paquete

- `sitio/`: publicación conjunta del servidor y el cliente. Su contenido completo se
  instala en la raíz de la aplicación IIS, prevista como `/cfdiAdmon` en FTP.
- `base-datos/`: script SQL e instrucciones. Entregar al administrador; no subir
  dentro del sitio ni en `wwwroot`.
- `insumos-sat/`: catálogos, esquemas y XSLT disponibles localmente. Ubicarlos fuera
  del directorio público, configurar `EsquemasSat__Ruta` y ejecutar los importadores.

No incluye empresas, documentos, certificados, claves ni configuración local.
Los archivos publicados contienen todos los cambios actuales del árbol de trabajo,
incluidos los aún no confirmados en Git. Este paquete no es una copia de producción.

## Condiciones de instalación

1. IIS con Hosting Bundle de .NET 10, aplicación como sitio raíz, grupo de aplicaciones
   sin código administrado y arquitectura compatible con las dependencias nativas.
   La publicación es dependiente del framework, no un ejecutable autónomo.
2. HTTPS válido. El acceso por FTP no configura IIS, HTTPS ni SQL Server.
3. Base vacía de SQL Server con Full-Text Search; ejecutar el script completo en SSMS.
4. Configurar las variables indicadas debajo y dar permisos de escritura al grupo
   de aplicaciones sobre las dos rutas persistentes.
5. Cargar los catálogos. Reiniciar la aplicación y comprobar `/api/version`, acceso,
   autocompletados, guardado de borradores y PDF. Verificar Brotli en la respuesta HTTP.

No hay asistente web de instalación ni ejecución automática del SQL al arrancar.
El instalador mencionado por el encargado no ha sido identificado. Esta entrega
usa la configuración existente y no modifica el funcionamiento de la aplicación.

## Configuración en IIS

Asignar variables al proceso de la aplicación, por ejemplo en la sección
`environmentVariables` de `aspNetCore` del `web.config` generado. El administrador
debe introducir los valores reales en el servidor y conservarlos al actualizar.
No guardar secretos en Git, en `wwwroot`, en capturas ni en documentación.

Variables necesarias:

- `ASPNETCORE_ENVIRONMENT`: `Staging`.
- `Pac__Modo`: `Deshabilitado`. Sin PAC no se simula una emisión fiscal.
- `AllowedHosts`: dominio exacto del sitio.
- `ConnectionStrings__BaseDeDatos`: conexión real a SQL Server con cifrado y
  validación de certificado según la configuración del administrador.
- `Jwt__Emisor` y `Jwt__Audiencia`: identificadores consistentes del despliegue.
- `Jwt__ClaveDeFirma`: secreto aleatorio de al menos 32 caracteres.
- `Almacen__Raiz`: ruta absoluta persistente para archivos, fuera del sitio público.
- `Almacen__RutaLlavero`: ruta absoluta persistente para el llavero, fuera del sitio público.
- `Almacen__LlaveMaestraPfx`: certificado nuevo en base64 generado en el servidor.
- `Correo__Servidor`, `Correo__RemitenteCorreo`: SMTP real del sistema.
- `Correo__Puerto`, `Correo__Usuario`, `Correo__Contrasena`, `Correo__UsarTls`:
  configuración real del proveedor de correo.
- `EsquemasSat__Ruta`: ruta absoluta de los insumos SAT.

No activar `Development` para eludir requisitos y no transferir
`appsettings.Development.json` ni las claves de la instalación local.

## Comandos desde el servidor

Ejecutar en la carpeta de la publicación con las variables anteriores configuradas
también en la consola. Los valores de IIS no pasan automáticamente a esa consola.

Generar la clave maestra antes de arrancar; guardar su salida por un medio privado
y usarla en `Almacen__LlaveMaestraPfx`. No regenerarla con cada actualización:

```powershell
dotnet .\Facturacion.Server.dll --generar-llave-maestra
```

Tras ejecutar SQL, cargar los libros existentes usando sus rutas reales:

```powershell
dotnet .\Facturacion.Server.dll --cargar-catalogos C:\FacturacionDatos\SAT\catCFDI_V_4_20260806.xls
dotnet .\Facturacion.Server.dll --cargar-catalogos-carta-porte C:\FacturacionDatos\SAT\CatalogosCartaPorte31.xls
dotnet .\Facturacion.Server.dll --cargar-catalogo-comercio c_INCOTERM C:\FacturacionDatos\SAT\c_INCOTERM20.xls
dotnet .\Facturacion.Server.dll --cargar-catalogo-comercio c_UnidadAduana C:\FacturacionDatos\SAT\c_UnidadAduana20.xls
dotnet .\Facturacion.Server.dll --cargar-catalogo-comercio c_FraccionArancelaria C:\FacturacionDatos\SAT\c_FraccionArancelaria_20240513.xls
```

Las rutas son ejemplos: deben corresponder al almacenamiento real del servidor.
Estos libros son los disponibles en la máquina de desarrollo; no se ha comprobado
en esta fase si el SAT publicó versiones posteriores.

Crear el primer operador con el correo real elegido por el responsable:

```powershell
dotnet .\Facturacion.Server.dll --crear-operador CORREO_REAL "Administrador"
```

El comando imprime una contraseña generada una vez. Entregarla privadamente.
El cliente puede registrarse por la interfaz; su verificación requiere correo funcional.
No crear usuarios mediante INSERT de contraseñas ni habilitar el sembrado local.

## Demostración de respaldo

Si el servidor no está configurado, mostrar la instalación local existente en
`https://localhost:7123`. No se garantiza el despliegue solo con transferir archivos.
La revisión no cubre PAC ni pasarela de pagos y no es certificación fiscal.

El FTP observado no soporta TLS. No transferir credenciales o claves por ese canal;
pedir al administrador un medio privado para configurarlas directamente en el servidor.
