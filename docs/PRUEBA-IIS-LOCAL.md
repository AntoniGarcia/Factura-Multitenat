# Prueba local de IIS — 20 de octubre de 2026

## Alcance comprobado

Se creó el sitio aislado `FacturacionRevisionLocal20261020`, con grupo propio,
runtime no administrado, proceso de 64 bits y perfil de usuario habilitado.
Solo tiene enlaces HTTPS de loopback: `127.0.0.1:7443` y `[::1]:7443`.
No se modificaron `Default Web Site` ni `Prueba11`.

Entrada: https://localhost:7443/operador

La publicación está en `tmp/iis-20261020/publicado`. La configuración privada está
en `tmp/iis-20261020/privado/cnf`, fuera de la raíz del sitio, con acceso limitado
a SYSTEM, administradores y la identidad del grupo de aplicaciones.
Los logs temporales de diagnóstico están en `privado/logs`, no en el sitio público.

El certificado HTTPS local, confiable en esta PC y con vigencia de tres meses,
solo sirve para esta prueba. No se entrega como certificado del servidor real.

## Arranque de revisión

La publicación de prueba usa `hostingModel=outofprocess`, `Staging`,
`Instalacion__ArranqueAutomatico=true`, `Instalacion__Directorio` con ruta absoluta
privada y `Pac__Modo=Deshabilitado`. No utiliza `INICIAR.cmd` ni certificados HTTPS
de desarrollo de otro usuario.

El arranque automático genera la configuración cifrada bajo la identidad del proceso
IIS. Requiere una carpeta fuera del sitio, conserva las claves existentes y rechaza
Production. No debe activarse como sustituto de una configuración de producción.
El perfil del grupo debe permanecer habilitado: DPAPI protege estas claves con la
identidad de Windows de la aplicación.

El asistente respondió HTTP 200 y reportó conexión todavía no guardada, sin
configuración de arranque pendiente. Después de reciclar exclusivamente el grupo
de prueba volvió a responder; el archivo cifrado conservó exactamente su hash.
Las cinco pruebas automatizadas del arranque cifrado pasaron.

## Comprobaciones posteriores

- Luis autorizó reutilizar `Facturacion1.1` y eliminar al único principal. Se retiraron
  sus 25 permisos y 7 sesiones, con respaldo cifrado DPAPI en la carpeta temporal.
  Se conservaron las demás tablas y el historial; se registró el restablecimiento.
- Se concedió acceso a `IIS APPPOOL\FacturacionRevisionLocal20261020` únicamente
  en esa base: lectura/escritura de datos y ejecución de los cinco procedimientos.
  Se comprobó que no es sysadmin, db_owner ni puede alterar la base; no tiene acceso
  a `FacturacionDev`. La prueba de conexión desde IIS detectó las 82 tablas.
- Luis completó el asistente y confirmó entrada al sistema y acceso después de
  reciclar el grupo. También confirmó la secuencia solicitada de catálogos,
  guardado/reapertura de borrador y PDF; esas últimas comprobaciones son manuales
  reportadas por Luis, no una auditoría fiscal de todas las variantes.
- La comprobación final de entrega confirmó los 28 catálogos listos y HTTP 200 con
  `Content-Encoding: br` para el cargador Blazor. Pasaron las 19 pruebas de conexión,
  claves y catálogos; la compilación terminó sin advertencias ni errores.

La entrega para IIS se prepara separadamente del ZIP de prueba para PC. Las rutas,
el certificado y la identidad del servidor del ingeniero aún deben configurarse allí.
PAC y pasarela siguen excluidos. La prueba local no garantiza el despliegue remoto.

Para volver a consultar requisitos: `herramientas/entrega-pc/Comprobar-IIS.ps1`
desde PowerShell como administrador. Los scripts `Crear-PruebaIIS.ps1` y
`Verificar-PruebaIIS.ps1` son específicos de esta prueba local, no instaladores
generales para ejecutar sin revisar en el servidor del ingeniero.
