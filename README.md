# Sistema de facturación CFDI 4.0

Aplicación web progresiva (PWA) de facturación electrónica **CFDI 4.0** para México,
multiempresa. Sigue en desarrollo y revisión; el timbrado real requiere conectar y
validar las credenciales del PAC. Consulta el [estado actual](REPORTE_ESTADO_SISTEMA.md)
antes de asumir que una función ya está lista en Azure.

## Cómo correrlo

**[docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md)** — pasos para dejarlo corriendo en una
máquina nueva.

La estructura de la base de datos la crean las migraciones; no hay que armarla a mano.
Sí hay que aplicar las migraciones en cada entorno. Como mínimo hay que conseguir
aparte la configuración local (`appsettings.Development.json`), la llave maestra que
protege los certificados y los catálogos del SAT. Carta Porte y Comercio Exterior
requieren además sus catálogos específicos.

## Estructura

```
Facturacion.Client    Blazor WebAssembly (PWA)
Facturacion.Server    ASP.NET Core Web API — además sirve los estáticos del Client
Facturacion.Shared    DTOs y contratos compartidos
tests/                pruebas contra SQL Server
```

## Documentación

- [Estado actual](REPORTE_ESTADO_SISTEMA.md): implementado, límites de validación y pendientes.
- [Resumen ejecutivo](RESUMEN_EJECUTIVO.md): explicación para quienes no programan.
- [Arranque local](docs/ARRANQUE-LOCAL.md): configuración y ejecución en desarrollo.
- [Despliegue](docs/DESPLIEGUE.md): variables, PAC y comprobaciones del servidor.
- [Arquitectura](ARQUITECTURA.md) y [AGENTS.md](AGENTS.md): reglas del proyecto.
- [UI funcional anterior](docs/UI_Funcional.md): inventario legado de campos y reglas,
  no referencia visual ni garantía de vigencia fiscal.

Los planes [A](PROMPT-FASES-A.md), [B](PROMPT-FASES-B.md), el
[reparto original](REPARTO-EQUIPO.md) y los informes con fecha anterior se conservan
como historial. No sustituyen el reporte de estado actual.
