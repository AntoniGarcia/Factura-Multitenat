# Arranque local

Cómo dejar el sistema corriendo en una máquina nueva, desde el repositorio recién clonado.

No hay que crear la base de datos a mano: la crean las migraciones. Lo que sí hay que
conseguir aparte son **tres cosas que el repositorio no puede llevar dentro**: la
configuración local, la llave maestra y el archivo de catálogos del SAT.

---

## 1. Requisitos

| | |
|---|---|
| .NET SDK | 10.0 |
| SQL Server | 2019 o superior, incluido Express |
| Herramienta EF | `dotnet tool install --global dotnet-ef` |

Comprueba el SDK:

```bash
dotnet --version
```

---

## 2. Configuración local

`appsettings.Development.json` está en `.gitignore` a propósito: lleva la clave de firma de
los tokens y la llave que protege los certificados. En el repositorio va solo el ejemplo.

```bash
cp Facturacion.Server/appsettings.Development.json.ejemplo Facturacion.Server/appsettings.Development.json
```

Abre la copia y ajusta:

- **`ConnectionStrings:BaseDeDatos`** — si usas SQL Express con nombre de instancia, el
  servidor va como `Server=.\SQLEXPRESS`. Con la instancia por omisión, `Server=.` sirve tal
  cual.
- **`Jwt:ClaveDeFirma`** — mínimo 32 caracteres. Inventa una propia; **no se comparte entre
  equipos**. El servidor no arranca si falta o es corta, a propósito: no es algo que se deba
  descubrir cuando alguien intenta iniciar sesión.
- **`Sembrado:Correo`** y **`Sembrado:Contrasena`** — con estas dos claves puestas, al
  arrancar en `Development` se crea una cuenta con **dos** empresas y un usuario con los seis
  permisos. La contraseña necesita **12 caracteres o más**, con mayúscula, minúscula y
  dígito; si es más corta, Identity la rechaza y el sembrado no crea al usuario. Si borras
  las dos claves, el sembrado no corre.

---

## 3. Llave maestra

Protege el llavero de Data Protection y, con él, los CSD guardados. La genera un comando y
se copia a la configuración; no se inventa sola al arrancar, porque una clave que la
aplicación se genera y guarda junto a lo que protege no protege de nada.

```bash
dotnet run --project Facturacion.Server -- --generar-llave-maestra
```

Copia lo que imprime a `Almacen:LlaveMaestraPfx`.

> **Respáldala.** Si se pierde, los `.cer` y `.key` ya cargados quedan ilegibles y hay que
> volver a cargarlos.

---

## 4. Crear la base de datos

Un solo comando. Crea la base, las tablas, los procedimientos almacenados, los índices de
texto completo y los datos de catálogo de la plataforma (permisos y paquetes de timbres).

```bash
dotnet ef database update --project Facturacion.Server
```

Si falla la conexión, el problema está en `ConnectionStrings:BaseDeDatos`, no en las
migraciones.

---

## 5. Catálogos del SAT

**Este paso no es opcional si vas a tocar clientes, productos o emisión.** Sin catálogos, la
validación fiscal rechaza todo: las claves de producto, las de unidad, los regímenes, los
usos de CFDI y las tasas de impuesto se comprueban contra estas tablas.

El archivo pesa unos 48 MB y **no está en el repositorio** (`.gitignore` excluye
`/CatalogosSAT/`): un binario de ese tamaño en Git lo carga cada quien en cada clon, para
siempre, y además el SAT lo republica con correcciones.

1. Descarga el archivo de catálogos CFDI 4.0 del portal del SAT. Es un `.xls` con nombre del
   estilo `catCFDI_V_4_AAAAMMDD.xls`.
2. Impórtalo:

```bash
dotnet run --project Facturacion.Server -- --cargar-catalogos ruta/al/catCFDI_V_4_AAAAMMDD.xls
```

Tarda varios minutos: `c_ClaveProdServ` trae unas 52 000 filas, `c_CodigoPostal` unas 95 000
y `c_Colonia` unas 145 000, y encima se pueblan los índices de texto completo. El comando no
levanta el servidor; carga y termina.

Es idempotente: volver a correrlo actualiza en vez de duplicar. Cuando el SAT publique una
corrección puntual, se puede recargar **un solo catálogo** pasándolo como tercer argumento,
sin volver a procesar los 300 000 renglones:

```bash
dotnet run --project Facturacion.Server -- --cargar-catalogos ruta/al/archivo.xls c_TasaOCuota
```

---

## 6. Correr

```bash
dotnet run --project Facturacion.Server
```

El `Server` sirve la API **y** los estáticos del `Client`: es un solo origen y un solo
proceso. No hay que levantar nada aparte para el WebAssembly.

Entra a `https://localhost:7123` con el correo y la contraseña del sembrado. Como el usuario
tiene dos empresas, la primera pantalla después de iniciar sesión es el selector de empresa.

---

## 7. Comprobar que quedó bien

| Qué | Cómo | Qué esperar |
|---|---|---|
| Base y migraciones | `dotnet ef migrations list --project Facturacion.Server` | Ninguna dice `(Pending)` |
| Catálogos | Menú → **Catálogos del SAT** | Cada catálogo con su número de filas y su versión |
| Pruebas | `dotnet test` | 48 de 48 |

Las pruebas crean y borran sus propias bases (`FacturacionPruebas*`) contra el mismo servidor
SQL de la cadena de conexión. Corren contra SQL Server de verdad a propósito: lo que prueban
son bloqueos de renglón e índices únicos, y un proveedor en memoria no los tiene.

---

## 8. Comandos de operación

No levantan el servidor; usan el contenedor de dependencias y terminan.

```bash
dotnet run --project Facturacion.Server -- --compras-pendientes
```

```bash
dotnet run --project Facturacion.Server -- --acreditar-compra <id-de-la-compra>
```

Acreditar es lo que mete los timbres a la bolsa de la empresa que compró. Es una operación
del operador del SaaS, no del inquilino: por eso vive en la consola y no en un endpoint.

---

## 9. Si algo falla

**«Falta 'Jwt:ClaveDeFirma' o tiene menos de 32 caracteres»** — el paso 2.

**No se puede iniciar sesión con el usuario del sembrado** — revisa que la contraseña de
`Sembrado:Contrasena` cumpla la política (12 caracteres, mayúscula, minúscula y dígito). Si
no la cumple, el sembrado no llega a crear al usuario. Tras varios intentos fallidos el
bloqueo por IP entra en juego y crece con cada tanda; espera o reinicia el servidor, que lo
guarda en memoria.

**El ejecutable está bloqueado al compilar** — hay un `dotnet run` vivo. Ciérralo antes de
compilar.

**Una clave del SAT válida se rechaza al guardar un producto o un cliente** — casi siempre es
el paso 5 sin hacer, o hecho con un archivo viejo. Revisa la pantalla de catálogos.
