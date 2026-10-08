# Instalación de la base de datos

`Facturacion.Instalacion.sql` contiene las migraciones desde la primera hasta
`20261007215004_20261007_B_ReceptorExtranjero`. Incluye tablas, índices,
procedimientos y los datos iniciales definidos por las migraciones. No exporta
usuarios, empresas, documentos, certificados ni contraseñas del entorno local.

## Ejecución

1. Crear una base vacía en SQL Server y seleccionarla en SQL Server Management Studio.
2. Abrir `Facturacion.Instalacion.sql` y verificar la base seleccionada antes de ejecutar.
3. Ejecutar el archivo completo con una cuenta autorizada para modificar el esquema.
4. Comprobar que `__EFMigrationsHistory` incluye la última migración indicada arriba.
5. Cargar los catálogos del SAT siguiendo `../DESPLIEGUE.md` y configurar la aplicación.

El archivo no crea la base, ni usuarios SQL, ni credenciales. Requiere SQL Server
con búsqueda de texto completo disponible y un ejecutor que interprete `GO` (SSMS
o sqlcmd). No envolverlo completo en una transacción: las operaciones FULLTEXT
deben ejecutarse fuera de ella. Detener la ejecución ante cualquier error.

El control de `__EFMigrationsHistory` permite omitir migraciones ya aplicadas;
no repara una base modificada manualmente ni una migración aplicada parcialmente.
Para una base existente, hacer respaldo y revisar primero el script. No alojar
este archivo en `wwwroot` ni ofrecerlo como descarga pública.

## Regeneración

Desde la raíz del repositorio, compilar en Release y ejecutar:

```powershell
dotnet build Facturacion.sln -c Release
.\docs\sql\Generar-Script.ps1
```

El generador utiliza EF Core y adapta los procedimientos al SQL dinámico que
requiere el script condicional. No conecta ni aplica cambios a la base de datos.
La generación y revisión estática no sustituyen la prueba de instalación en el
SQL Server del alojamiento, que todavía no se ha confirmado.
