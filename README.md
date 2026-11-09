# Sistema de facturación CFDI 4.0

Documentación de entrega para revisión · 6 de noviembre de 2026.

Aplicación web para administrar cuentas con varias empresas, sus usuarios, documentos y compras de timbres. El servidor ASP.NET Core sirve tanto la API como el cliente Blazor WebAssembly; se publica un solo sitio.

La entrega permite revisar creación, guardado, modificación, cálculo, PDF, XML y envío de documentos. Incluye factura estándar, Constructoras, Notaría, Comercio Exterior 2.0, Carta Porte 3.1 para autotransporte nacional propio y complemento de pagos 2.0. El operador también prepara las facturas de compras de paquetes de timbres.

El timbrado con PAC real, el uso de CSD reales y el cobro mediante pasarela quedan para la integración del responsable de la empresa. Los archivos del modo de revisión no acreditan una emisión fiscal ni tienen un UUID certificado por un PAC.

## Manuales

- [Arquitectura](ARQUITECTURA.md): componentes, seguridad, aislamiento y comunicación.
- [Base de datos](docs/Base-de-datos.md): propiedad de los datos, relaciones y diccionario de tablas y columnas.
- [Instalación y actualización](docs/Instalacion-y-actualizacion.md): publicación, IIS, permisos, asistente, SQL y respaldos.
- [Funcionamiento](docs/Funcionamiento.md): operación de cuentas, empresas, documentos, correo y panel del operador.
- [Pruebas y pendientes](docs/Pruebas-y-pendientes.md): evidencia disponible y límites de la entrega.

Estos seis documentos forman la guía principal. Los informes fechados anteriores se conservan durante la consolidación como antecedentes; cuando contradigan estos manuales, no deben interpretarse como instrucciones actuales.

Para incorporar a una persona al desarrollo, consultar la [guía técnica de aprendizaje y mantenimiento](docs/Guia-tecnica-para-continuar-el-proyecto.md), disponible también en [Word](docs/Guia-tecnica-para-continuar-el-proyecto.docx). Explica el código desde sus conceptos básicos hasta los flujos completos de facturación.

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

Antes de sustituir la publicación, respaldar la base y la configuración privada, conservar sus llaves y aplicar el script que corresponda al esquema de partida. Consultar [Instalación y actualización](docs/Instalacion-y-actualizacion.md).
