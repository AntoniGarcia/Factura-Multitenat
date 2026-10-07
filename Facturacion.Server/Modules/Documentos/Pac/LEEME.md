# Integración del PAC

Guía para quien conecte el PAC real. Todo lo demás del timbrado y la cancelación ya está hecho y
**no depende de qué PAC sea**. Lo único que falta es una clase que implemente `IProveedorPac`.

## Qué hay que escribir

Una clase en esta carpeta que implemente `IProveedorPac.cs`. Sus cuatro métodos:

| Método | Lo llama | Qué debe devolver |
|---|---|---|
| `TimbrarAsync(xml, clave)` | `Timbrado/ServicioDeTimbrado` | `Timbrado` con UUID, fecha (UTC), número de certificado del SAT, sello del SAT, cadena original del TFD y el **XML timbrado completo**; `Rechazado` si el PAC rechaza el comprobante; `ErrorDeComunicacion` si no se sabe qué pasó |
| `ConsultarAsync(xml, clave)` | `Timbrado/ConciliacionDeTimbrados` | Igual que el anterior para una clave ya enviada; `NoEncontrado` si el PAC nunca la recibió |
| `CancelarAsync(datos)` | `Cancelacion/ServicioDeCancelacion` | `Cancelado`, `EnEsperaDelReceptor`, `Rechazado` o `ErrorDeComunicacion` |
| `ConsultarEstatusAsync(datos)` | `Cancelacion/ServicioDeCancelacion` | El estado del CFDI con los textos **tal como los da el SAT**: «Vigente», «Cancelado», «Cancelable con aceptación», «En proceso», «Solicitud rechazada», «Plazo vencido»… |

Las reglas que no se negocian:

- **Si no se sabe qué pasó, es `ErrorDeComunicacion`, nunca `Rechazado`.** Un rechazo revierte el
  comprobante y devuelve el timbre; si en realidad sí se timbró, quedan dos folios fiscales para
  una sola venta.
- **La clave de idempotencia viaja al PAC.** Se genera una por intento y se reutiliza en cada
  reintento y en la conciliación. El PAC debe devolver el mismo timbre ante la misma clave.
- **El CSD llega en claro en `DatosDeCancelacion`.** No se registra en el log ni se serializa.
- **Nada de reintentos dentro de la clase.** `ServicioDeTimbrado` ya reintenta con espera creciente.
- **El XML ya viene sellado.** No hay que volver a sellarlo.

`ProveedorPacSwSapien.cs` es una implementación para SW Sapien que se escribió contra su sandbox y
**no se ha probado** con timbrados reales. Se puede terminar esa o escribir otra para otro PAC.

## Cómo se activa

`DocumentosModule.AgregarPac` ya registra `ProveedorPacSwSapien` cuando el modo es `Real` y hay
credenciales. Para otro PAC se cambia esa línea por la clase nueva:

```csharp
servicios.AddSingleton<IProveedorPac, ProveedorPacSwSapien>();
```

La configuración va en variables de entorno (nunca en el repositorio):

```text
Pac__Modo=Real
Pac__UrlBase=...
Pac__Usuario=...
Pac__Contrasena=...
```

Si el PAC nuevo necesita otros datos, se agregan a `OpcionesDePac.cs`.

## Modos

| `Pac:Modo` | Dónde | Qué hace |
|---|---|---|
| `Real` | cualquier entorno | Usa el PAC configurado |
| `Deshabilitado` | `Staging` | No timbra ni cancela; bloquea antes de gastar folio o timbre |
| `Simulado` | **solo `Development`** | `Dobles/DobleProveedorPac` y `Dobles/DobleProveedorCsdParaTimbrado` |

El modo `Simulado` sirve para probar todo el circuito —timbrado, PDF, correo, cancelación y
seguimiento— sin PAC ni CSD. El PDF lleva la leyenda «Timbre simulado en desarrollo. Sin validez
fiscal». Fuera de `Development` el servidor no arranca con ese modo.

## Antes de producción

1. Tanda de **50 timbrados seguidos** en el sandbox del PAC, con el resultado por escrito.
2. Verificar un comprobante real en `verificacfdi.facturaelectronica.sat.gob.mx` con el QR del PDF.
3. Probar una cancelación con aceptación y seguirla desde la pantalla de solicitudes.
4. Quitar la carpeta `Dobles/` y el valor `Simulado` de `ModoDePac`.
