# UI_Funcional.md — Especificación funcional de interfaces (Sistema legacy GenCFDI V4.2)

> Documento de análisis de requerimientos. Se extrae **únicamente la estructura de datos funcional**: componentes, columnas de tablas, inputs con su tipo, y reglas de negocio observables. Se ignora deliberadamente el diseño visual, colores y distribución del sistema antiguo.

---

## Índice

| # | Captura | Componente sugerido |
|---|---------|---------------------|
| 1 | Interfaz principal | `DocumentosDashboard` |
| 2 | Menús | `MainNavigation` |
| 3 | Configuración | `ConfiguracionSistemaForm` |
| 4 | Datos de la empresa | `EmpresaEmisoraForm` |
| 5 | Catálogo Productos (listado) | `ProductosList` |
| 6 | Producto (alta/edición) | `ProductoForm` |
| 7 | Catálogo Clientes (listado) | `ClientesList` |
| 8 | Cliente (alta/edición) | `ClienteForm` |
| 9 | Catálogo Usuarios | `UsuariosList` + `UsuarioForm` |
| 10 | Catálogo Vehículos | `VehiculosList` + `VehiculoForm` |
| 11 | Catálogo Figuras Transporte | `FigurasTransporteList` + `FiguraTransporteForm` |
| 12 | Factura estándar — Detalle | `FacturaEstandarForm` / `FacturaDetalleTab` |
| 13 | Factura estándar — CFDI Relacionados | `CfdiRelacionadosTab` |
| 14 | Factura estándar — Información Global | `InformacionGlobalTab` |
| 15 | Factura Constructoras | `FacturaConstructorasForm` |
| 16 | Comercio Exterior — Factura | `FacturaComercioExteriorForm` |
| 17 | Comercio Exterior — Complemento | `ComercioExteriorComplementoTab` |
| 18 | Comercio Exterior — Emisor | `ComercioExteriorEmisorTab` |
| 19 | Comercio Exterior — Receptor | `ComercioExteriorReceptorTab` |
| 20 | Notaría — Factura | `FacturaNotariaForm` |
| 21 | Notaría — Datos del Notario | `NotarioTab` |
| 22 | Notaría — Datos del Inmueble | `InmuebleTab` |
| 23 | Notaría — Datos de la Operación | `OperacionNotarialTab` |
| 24 | Notaría — Datos del Vendedor | `EnajenantesTab` |
| 25 | Notaría — Datos del Comprador | `AdquirentesTab` |
| 26 | Traslados (Carta Porte) | `TrasladoCartaPorteForm` |
| 27 | Pagos — General | `ComplementoPagoForm` / `PagoGeneralTab` |
| 28 | Pagos — Bancos | `PagoBancosTab` |
| 29 | Pagos — SPEI | `PagoSpeiTab` |
| 30 | Solicitudes de Cancelación | `SolicitudesCancelacionList` |
| 31 | Búsqueda de catálogos (regla global) | `CatalogoSearchModal` |

---

## 1. `DocumentosDashboard` — Interfaz principal

Pantalla de arranque. Tres zonas funcionales: filtros de búsqueda, listado de documentos emitidos, y panel de envío por correo, más un visor de la representación impresa del documento seleccionado.

### 1.1 Formulario de filtros — `DocumentosFiltroPanel`

| Label | Tipo | Notas |
|---|---|---|
| Fecha Inicial | fecha (date) | Valor por defecto: inicio de año |
| Final (fecha) | fecha (date) | Valor por defecto: fecha actual |
| Cliente Inicial | texto (código, 9 dígitos) | Default `000000000`. Lleva botón de búsqueda de catálogo (ver §31) |
| Final (cliente) | texto (código, 9 dígitos) | Default `999999999`. Lleva botón de búsqueda de catálogo |
| Documento | select | Lista de tipos de documento. Incluye opción `TODOS` |

Botones: **Consultar**, **Actual** (refrescar), **Validar RFC**, ícono **Abrir/Exportar**, ícono **Imprimir**.

### 1.2 DataGrid — `DocumentosGrid`

Columnas exactas:

1. `Folio Int.`
2. `Fecha`
3. `ClvCli`
4. `TDocto`
5. `Folio Sat`

(La rejilla tiene scroll horizontal; puede contener columnas adicionales fuera de vista.)

### 1.3 Panel de envío CFD — `EnvioCfdPanel`

| Label | Tipo |
|---|---|
| Para | texto (email, admite múltiples destinatarios) |
| Asunto | texto (default `Documento CFDI`) |
| Mensaje | textarea |
| Incluir archivo XML | checkbox (marcado por defecto) |
| Archivos Adjuntos | lista de solo lectura (.xml y .pdf generados) |

Botón: **Enviar a cuenta única**.

### 1.4 Visor de documento — `DocumentoPreview`

Visor paginado de la representación impresa (equivalente a reporte). Controles: primera / anterior / siguiente / última página, campo **página actual** (número), total de páginas, buscar en documento, zoom. Barra de estado: `Nº de página actual`, `Nº total de páginas`, `Factor de zoom`.

Campos visibles del documento renderizado (solo lectura, útiles como contrato del reporte): Versión, Folio Interno, Folio Fiscal (UUID), No. de Serie del CSD SAT, No. de Serie del CSD Emisor, Cliente, R.F.C., Fecha y hora de Certificación, Fecha y hora de Emisión, Uso de CFDI, Régimen Fiscal, Lugar de Expedición; tabla de conceptos con `CANTIDAD`, `CLAVE UNIDAD`, `CLAVE DEL PRODUCTO`, `DESCRIPCIÓN`, `OBJ. IMP.`, `P. UNITARIO`, `IMPORTE`, y sub-tabla de impuestos con `BASE`, `IMPUESTO`, `TIPO FACTOR`, `TASA O CUOTA`, `IMPORTE`; totales: Sub-Total, Descuento, IVA 16 %, IVA Retenido, ISR Retenido, Total; Método de Pago, Forma de Pago, Moneda, T.C.

### Reglas de negocio

- El listado se puebla solo tras pulsar **Consultar**; el rango de fechas y de clientes es obligatorio (siempre tiene valores por defecto).
- El envío por correo y la vista impresa operan sobre el **registro seleccionado** en la rejilla.
- Los adjuntos se generan automáticamente (XML + PDF); el XML es opcional vía checkbox.
- Acceso a los menús de catálogos y configuración desde esta pantalla.

---

## 2. `MainNavigation` — Menús

Árbol de navegación (no es un formulario). Estructura exacta:

- **Utilerías** ▸ (submenú)
- **Catálogos** ▸
  - Clientes
  - Productos
  - Tipos de IVA
  - Usuarios
  - Vehículos
  - Figuras Transporte
- **Cotizaciones**
- **Factura Notaría**
- **Fac. Constructoras**
- **Fac. Comercion Exterior** *(sic — "Comercio Exterior")*
- **Facturación**
- **Pagos**
- **Traslados**
- **Addendas** ▸ (submenú)
- **Solicitudes de Cancelación**

### Reglas de negocio

- La visibilidad/habilitación de cada nodo depende de los **permisos del usuario** autenticado (ver §9): edición de catálogos, generación de documentos y configuración.

---

## 3. `ConfiguracionSistemaForm` — Configuración

| Sección | Label | Tipo |
|---|---|---|
| General | Habilitar enviar notificaciones | checkbox |
| Web Services | Usuario | texto |
| Web Services | Contraseña | texto (password) |
| Logo de la Empresa | (imagen) | uploader — botones **Quitar Logo** / **Agregar Logo** + campo texto de ruta |
| General | IVA Tasa % | número (decimal) |
| Correo electrónico | Servidor SMTP | texto |
| Correo electrónico | Puerto | número |
| Correo electrónico | Usuario | texto |
| Correo electrónico | Contraseña | texto (password) |
| Correo electrónico | Requiere autentificación SSL | checkbox |
| Correo electrónico | De | texto (remitente con formato `Nombre<correo>`) |
| Correo electrónico | Para | texto (email) |
| Notificaciones | Avisar caducidad de Certificado — Días antes | número (entero) |
| Impuesto Retenido | IVA % | número (decimal) |
| Impuesto Retenido | ISR % | número (decimal) |
| Impuesto Retenido | ISH % | número (decimal) |
| Impuesto Retenido | IEPS % | número (decimal) |

Botones: **Prueba de envío**, **Guardar**, **Cancelar**.

### Reglas de negocio

- Los parámetros de correo solo aplican si *Habilitar enviar notificaciones* está activo.
- **Prueba de envío** valida la configuración SMTP antes de guardar.
- Las tasas configuradas aquí alimentan por defecto los cálculos de impuestos en los formularios de facturación.
- Solo accesible con permiso de *configuración*.

---

## 4. `EmpresaEmisoraForm` — Información de la Empresa

| Label | Tipo | Notas |
|---|---|---|
| RFC | texto | Identificador fiscal del emisor |
| Nombre o Razón Social | texto | |
| Calle | texto | |
| No. Exterior | texto | |
| No. Interior | texto | |
| Referencia | texto | |
| Colonia | texto | |
| Localidad | texto | |
| Municipio | texto | |
| Estado | texto | |
| País | texto | |
| C.P. | texto (código postal) | |
| Teléfono | texto | |
| Correo Eléctronico | texto (email) | |
| Llave Privada | texto + selector de archivo (botón `**`) | archivo `.key` |
| Contraseña de la Llave Privada | texto (password) | |
| Certificado | texto + selector de archivo (botón `**`) | archivo `.cer` |
| Expedido En | texto | Lugar de expedición del CFDI |
| Régimen Fiscal | select | Catálogo SAT c_RegimenFiscal |
| Licencia CFDI | texto | |
| Lic. Notarios | texto | Habilita módulo Factura Notaría |
| Lic. Obras | texto | Habilita módulo Fac. Constructoras |
| Lic. Comercio | texto | Habilita módulo Comercio Exterior |
| Lic. INE | texto | Habilita complemento INE |

Sub-sección **Carta Porte** (domicilio de origen por defecto):

| Label | Tipo |
|---|---|
| Calle | texto |
| No. Exterior | texto |
| No. Interior | texto |
| Estado | select |
| Municipio | select (dependiente de Estado) |
| Código Postal | texto |

Botones: **Guardar**, **Cancelar**.

### Reglas de negocio

- Pie de pantalla informativo: `Certificado: Vigencia del <fecha inicio> al <fecha fin>` — se calcula al cargar el .cer.
- **Municipio** depende del **Estado** seleccionado (carga en cascada).
- Cada licencia habilita/deshabilita el módulo correspondiente en el menú.
- El domicilio Carta Porte se usa como origen precargado en §26.

---

## 5. `ProductosList` — Catálogo Productos y/o Servicios

### DataGrid

1. `Código`
2. `Descripción`
3. `Unidad`
4. `Precio`
5. `Peso KG`

Barra de herramientas: **Nuevo**, **Editar**, **Eliminar**, **Exportar**, **Buscar** (ícono) + input de texto **Buscar** (filtro en vivo).

---

## 6. `ProductoForm` — Alta / edición de Producto

| Label | Tipo | Notas |
|---|---|---|
| Código | texto | Deshabilitado (autogenerado / no editable en edición) |
| Código SAT | texto + botón búsqueda (`**`) | Catálogo SAT c_ClaveProdServ. Muestra la descripción resuelta al costado (p. ej. "Industria lechera") |
| Descripción | textarea | |
| Unidad de Medida | texto | Descripción libre |
| Peso en KG | número (decimal) | |
| Unidad SAT | texto + botón búsqueda (`**`) | Catálogo SAT c_ClaveUnidad. Muestra descripción resuelta ("Litro") |
| Precio de Venta | número (decimal) | |
| Obj. de Impuesto | select | Valores tipo `SI` / `NO` (c_ObjetoImp) |
| Tipo de IVA | radio group (3 opciones excluyentes) | `IVA Tasa 16%`, `IVA Tasa 0%`, `Excento de Iva` |

Botones: **Guardar**, **Cancelar**.

### Reglas de negocio

- `Código SAT` y `Unidad SAT` **deben** seleccionarse desde su interfaz de búsqueda (regla §31); al elegir clave se autocompleta la descripción.
- El grupo de IVA es de selección única y obligatoria.

---

## 7. `ClientesList` — Catálogo Clientes

### DataGrid

1. `Clave`
2. `Nombre`
3. `RFC`
4. `Calle`
5. `No. Ext.`
6. `No. Int.`
7. `Colonia`
8. `Referencia`
9. *(columnas adicionales accesibles por scroll horizontal: Municipio, Estado, País, C.P., Teléfono, Email)*

Barra de herramientas: **Nuevo**, **Editar**, **Eliminar**, **Exportar**, **Buscar** (ícono) + input **Buscar**.

---

## 8. `ClienteForm` — Alta / edición de Cliente

| Label | Tipo | Obligatorio |
|---|---|---|
| Nombre o Razón Social | texto | ✱ Sí |
| RFC | texto | ✱ Sí |
| Calle | texto | No |
| No. Exterior | texto | No |
| No. Interior | texto | No |
| Colonia | texto | No |
| Localidad | texto | No |
| Referencia | texto | No |
| Municipio | texto | No |
| Estado | texto | No |
| País | texto | No |
| C.P. | texto | No |
| Teléfono | texto | No |
| Email | texto (email) | No |
| Método de Pago | select | ✱ Sí (c_MetodoPago: PUE / PPD) |
| Forma de Pago | select | ✱ Sí (c_FormaPago) |
| Uso CFDI | select | ✱ Sí (c_UsoCFDI) |
| Régimen Fiscal | select | ✱ Sí (c_RegimenFiscal) |

Sub-sección **Carta Porte** (domicilio de entrega del cliente):

| Label | Tipo |
|---|---|
| Calle | texto |
| No. Exterior | texto |
| No. Interior | texto |
| Estado | select |
| Municipio | select (dependiente de Estado) |
| Código Postal | texto |

Botones: **Guardar**, **Cancelar**. Leyenda: `✱ Campos obligatorios`.

### Reglas de negocio

- Los campos marcados con asterisco rojo son obligatorios y bloquean el guardado.
- **Municipio** de Carta Porte depende de **Estado**; ambos deshabilitados hasta capturar la sección.
- Los valores de Método de Pago / Forma de Pago / Uso CFDI / Régimen Fiscal se precargan automáticamente al seleccionar el cliente en cualquier formulario de facturación.

---

## 9. `UsuariosList` + `UsuarioForm` — Catálogo de Usuarios y permisos

### DataGrid `UsuariosGrid`

1. `Id`
2. `Usuario`
3. `Nombre`

Barra de herramientas: **Nuevo**, **Editar**, **Eliminar**, **Exportar**, **Buscar** (ícono) + input **Buscar**, botón **Limpiar filtro**.

### Formulario `UsuarioForm` (modal "Usuarios de Visor CFD")

| Label | Tipo | Obligatorio |
|---|---|---|
| Usuario | texto | Sí (único) |
| Nombre | texto | Sí |
| Contraseña | texto (password) | Sí |
| Confirmar Contraseña | texto (password) | Sí |

Botones: **Guardar**, **Cancelar**.

### Reglas de negocio (requerimiento explícito del cliente)

- Al crear un usuario auxiliar se le **asignan permisos** granulares sobre:
  - Edición de catálogos
  - Generación de documentos
  - Configuración
- Los permisos deben poder **actualizarse** o **borrarse** posteriormente.
- Un usuario puede **desactivarse** en lugar de eliminarse (baja lógica → se requiere campo `Activo` (checkbox) y `Permisos` (multi-selección de checkboxes) en el rediseño, aunque no estén presentes en la pantalla legacy).
- `Contraseña` y `Confirmar Contraseña` deben coincidir.

---

## 10. `VehiculosList` + `VehiculoForm` — Catálogo de Vehículos

### DataGrid `VehiculosGrid`

1. `Clave`
2. `Descripción`
3. `Placa`
4. `Modelo`
5. `Configuración`
6. `Tipo Permiso`
7. `No. Permiso`
8. `Aseguradora`
9. `No. Póliza`

Barra de herramientas: **Nuevo**, **Editar**, **Eliminar**, **Exportar**, **Buscar** (ícono) + input **Buscar**.

### Formulario `VehiculoForm`

| Label | Tipo | Notas |
|---|---|---|
| Descripción | texto | |
| Configuración | select | Catálogo SAT c_ConfigAutotransporte (p. ej. "Vehículo ligero de carga (2 llantas en el eje delantero y 2 llantas en el trasero)") |
| Placa | texto | |
| Año Modelo | número (entero, 4 dígitos) | |
| Aseguradora | texto | |
| No. de Póliza | texto | |
| Tipo Permiso | select | Catálogo SAT c_TipoPermiso |
| No. de Permiso | texto | |
| Peso Vehicular | número (decimal) | Unidad fija: Toneladas |

Botones: **Guardar**, **Cancelar**.

### Reglas de negocio

- Todos los campos son requeridos por Carta Porte; el vehículo seleccionado en §26 precarga Permiso SCT, Placa, Modelo, Aseguradora, Póliza y Peso Bruto Vehicular.

---

## 11. `FigurasTransporteList` + `FiguraTransporteForm` — Catálogo Figuras Transporte

### DataGrid `FigurasTransporteGrid`

1. `Clave`
2. `Tipo Figura`
3. `RFC`
4. `Nombre`
5. `Licencia`
6. `Calle`
7. `No. Exterior`
8. `No. Interior`
9. `Estado`
10. *(por scroll: Municipio, Código Postal)*

Barra de herramientas: **Nuevo**, **Editar**, **Eliminar**, **Exportar**, **Buscar** (ícono) + input **Buscar**.

### Formulario `FiguraTransporteForm`

| Label | Tipo | Notas |
|---|---|---|
| Tipo Figura | select | Catálogo SAT c_FiguraTransporte (p. ej. `01 Operador`) |
| RFC | texto | |
| Nombre | texto | |
| No. de Licencia | texto | |
| **Domicilio** — Calle | texto | |
| **Domicilio** — No. Exterior | texto | |
| **Domicilio** — No. Interior | texto | |
| **Domicilio** — Estado | select | |
| **Domicilio** — Municipio | select (dependiente de Estado) | |
| **Domicilio** — Código Postal | texto | |

Botones: **Guardar**, **Cancelar**.

### Reglas de negocio

- `No. de Licencia` es obligatorio cuando `Tipo Figura` = `01 Operador`.
- **Municipio** depende de **Estado**.

---

## 12. `FacturaEstandarForm` — Facturación estándar (pestaña Detalle)

Formulario maestro-detalle con tres pestañas: **Detalle**, **CFDI Relacionados**, **Información Global**.

### 12.1 Cabecera — sección *General*

| Label | Tipo | Notas |
|---|---|---|
| Tipo de Documento | select | p. ej. `FACTURA` |
| Folio | texto | Solo lectura, autogenerado (`FAC-102`) |
| Moneda | select | `MXN`, `USD`, … |
| Tipo de Cambio | número (decimal) | Deshabilitado si Moneda = MXN; obligatorio en otro caso |
| Tasa IVA | número (decimal) | Precargada desde Configuración |

### 12.2 Cabecera — sección *Datos del Cliente*

| Label | Tipo | Notas |
|---|---|---|
| Clave | texto (numérico) + botón **lupa** de búsqueda | Regla §31 |
| R.F.C. | texto | Solo lectura, resuelto desde el cliente |
| Nombre | texto | Solo lectura |
| Dirección | textarea | Solo lectura |
| Método de Pago | select | Precargado del cliente (c_MetodoPago) |
| Forma de Pago | select | Precargado del cliente (c_FormaPago) |
| Uso CFDI | select | Precargado del cliente (c_UsoCFDI) |

Botón: **Cotización** (importa conceptos desde una cotización existente).

### 12.3 DataGrid editable `ConceptosGrid` (pestaña Detalle)

Columnas exactas:

1. `Código`
2. `Cantidad`
3. `Unidad`
4. `Descripción`
5. `Precio Unit.`
6. `Importe`
7. `Descuento`
8. *(columnas adicionales por scroll horizontal)*

### 12.4 Pie del formulario

| Label | Tipo |
|---|---|
| Observaciones | textarea |
| Condiciones de Pago | texto |
| Descuento | número (solo lectura, calculado) |
| Subtotal | número (solo lectura, calculado) |
| ISH 0% | número (solo lectura, calculado) |
| IEPS % | número (solo lectura, calculado) |
| IVA % | número (solo lectura, calculado, 4 decimales) |
| IVA Retenido | número (tasa %) + número (importe calculado) |
| ISR Retenido | número (tasa %) + número (importe calculado) |
| Total | número (solo lectura, calculado) |

Botones: **Generar Factura**, **Cancelar**.

### Reglas de negocio

- `Clave` de cliente es obligatoria y solo puede fijarse mediante la interfaz de búsqueda (§31); al resolverla se autocompletan RFC, Nombre, Dirección, Método de Pago, Forma de Pago y Uso CFDI.
- `Tipo de Cambio` se habilita solo cuando la moneda ≠ MXN.
- El renglón de concepto se resuelve desde el catálogo de Productos (§5) mediante búsqueda por `Código`; se autocompletan Unidad, Descripción y Precio Unit.
- `Importe = Cantidad × Precio Unit. − Descuento`; los totales del pie son de solo lectura y se recalculan en cada cambio del grid.
- Las tasas de IVA Retenido / ISR Retenido son editables por documento (default desde Configuración).
- Debe existir al menos un concepto para habilitar **Generar Factura**.

---

## 13. `CfdiRelacionadosTab` — Pestaña CFDI Relacionados

Misma cabecera y pie que §12.

### Inputs

| Label | Tipo | Notas |
|---|---|---|
| Tipo Relación | select | Catálogo SAT c_TipoRelacion. Deshabilitado hasta que exista al menos una relación / o habilita la captura |
| Serie | texto | |
| Folio | texto | |

Botón: **Agregar**.

### DataGrid `CfdiRelacionadosGrid`

1. *(columna selector de fila)*
2. `Serie`
3. `Folio`
4. `UUID`

### Reglas de negocio

- `Tipo Relación` es obligatorio si se agrega al menos un CFDI relacionado.
- **Agregar** busca el CFDI por Serie+Folio y resuelve el `UUID` automáticamente.
- La pestaña es opcional; una factura puede generarse sin relaciones.

---

## 14. `InformacionGlobalTab` — Pestaña Información Global

Misma cabecera y pie que §12.

| Label | Tipo | Notas |
|---|---|---|
| Periodicidad | select | Catálogo SAT c_Periodicidad. Deshabilitado por defecto |
| Mes | select | Catálogo SAT c_Meses. Deshabilitado por defecto |
| Año | número (entero, 4 dígitos) | Default: año en curso |

### Reglas de negocio

- La sección se **habilita únicamente cuando el receptor es "PÚBLICO EN GENERAL"** (RFC genérico `XAXX010101000`); en ese caso los tres campos son obligatorios.
- En cualquier otro caso los selects permanecen deshabilitados y no se emite el nodo `InformacionGlobal`.

---

## 15. `FacturaConstructorasForm` — Facturación Constructoras

Cabecera idéntica a §12 (General + Datos del Cliente + Método/Forma de Pago + Uso CFDI), con **Condiciones de Pago** promovido a la cabecera. Pestañas: **Detalle**, **CFDI Relacionados**.

### DataGrid `ConceptosGrid`

1. `Código`
2. `Cantidad`
3. `Unidad`
4. `Descripción`
5. `Precio Unit.`
6. `Importe`
7. *(por scroll: Tasa / impuestos)*

### Panel de cálculo de obra (lado derecho)

| Label | Tipo | Notas |
|---|---|---|
| Importe de los Trabajos | número (decimal) | Calculado del detalle |
| Amortización del Anticipo | número (% editable) + número (importe calculado) | |
| Retenciones | número (% editable) + número (importe calculado) | |
| Devoluciones | número (% editable) + número (importe calculado) | |
| Subtotal | número (solo lectura, calculado) | |
| IVA 16% | número (solo lectura, calculado) | |
| Total | número (solo lectura, calculado) | |
| 5 al millar | número (% editable) + número (importe calculado) | Deducción |
| 1% OBS | número (% editable) + número (importe calculado) | Deducción |
| ICIC | número (% editable) + número (importe calculado) | Deducción |
| UNETE | número (% editable) + número (importe calculado) | Deducción |
| Importe Líquido | número (solo lectura, calculado) | |

Pie: **Observaciones** (textarea). Botones: **Generar Factura**, **Cancelar**.

### Reglas de negocio

- `Subtotal = Importe de los Trabajos − Amortización del Anticipo − Retenciones − Devoluciones`.
- `Total = Subtotal + IVA`.
- `Importe Líquido = Total − (5 al millar + 1% OBS + ICIC + UNETE)`.
- Las etiquetas de las cuatro deducciones (`5 al millar`, `1% OBS`, `ICIC`, `UNETE`) son editables (campos de texto).
- Módulo disponible solo si existe **Lic. Obras** (§4).
- Nota del analista: en la captura hay un campo tachado en el extremo superior derecho — se interpreta como **campo a eliminar** en el rediseño.

---

## 16. `FacturaComercioExteriorForm` — Comercio Exterior (pestaña Factura)

Pestañas del formulario: **Factura**, **Complemento**, **Emisor**, **Receptor**.

### Cabecera

| Label | Tipo | Notas |
|---|---|---|
| Tipo de Documento | select | |
| Folio | texto | Solo lectura, autogenerado |
| Moneda | select | Default `USD` en este módulo |
| T. C. | número (decimal, 4 decimales) | Obligatorio cuando moneda ≠ MXN |
| Tasa IVA | número (decimal) | |
| Método de Pago | select | |
| Forma de Pago | select | |

### Datos del Cliente

| Label | Tipo | Notas |
|---|---|---|
| Clave | texto + botón **lupa** | Regla §31 |
| R.F.C. | texto (solo lectura) | |
| Nombre | texto (solo lectura) | |
| Dirección | textarea (solo lectura) | |
| Residencia Fiscal | select | Catálogo SAT c_Pais (p. ej. `USA Estados Unidos (los)`) |
| No. de Registro de Identidad fiscal | texto | |
| Exportación | select | Catálogo SAT c_Exportacion (`01 No aplica`, `02 Definitiva`, `03 Temporal`) |

### DataGrid `ConceptosGrid`

1. `Código`
2. `Cantidad`
3. `Unidad`
4. `Descripción`
5. `Precio Unit.`
6. `Importe`
7. `Fracción Arancelaria`
8. *(por scroll: Unidad Aduana, Valor Unitario Aduana, Cantidad Aduana, Marca / Modelo / Submodelo / No. Serie)*

### Pie

| Label | Tipo |
|---|---|
| Uso CFDI | select |
| Observaciones | textarea |
| Condiciones de Pago | texto |
| Subtotal | número (solo lectura) |
| ISH % | número (solo lectura) |
| IVA % | número (solo lectura) |
| IVA Retenido | número (%) + número (importe) |
| ISR Retenido | número (%) + número (importe) |
| Total | número (solo lectura) |

Botones: **Generar Factura**, **Cancelar**.

### Reglas de negocio

- `Residencia Fiscal` y `No. de Registro de Identidad fiscal` son obligatorios cuando el receptor es extranjero.
- `Fracción Arancelaria` es obligatoria por concepto cuando `Exportación` = `02 Definitiva`.
- Módulo disponible solo si existe **Lic. Comercio** (§4).

---

## 17. `ComercioExteriorComplementoTab` — Pestaña Complemento

| Label | Tipo | Notas |
|---|---|---|
| Tipo de Operacion | select | c_TipoOperacion (`2 Exportación`) |
| Clave Pedimento | select | c_ClavePedimento (`A1 IMPORTACION O EXPORTACION DEFINITIVA`) |
| Certificado Origen | select | `0 No funge como certificado de origen` / `1 Funge como certificado de origen` |
| Número de Certificado Origen | texto | |
| Número Exportador Confiable(UE) | texto | |
| Clave INCOTERM | select | c_INCOTERM. Deshabilitado por defecto |
| ¿Tiene Subdivisión? | select | `0 No tiene subdivisión.` / `1 Sí` |
| Observaciones | texto | |

### Reglas de negocio

- `Número de Certificado Origen` se habilita/es obligatorio solo si `Certificado Origen` = `1`.
- `Clave INCOTERM` se habilita solo cuando `Tipo de Operacion` = `2 Exportación` con clave de pedimento `A1`.
- `Número Exportador Confiable(UE)` aplica solo a operaciones con la Unión Europea.

---

## 18. `ComercioExteriorEmisorTab` — Pestaña Emisor

| Label | Tipo | Obligatorio |
|---|---|---|
| CURP | texto | No (solo persona física) |
| Calle | texto | ✱ Sí |
| Número Exterior | texto | No |
| Número Interior | texto | No |
| Colonia | select | No |
| Localidad | select | No |
| Referencia | texto | No |
| Municipio | select | No |
| Estado | select | ✱ Sí |
| País | select | ✱ Sí (default `MEX México`) |
| Código Postal | texto | ✱ Sí |

### Reglas de negocio

- Los labels en **negrita** indican campo obligatorio (Calle, Estado, País, Código Postal).
- `Estado` y `País` se muestran deshabilitados: se heredan de los datos de la empresa (§4).
- Cascada: País → Estado → Municipio → Colonia/Localidad.

---

## 19. `ComercioExteriorReceptorTab` — Pestaña Receptor

| Label | Tipo | Obligatorio |
|---|---|---|
| Calle | texto | ✱ Sí |
| Número Exterior | texto | No |
| Número Interior | texto | No |
| Colonia | texto | No |
| Localidad | texto | No |
| Referencia | texto | No |
| Municipio | texto | No |
| Estado | select | ✱ Sí |
| País | select | ✱ Sí |
| Código Postal | texto | ✱ Sí |

### Reglas de negocio

- `País` se hereda de la *Residencia Fiscal* capturada en §16 y queda deshabilitado.
- `Estado` se deshabilita cuando el país no tiene catálogo de estados en el SAT.
- Los campos de domicilio son de texto libre (no select) por tratarse de domicilio extranjero.

---

## 20. `FacturaNotariaForm` — Factura Notaría (pestaña Factura)

Pestañas: **Factura**, **Datos del Notario**, **Datos del Inmueble**, **Datos de la Operación**, **Datos del Vendedor**, **Datos del Comprador**.

### Cabecera

Idéntica a §12: Tipo de Documento (select), Folio (texto solo lectura), Moneda (select), Tipo de Cambio (número, deshabilitado en MXN), Tasa IVA (número), Datos del Cliente (Clave + lupa, R.F.C., Nombre, Dirección — solo lectura), Método de Pago (select), Forma de Pago (select), Uso CFDI (select).

### DataGrid `ConceptosGrid` (sub-pestañas Detalle / CFDI Relacionados)

1. `Código`
2. `Cantidad`
3. `Unidad`
4. `Descripción`
5. `Precio Unit.`
6. `Importe`
7. `ISH` — **checkbox por renglón**
8. *(por scroll: columnas de impuestos)*

### Pie

Observaciones (textarea), Condiciones de Pago (texto), Subtotal, ISH %, IVA %, IVA Retenido (% + importe), ISR Retenido (% + importe), Total — todos numéricos calculados salvo las tasas de retención.

Botones: **Generar Factura**, **Cancelar**.

### Reglas de negocio

- El checkbox `ISH` marca por concepto si aplica el Impuesto Sobre Hospedaje/adicional; alimenta el total `ISH %`.
- Módulo disponible solo si existe **Lic. Notarios** (§4).
- Las cinco pestañas de datos notariales deben completarse antes de habilitar **Generar Factura**.

---

## 21. `NotarioTab` — Datos del Notario

| Label | Tipo | Obligatorio |
|---|---|---|
| CURP | texto (18 caracteres) | ✱ Sí |
| Número Notaria | número (entero) | ✱ Sí |
| Estado | select | ✱ Sí (c_Estado, p. ej. `13 HIDALGO`) |
| Adscrito a la plaza | texto | No |

Botón: **Guardar** (guarda los datos del notario como configuración persistente reutilizable).

---

## 22. `InmuebleTab` — Datos del Inmueble

| Label | Tipo | Obligatorio |
|---|---|---|
| Tipo de Inmueble | select | ✱ Sí (c_TipoInmueble) |
| Calle | texto | ✱ Sí |
| Número Exterior | texto | No |
| Número Interior | texto | No |
| Colonia | texto | No |
| Localidad | texto | No |
| Referencia | texto | No |
| Municipio | texto | ✱ Sí |
| Estado | select | ✱ Sí |
| País | select | ✱ Sí (default `MEX MEXICO (ESTADOS UNIDOS MEXICANOS)`) |
| Código Postal | texto | No |

### Reglas de negocio

- Labels en negrita = obligatorio (Tipo de Inmueble, Calle, Municipio, Estado, País).
- `Tipo de Inmueble` inicia deshabilitado/vacío y debe seleccionarse del catálogo.

---

## 23. `OperacionNotarialTab` — Datos de la Operación

| Label | Tipo | Obligatorio |
|---|---|---|
| Número de Instrumento Notarial | texto | ✱ Sí |
| Fecha | fecha (date picker) | ✱ Sí (default: fecha actual) |
| Monto de la Operación | número (decimal) | ✱ Sí |
| SubTotal | número (decimal) | ✱ Sí |
| IVA | número (decimal) | ✱ Sí |

### Reglas de negocio

- `Monto de la Operación = SubTotal + IVA` (validación de consistencia).
- Todos los campos son obligatorios (labels en negrita).

---

## 24. `EnajenantesTab` — Datos del Vendedor

| Label | Tipo | Notas |
|---|---|---|
| ¿Copropiedad o Sociedad Conyugal? | select | Controla qué sección se habilita. Deshabilitado/vacío por defecto |

**Sección A — "En caso de ser un solo propietario"**

| Label | Tipo | Obligatorio |
|---|---|---|
| Nombre | texto | ✱ Sí |
| Apellido Paterno | texto | ✱ Sí |
| Apellido Materno | texto | No |
| RFC | texto | ✱ Sí |
| CURP | texto | ✱ Sí |

**Sección B — "Capturar los datos de los enajenantes en caso de copropiedad o sociedad conyugal"**

| Label | Tipo | Obligatorio |
|---|---|---|
| Nombre | texto | ✱ Sí |
| Apellido Paterno | texto | No |
| Apellido Materno | texto | No |
| RFC | texto | ✱ Sí |
| CURP | texto | ✱ Sí |
| Porcentaje | número (decimal) | ✱ Sí |

Botón: **Agregar** (añade el renglón a la rejilla).

### DataGrid `EnajenantesGrid`

1. *(selector de fila)*
2. `Nombre`
3. `Apellido Paterno`
4. `Apellido Materno`
5. `RFC`
6. `CURP`
7. `Porcentaje`

### Reglas de negocio

- Si `¿Copropiedad o Sociedad Conyugal?` = **No**, se habilita la Sección A y se deshabilita la B (y viceversa).
- La suma de los `Porcentaje` de la rejilla debe ser **100 %**.

---

## 25. `AdquirentesTab` — Datos del Comprador

Estructura **idéntica a §24**, con los textos "propietario/enajenantes" sustituidos por "comprador/adquirientes".

| Label | Tipo |
|---|---|
| ¿Copropiedad o Sociedad Conyugal? | select |

**Sección A — "En caso de ser un solo comprador"**: `Nombre` (texto, obligatorio), `Apellido Paterno` (texto, obligatorio), `Apellido Materno` (texto), `RFC` (texto, obligatorio), `CURP` (texto, obligatorio).

**Sección B — "Capturar los datos de los adquirientes en caso de copropiedad o sociedad conyugal"**: `Nombre` (texto, obligatorio), `Apellido Paterno` (texto), `Apellido Materno` (texto), `RFC` (texto, obligatorio), `CURP` (texto, obligatorio), `Porcentaje` (número, obligatorio). Botón **Agregar**.

### DataGrid `AdquirentesGrid`

1. *(selector de fila)*
2. `Nombre`
3. `Apellido Paterno`
4. `Apellido Materno`
5. `RFC`
6. `CURP`
7. `Porcentaje`

### Reglas de negocio

- Misma lógica de exclusión entre Sección A y B según el select.
- La suma de porcentajes debe ser **100 %**.

---

## 26. `TrasladoCartaPorteForm` — Traslados (Carta Porte)

### Cabecera

| Label | Tipo | Notas |
|---|---|---|
| Tipo de Documento | select | `TRASLADO` |
| Folio | texto | Solo lectura, autogenerado (`TRA-6`) |

### Sección *Origen*

| Label | Tipo |
|---|---|
| Calle | texto |
| No. Exterior | texto |
| No. Interior | texto |
| Estado | select |
| Municipio | select (dependiente de Estado) |
| Código Postal | texto |

### Sección *Destino*

| Label | Tipo | Notas |
|---|---|---|
| Clave | texto + botón **lupa** | Cliente destino, regla §31 |
| R.F.C. | texto (solo lectura) | |
| Nombre | texto (solo lectura) | |
| Calle | texto | |
| No. Exterior | texto | |
| No. Interior | texto | |
| Estado | select | |
| Municipio | select (dependiente de Estado) | |
| Código Postal | texto | |

### Sección *Autotransporte*

| Label | Tipo | Notas |
|---|---|---|
| Vehículo | texto (clave) + botón **lupa** | Regla §31; muestra la descripción resuelta |
| Permiso SCT | etiqueta (solo lectura) | Derivado del vehículo |
| Placa | etiqueta (solo lectura) | Derivado |
| Modelo | etiqueta (solo lectura) | Derivado |
| No. Permiso SCT | etiqueta (solo lectura) | Derivado |
| Aseguradora | etiqueta (solo lectura) | Derivado |
| Póliza | etiqueta (solo lectura) | Derivado |
| Peso Bruto Vehicular | etiqueta (solo lectura) | Derivado |
| Fecha Salida | fecha + hora (datetime picker) | |
| Fecha Llegada | fecha + hora (datetime picker) | |
| Distancia Recorrida | número (decimal) | Unidad fija: Km. |
| Observaciones | textarea | |

### Pestañas inferiores

**Pestaña `Productos` — DataGrid `ProductosTrasladoGrid`:**

1. `Código`
2. `Descripción`
3. `Unidad`
4. `Cantidad`
5. `Peso Unitario`
6. `Peso Total`
7. `unidad SAT`
8. `Código SAT`
9. *(por scroll: columnas adicionales)*

**Pestaña `Figura Transporte`** — selección/alta de figuras desde el catálogo §11.

**Pestaña `CFDI Relacionados`** — misma estructura que §13.

### Totales

| Label | Tipo |
|---|---|
| Peso Total | número (solo lectura, suma de `Peso Total`) |
| Total Productos | número (solo lectura, suma de `Cantidad`) |

Botones: **Generar Factura**, **Cancelar**.

### Reglas de negocio

- El domicilio de **Origen** se precarga desde la sección Carta Porte de la empresa (§4); el de **Destino** desde la sección Carta Porte del cliente (§8).
- `Peso Total (renglón) = Cantidad × Peso Unitario`.
- `Fecha Llegada` debe ser ≥ `Fecha Salida`.
- Se requiere al menos una **Figura Transporte** (operador) y un vehículo para generar el documento.
- `Distancia Recorrida` es obligatoria (> 0).

---

## 27. `ComplementoPagoForm` — Pagos (pestaña General)

Formulario con pestañas superiores **General**, **Bancos**, **SPEI**, y pestañas inferiores **Pago**, **CFDI Relacionados**, *(TabPage6 — pestaña sin nombre, a definir/eliminar)*.

### Cabecera *General*

| Label | Tipo | Notas |
|---|---|---|
| Folio | texto | Solo lectura, autogenerado (`PAG-14`) |
| No. de Operación | texto | |
| Fecha | fecha (date picker) | Default: fecha actual |
| Forma de Pago | select | c_FormaPago (p. ej. `03 Transferencia electrónica de fondos`) |
| Moneda | select | |
| T.C. | número (decimal) | Deshabilitado si Moneda = MXN |
| Importe | número (decimal) | Importe total del pago |

### Datos del Cliente

| Label | Tipo |
|---|---|
| Clave | texto + botón **lupa** (regla §31) |
| R.F.C. | texto (solo lectura) |
| Nombre | texto (solo lectura) |

### Sub-pestaña *Pago* — captura de documentos a pagar

| Label | Tipo | Notas |
|---|---|---|
| Serie | texto | |
| Folio | texto | |
| Saldo Anterior | número (solo lectura) | Resuelto por **Consulta** |
| Importe a Pagar | número (decimal) | Editable |
| Saldo Actual | número (solo lectura, calculado) | |
| No. de Parcialidad | número (entero, solo lectura) | Calculado |

Botones: **Consulta** (busca el CFDI por Serie+Folio), **Agregar** (añade el renglón).

### DataGrid `DocumentosPagadosGrid`

1. `UUID`
2. `Serie`
3. `Folio`
4. `Moneda`
5. `TC`
6. `Método de Pago`
7. `No. Parcia` (No. de Parcialidad)
8. `Saldo Anterior`
9. `Importe Pagado`
10. `Saldo Insoluto`
11. `Obj. Impuesto`
12. `Imp. Exc` (Impuesto Exento) *(y columnas adicionales por scroll)*

Botón principal: **Generar Pago**.

### Reglas de negocio

- Solo pueden pagarse CFDI con Método de Pago = **PPD**; la **Consulta** trae `Saldo Anterior` y calcula `No. de Parcialidad` automáticamente.
- `Saldo Actual = Saldo Anterior − Importe a Pagar`; no puede resultar negativo.
- La suma de `Importe Pagado` de la rejilla debe cuadrar con el `Importe` de la cabecera.
- `T.C.` obligatorio cuando la moneda del pago difiere de la del documento.

---

## 28. `PagoBancosTab` — Pagos (pestaña Bancos)

Comparte cabecera inferior (sub-pestañas Pago / CFDI Relacionados / TabPage6) y botón **Generar Pago** con §27.

| Sección | Label | Tipo |
|---|---|---|
| Banco Emisor | R.F.C. | texto |
| Banco Emisor | Cuenta Ordenante | texto |
| Banco Receptor | R.F.C. | texto |
| Banco Receptor | Cuenta Beneficiario | texto |

### Reglas de negocio

- La sección se habilita y es obligatoria solo cuando la `Forma de Pago` es transferencia/cheque/tarjeta (02, 03, 04, 05, 28, 29).
- `Cuenta Ordenante` y `Cuenta Beneficiario` deben cumplir la longitud según la forma de pago (CLABE 18 / tarjeta 16).

---

## 29. `PagoSpeiTab` — Pagos (pestaña SPEI)

Comparte cabecera inferior y botón **Generar Pago** con §27.

| Label | Tipo | Notas |
|---|---|---|
| TipoCadena de Pago | select | Deshabilitado por defecto |
| Certificado de Pago | texto | |
| Cadena Original del Comprobante de Pago | texto | |
| Sello del Pago | texto | |

### Reglas de negocio

- Los cuatro campos se habilitan solo cuando `Forma de Pago` = `03 Transferencia electrónica de fondos` y el pago se realiza vía SPEI.
- Si se captura `TipoCadena de Pago`, los tres campos restantes se vuelven obligatorios.

---

## 30. `SolicitudesCancelacionList` — Consulta de peticiones de cancelación

### DataGrid `SolicitudesCancelacionGrid`

Columnas exactas:

1. `Serie`
2. `Folio`
3. `Fecha Solicitud`
4. `uuid`
5. `total`
6. `Estatus`
7. `Es cancelable`
8. `Estatus Cancelación`

Botón: **Verificar Estatus SAT**.

### Reglas de negocio

- `Estatus`, `Es cancelable` y `Estatus Cancelación` son de solo lectura y se actualizan al pulsar **Verificar Estatus SAT** (consulta al web service del SAT).
- Valores observados de `Es cancelable`: `Cancelable sin aceptación`, (implícitos: `Cancelable con aceptación`, `No cancelable`).
- La rejilla es de solo consulta; no permite edición manual de los estatus.

---

## 31. `CatalogoSearchModal` — Interfaz de búsqueda de catálogos (REGLA GLOBAL)

> **Regla de negocio transversal:** *toda* interfaz de documentos y **cualquier punto del sistema donde se seleccione una clave de un catálogo** debe ofrecer su interfaz de búsqueda. Este componente es genérico y parametrizable por catálogo.

### Inputs

| Label | Tipo |
|---|---|
| Texto de Busqueda | texto (filtro incremental, aplica a todas las columnas) |

### DataGrid `CatalogoSearchGrid`

Columnas genéricas (parametrizables según el catálogo invocado):

1. `Clave` (valor)
2. `Descripción` / `Nombre`

### Puntos del sistema que deben invocarlo (mínimo)

- §1 Filtros: `Cliente Inicial`, `Cliente Final`
- §6 Producto: `Código SAT`, `Unidad SAT`
- §12/§15/§16/§20 Facturación: `Clave` de cliente, `Código` de producto en el grid de conceptos
- §16 Comercio Exterior: `Fracción Arancelaria`, `Residencia Fiscal`
- §26 Traslados: `Clave` de cliente destino, `Vehículo`, `Código SAT` / `unidad SAT` de productos, Figura Transporte
- §27 Pagos: `Clave` de cliente
- Todos los `select` de catálogos SAT: Régimen Fiscal, Uso CFDI, Método/Forma de Pago, Tipo Relación, Periodicidad, Clave Pedimento, INCOTERM, Config. Autotransporte, Tipo Permiso, Tipo Figura, Tipo de Inmueble, Estados, Municipios, Países

### Comportamiento requerido

- Se abre como modal desde un botón **lupa** (o equivalente) contiguo al campo de clave.
- Filtrado en vivo conforme se escribe, sobre clave y descripción.
- Al confirmar (doble clic o Enter sobre el renglón) devuelve la clave y **autocompleta los campos derivados** del formulario invocante.
- Cierre sin selección deja el campo sin modificar.
- Debe soportar teclado (navegación con flechas, Enter para confirmar, Esc para cerrar).

---

## Anexo — Reglas transversales del sistema

1. **Patrón de catálogo estándar:** todos los catálogos (§5, §7, §9, §10, §11) comparten la misma barra de herramientas: `Nuevo`, `Editar`, `Eliminar`, `Exportar`, `Buscar` + input de filtro, y abren un formulario modal con `Guardar` / `Cancelar`.
2. **Patrón de documento estándar:** todos los formularios de emisión (§12, §15, §16, §20, §26) comparten: cabecera General (Tipo de Documento, Folio autogenerado de solo lectura, Moneda, Tipo de Cambio, Tasa IVA), Datos del Cliente por clave con búsqueda, pestaña de Detalle con grid editable de conceptos, pestaña de CFDI Relacionados, pie con Observaciones/Condiciones de Pago y totales calculados, y botones `Generar Factura` / `Cancelar`.
3. **Campos derivados:** RFC, Nombre y Dirección del cliente, y todos los datos del vehículo, son de **solo lectura** — se resuelven a partir de la clave seleccionada.
4. **Tipo de Cambio:** deshabilitado cuando la moneda es MXN; obligatorio en cualquier otro caso.
5. **Cascada geográfica:** País → Estado → Municipio → Colonia/Localidad en todos los bloques de domicilio.
6. **Marcado de obligatoriedad:** el sistema legacy usa asterisco rojo (§8) o label en negrita (§18–§25). En el rediseño debe unificarse un único indicador.
7. **Permisos (§9):** cada pantalla queda condicionada a uno de tres permisos — *edición de catálogos*, *generación de documentos*, *configuración*.
8. **Licencias (§4):** los módulos de Notaría, Constructoras, Comercio Exterior e INE se habilitan por licencia registrada en los datos de la empresa.
9. **Búsqueda de catálogos (§31):** regla global, aplica a todo campo de clave del sistema.
