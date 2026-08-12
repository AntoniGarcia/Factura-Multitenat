# Plan de fases — Mitad B (Documentos, timbrado y salidas)

Este documento es para **el compañero**. Se le entrega junto con `CLAUDE.md` y
`REPARTO-EQUIPO.md`.

Luis lo conserva también por una razón concreta: si el 15 de octubre la mitad B no
timbra contra el sandbox del PAC, esta mitad se absorbe (`REPARTO-EQUIPO.md` §6), y
entonces estos prompts se usan tal cual, cambiando la línea de `CLAUDE.md` que dice
qué mitad se trabaja.

---

## Advertencia para quien tome esta mitad

Aquí vive todo lo que puede fallar de forma cara: dinero mal calculado, comprobantes
duplicados, timbres consumidos sin factura, y datos fiscales que ya no se pueden
corregir porque el SAT los tiene.

Tres reglas que no se negocian:

1. **Nunca una llamada HTTP dentro de una transacción de base de datos.** El PAC puede
   tardar 30 segundos; una transacción abierta ese tiempo tumba el sistema completo.
2. **El timbrado son tres pasos, no uno.** Transacción corta que reserva → llamada al
   PAC fuera de toda transacción → transacción corta que confirma o revierte.
3. **Al timbrar se congelan los datos.** Nombre, RFC, régimen y CP del receptor, y la
   descripción y clave de cada concepto, se copian dentro del comprobante. Un reporte
   de 2026 no puede cambiar porque un cliente actualizó su domicilio en 2028.

---

## Fases

| Fase | Contenido | Modelo | Depende de |
|---|---|---|---|
| B0 | Dobles de prueba y esqueleto del módulo | Sonnet | fase 0 de A |
| B1 | Motor de cálculo de impuestos | **Opus** | — |
| B2 | Formulario de emisión y borradores | Sonnet | fase 2 de A |
| B3 | Generación del XML CFDI 4.0 | **Opus** | fase 3 de A |
| B4 | Integración con el PAC y timbrado en tres pasos | **Opus** | fases 4 y 7 de A |
| B5 | PDF con QR y cadena original | Sonnet | B4 |
| B6 | Envío por correo | Sonnet | B5 |
| B7 | Listado de documentos y filtros | Sonnet | B4 |
| B8 | Cancelación y consulta de estatus ante el SAT | **Opus** | B4 |
| B9 | Complemento de pagos | **Opus** | B4 |

---

## Fase B0 — Dobles de prueba y esqueleto

▸ PROMPT

```
Lee CLAUDE.md y REPARTO-EQUIPO.md completos. Trabajo la mitad B.

Fase B0. Antes de construir nada de negocio, monta lo que te permite trabajar sin
depender de la mitad A.

1. Esqueleto del módulo: Server/Modules/Documentos/ con DocumentosModule.cs
   (AddDocumentos / MapDocumentos), ya cableado en Program.cs.

2. Dobles de prueba, en Server/Modules/Documentos/Dobles/, registrados SOLO
   cuando el entorno es Development, de las interfaces de Shared/Contratos:
   IServicioCatalogosSat, IServicioClientes, IServicioProductos,
   IServicioEmpresaEmisora, IServicioFolios, IServicioTimbres.
   Datos falsos pero REALISTAS: RFC válidos con dígito verificador correcto,
   claves de c_ClaveProdServ que existan de verdad, un CSD de pruebas del SAT.
   Un doble que devuelve datos imposibles esconde errores hasta la integración.

3. Implementación real de IResumenDocumentos, aunque devuelva ceros: es lo que
   la mitad A necesita para su tablero.

4. Entidades de tu mitad, con sus IEntityTypeConfiguration en
   Data/Configurations/Documentos/: Comprobante, Concepto, ImpuestoConcepto,
   ComprobanteRelacionado, IntentoTimbrado, SolicitudCancelacion.
   El Comprobante lleva copias congeladas de los datos fiscales del emisor y del
   receptor, no llaves foráneas a ellos.
   Migración con nombre 2026MMDD_B_DocumentosBase.

NO toques ningún archivo de la mitad A. Si necesitas un cambio ahí, dímelo.

Al terminar detente y dime qué construiste y si algún contrato de Shared te
quedó corto.
```

---

## Fase B1 — Motor de cálculo de impuestos · **Opus**

▸ PROMPT

```
Fase B1: el motor de impuestos. Es la parte que más se revisa y la que más caro
sale equivocar. Va aislado, sin depender de la base ni de la interfaz, para poder
probarlo solo.

REGLAS DE CÁLCULO
   Importe del concepto = Cantidad × Valor unitario, redondeado a 6 decimales.
   Descuento se resta del importe antes de calcular la base.
   Base gravable por concepto = Importe − Descuento.
   Traslados: IVA con tasa 0.160000, 0.080000 o 0.000000, o exento. El exento NO
   es tasa cero: exento no lleva nodo de impuesto con importe, tasa cero sí.
   Retenciones: IVA e ISR, con su tasa, calculadas sobre la base del concepto.
   Los totales del comprobante son la SUMA de los impuestos por concepto, no el
   impuesto calculado sobre el total. La diferencia por redondeo es la causa
   número uno de rechazo del PAC.
   Todo con decimal, cálculo a 6 decimales, presentación a 2.
   Redondeo bancario (MidpointRounding.ToEven) salvo que verifiques que el SAT
   pide otro; si encuentras que pide otro, dímelo con la referencia.

   SubTotal = suma de importes antes de descuentos.
   Descuento = suma de descuentos.
   Total = SubTotal − Descuento + traslados − retenciones.

MONEDA EXTRANJERA
   Si la moneda no es MXN, el tipo de cambio es obligatorio. Los importes van en
   la moneda del comprobante; el tipo de cambio solo se declara.

ENTREGABLE
   Una clase pura, sin dependencias de EF ni de HTTP, con su juego de pruebas.
   Al menos 20 casos: tasas mezcladas, exento junto a gravado, descuentos,
   retenciones, moneda extranjera, y los casos frontera de redondeo donde la
   suma por concepto difiere del cálculo sobre el total.

Al terminar detente y dime qué construiste y qué caso de redondeo te preocupa.
```

---

## Fases B2 a B9 — resumen de contenido

Los prompts detallados de estas fases se escriben al cerrar la anterior, con lo
aprendido. Su contenido acordado:

**B2 — Formulario de emisión.** Referencia funcional §12, §13 y §14. Cabecera con
tipo de documento, moneda, tipo de cambio y tasa de IVA; datos del cliente resueltos
por clave con `BuscadorCatalogo`; rejilla editable de conceptos que resuelve producto
por código y autocompleta unidad, descripción y precio; pie con totales calculados en
vivo por el motor de B1. Pestaña de CFDI relacionados. Pestaña de información global,
habilitada **solo** cuando el receptor es `XAXX010101000`. Borradores que se guardan
sin consumir folio ni timbre. Optimizado para teclado: el capturista no debe tocar el
ratón para capturar una factura de diez renglones.

**B3 — Generación del XML.** Serialización a CFDI 4.0 validada contra el XSD oficial
antes de enviarla a ningún lado. Cadena original con el XSLT del SAT. Aquí se congelan
los datos fiscales dentro del comprobante.

**B4 — PAC y timbrado.** Los tres pasos, con `Idempotency-Key`. Reintentos con espera
creciente. Un proceso de conciliación que revisa los comprobantes que quedaron en
`timbrando` más de 30 minutos y pregunta al PAC qué pasó con ellos: sin esto, un corte
de red deja facturas en un limbo del que nadie las saca. Sandbox del PAC primero;
producción solo cuando el sandbox pase 50 timbrados seguidos.

**B5 — PDF.** Representación impresa con los campos de §1.4 del documento funcional,
QR con el formato exacto que pide el SAT, cadena original del complemento de
certificación, sello del CFDI y sello del SAT. Logo de la empresa desde el almacén
cifrado.

**B6 — Correo.** Envío por el remitente propio del SaaS con `Reply-To` a la empresa
(`REPARTO-EQUIPO.md` §1). XML y PDF adjuntos, XML opcional por casilla. Registro de
envíos y reenvío desde el listado.

**B7 — Listado de documentos.** Referencia §1.1 y §1.2. Filtros por rango de fechas,
cliente, tipo y estatus. Paginación en servidor con `Virtualize`. Exportación a CSV.
Descarga de XML y PDF por endpoint autorizado.

**B8 — Cancelación.** Motivos 01 a 04. El motivo 01 exige el UUID del comprobante que
sustituye. Consulta de estatus ante el SAT (§30). Estados `en_cancelacion` mientras el
receptor acepta o rechaza. Devolución del timbre cuando aplica.

**B9 — Complemento de pagos.** Referencia §27, §28 y §29. Solo sobre comprobantes con
método `PPD`. Cálculo de saldo anterior, importe pagado, saldo insoluto y número de
parcialidad. Validación de que la suma de la rejilla cuadre con el importe de la
cabecera. Datos bancarios obligatorios según la forma de pago.

---

## Si Luis absorbe esta mitad

El orden mínimo para tener algo timbrando, sacrificando lo demás:

**B1 → B3 → B4 → B5 → B2 → B7**, y se recorta B9 (bloqueando `PPD`), B8 pasa a
cancelación manual desde el portal del SAT, y B6 se reemplaza por descarga manual del
XML y el PDF.

Eso es un sistema que emite y timbra, que es lo mínimo que se puede entregar. No es
bonito, pero es entregable, y se llega desde el 15 de octubre con siete semanas.
