# Contratos entre las dos mitades

Esta carpeta es la frontera entre la mitad A (plataforma, identidad, catálogos) y la
mitad B (documentos, timbrado, salidas). Se definió en la fase 0 y **queda congelada**.

Cambiar cualquier firma o cualquier DTO de aquí exige:

1. acuerdo explícito de los dos desarrolladores, y
2. un commit dedicado que no traiga ningún otro cambio.

Ver `REPARTO-EQUIPO.md` §5.

## Tres diferencias respecto a REPARTO-EQUIPO.md §5

Se acordaron al cerrar la fase 0, antes de congelar. Están aquí para que queden a la vista
de quien compare este código contra el documento.

**1 · La llave privada del CSD salió de `IServicioEmpresaEmisora`.**
El documento ponía `ObtenerCsdAsync` dentro de esa interfaz. Cualquiera que la inyectara
para leer el RFC del emisor obtenía también la llave privada descifrada. Ahora vive sola
en `IProveedorCsdParaTimbrado`, que solo resuelve el módulo de timbrado.

**2 · `IServicioFolios` ganó `ConfirmarAsync`.**
Sin ella no hay forma de distinguir una reserva en curso de una abandonada, y el barrido
de reservas huérfanas no tiene contra qué comparar. Además quedó documentado que
`LiberarSiNoUsadoAsync` **no devuelve el folio a la serie**: CLAUDE.md §5 prohíbe
reciclarlo, y sin decirlo la mitad B iba a suponer lo contrario.

**3 · `IServicioEmpresaEmisora` ganó `ObtenerLogoAsync`.**
La mitad B genera el PDF y necesita el logo. Sin este método tendría que leer el almacén
de archivos de la mitad A por su cuenta, que es justo lo que la frontera existe para evitar.

## Convenciones

- Todos los DTO son `record` inmutables y viven en el mismo archivo que su interfaz.
- Todo importe es `decimal` con seis decimales de cálculo.
- Toda fecha es UTC.
- Ningún método recibe un `empresaId`: la empresa activa es un claim del token y la
  resuelve `IContextoEmpresa` (CLAUDE.md §4).
