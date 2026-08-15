# Factura-Multitenat

Aplicación web progresiva (PWA) de facturación electrónica **CFDI 4.0** para México,
multiempresa.

## Cómo correrlo

**[docs/ARRANQUE-LOCAL.md](docs/ARRANQUE-LOCAL.md)** — pasos para dejarlo corriendo en una
máquina nueva.

La base de datos la crean las migraciones; no hay que armarla a mano. Lo que sí hay que
conseguir aparte, porque el repositorio no puede llevarlo dentro, son tres cosas: la
configuración local (`appsettings.Development.json`), la llave maestra que protege los
certificados, y el archivo de catálogos del SAT.

## Estructura

```
Facturacion.Client    Blazor WebAssembly (PWA)
Facturacion.Server    ASP.NET Core Web API — además sirve los estáticos del Client
Facturacion.Shared    DTOs y contratos compartidos
tests/                pruebas contra SQL Server
```

## Documentos del proyecto

| | |
|---|---|
| `CLAUDE.md` | Contexto permanente: stack, reglas de seguridad, dominio y diseño |
| `REPARTO-EQUIPO.md` | Reparto del trabajo entre las dos mitades y contratos congelados |
| `PROMPT-FASES-A.md` | Fases de la mitad A |
| `docs/UI_Funcional.md` | Inventario de campos y reglas del sistema de escritorio anterior |
