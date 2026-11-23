# Sistema de facturación CFDI 4.0

Documentación de entrega para revisión · Área de Sistemas · 19 de noviembre de 2026.

Aplicación web para administrar cuentas con varias empresas, sus usuarios, documentos y compras de timbres. El servidor ASP.NET Core sirve tanto la API como el cliente Blazor WebAssembly; se publica un solo sitio.

La entrega permite revisar creación, guardado, modificación, cálculo, PDF, XML y envío de documentos. Incluye factura estándar, Constructoras, Notaría, Comercio Exterior 2.0, Carta Porte 3.1 para autotransporte nacional propio y complemento de pagos 2.0. El operador también prepara las facturas de compras de paquetes de timbres.

El timbrado con PAC real, el uso de CSD reales y el cobro mediante pasarela quedan para la integración del responsable de la empresa. Los archivos del modo de revisión no acreditan una emisión fiscal ni tienen un UUID certificado por un PAC.

## Documentación de entrega

- [Manual de entrega](docs/Manual-de-entrega.md) reúne operación, arquitectura, IIS, mantenimiento, pruebas e integración futura.
- [Base de datos](docs/Base-de-datos.md) describe la propiedad de los datos, las relaciones y las columnas.

La [guía técnica de aprendizaje](docs/Guia-tecnica-para-continuar-el-proyecto.md) y los informes de fases quedan como referencias del repositorio.

## Estructura

```text
Facturacion.Client/   Interfaz Blazor WebAssembly y PWA
Facturacion.Server/   API, reglas de negocio y publicación del cliente
Facturacion.Shared/   DTO y contratos compartidos
docs/                Manuales y referencia funcional; los SQL se conservan en local
herramientas/         Preparación y verificación de entregas
tests/                Pruebas automatizadas
```

## Ejecución desde el proyecto

Con SDK .NET 10 instalado, desde la raíz del repositorio:

```powershell
dotnet restore .\Facturacion.Server\Facturacion.Server.csproj
dotnet build .\Facturacion.Server\Facturacion.Server.csproj -c Release
dotnet run --project .\Facturacion.Server\Facturacion.Server.csproj --launch-profile https
```

El perfil HTTPS utiliza `https://localhost:7123`. Las credenciales locales no forman parte del repositorio. Para una instalación nueva, preparar primero la base y seguir el manual de instalación.

## Entrega al servidor

La publicación compatible preparada utiliza Windows x64 y .NET 10.0.14 incluido. IIS sigue necesitando AspNetCoreModuleV2 del Hosting Bundle. Una publicación dependiente del framework necesita el runtime compatible instalado; no se deben mezclar archivos de ambas modalidades.

El acceso por nombre requiere resolución DNS o local, binding de IIS y certificado HTTPS acordes. FTP solo transfiere archivos; no instala IIS ni ejecuta el backend.

Las consultas SQL están configuradas para compatibilidad 120. El script acumulado se comprobó con sintaxis de SQL Server 2014 y con compatibilidad 120 en un motor posterior; no se ha probado en un motor SQL Server 2014 nativo. El asistente configura una base existente: no sustituye la ejecución de su esquema.

Antes de sustituir la publicación, respaldar la base y la configuración privada, conservar sus llaves y aplicar el script que corresponda al esquema de partida. Consultar el [procedimiento de instalación y actualización](docs/Manual-de-entrega.md#iis).
