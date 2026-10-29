using System.Text;
using Facturacion.Shared.Plataforma;

namespace Facturacion.Server.Modules.Plataforma.Clientes;

/// <summary>
/// Convierte el CSV de clientes en renglones revisables. <b>No guarda nada</b>: igual que el de
/// productos, cada renglón se valida por su cuenta y uno malo no tira el archivo.
/// <para>
/// Usa las mismas columnas que la exportación, así que un catálogo exportado se puede corregir
/// en Excel y volver a subir. El domicilio de entrega no viaja en el CSV: se captura en la
/// pantalla del cliente.
/// </para>
/// </summary>
public static class LectorDeCsvDeClientes
{
    /// <summary>Encabezados que se esperan, en este orden. Activo puede ir al final.</summary>
    public static readonly string[] Columnas =
    [
        "RFC", "Nombre", "RegimenFiscal", "CodigoPostalFiscal", "UsoCFDI", "MetodoPago", "FormaPago",
        "Correo", "Telefono", "ResidenciaFiscal", "NumRegIdTrib"
    ];

    private const int MaximoRenglones = 5000;

    public static VistaPreviaDeImportacionDeClientes Analizar(Stream archivo)
    {
        // Detecta la marca de orden de bytes que traen los CSV de Excel.
        using var lector = new StreamReader(archivo, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var renglones = new List<RenglonDeImportacionDeCliente>();
        var numero = 0;
        var primera = true;
        var formatoExportado = false;

        while (lector.ReadLine() is { } linea)
        {
            if (primera)
            {
                primera = false;
                if (EsEncabezado(linea, out formatoExportado)) continue;
            }

            if (string.IsNullOrWhiteSpace(linea)) continue;

            numero++;

            if (numero > MaximoRenglones)
            {
                renglones.Add(new RenglonDeImportacionDeCliente(numero, null,
                    $"El archivo pasa de {MaximoRenglones} renglones. Pártelo en varios."));
                break;
            }

            renglones.Add(LeerRenglon(numero, linea, formatoExportado));
        }

        return new VistaPreviaDeImportacionDeClientes(
            renglones,
            renglones.Count(r => r.Error is null),
            renglones.Count(r => r.Error is not null));
    }

    // El archivo exportado empieza con la clave interna, que es informativa: la coincidencia
    // se resuelve por RFC.
    private static bool EsEncabezado(string linea, out bool formatoExportado)
    {
        var campos = LectorCsv.Partir(linea);
        formatoExportado = campos.Count > 1
            && campos[0].Trim().Equals("Clave", StringComparison.OrdinalIgnoreCase)
            && campos[1].Trim().Equals("RFC", StringComparison.OrdinalIgnoreCase);

        return formatoExportado || campos[0].Trim().Equals("RFC", StringComparison.OrdinalIgnoreCase);
    }

    private static RenglonDeImportacionDeCliente LeerRenglon(int numero, string linea, bool formatoExportado)
    {
        var campos = LectorCsv.Partir(linea);

        if (formatoExportado)
        {
            if (campos.Count < 2) return Malo(numero, "El renglón no trae la clave y el RFC.");
            campos.RemoveAt(0);
        }

        if (campos.Count < 4)
            return Malo(numero, $"Se esperaban al menos 4 columnas (RFC, nombre, régimen y código postal) y llegaron {campos.Count}.");

        string? Opcional(int indice)
            => indice < campos.Count && !string.IsNullOrWhiteSpace(campos[indice]) ? campos[indice].Trim() : null;

        var rfc = campos[0].Trim().ToUpperInvariant();
        var nombre = campos[1].Trim();

        if (rfc.Length == 0) return Malo(numero, "Falta el RFC.");
        if (nombre.Length == 0) return Malo(numero, "Falta el nombre.");

        var activoExplicito = Opcional(Columnas.Length) is not null;
        var activo = true;
        if (activoExplicito && !LectorCsv.TryLeerSiNo(campos[Columnas.Length], out activo))
            return Malo(numero, $"El valor de Activo «{campos[Columnas.Length]}» no se entiende. Escribe Sí o No.");

        return new RenglonDeImportacionDeCliente(numero,
            new PeticionGuardarCliente(
                rfc, nombre, campos[2].Trim(), campos[3].Trim(),
                ResidenciaFiscal: Opcional(9), NumRegIdTrib: Opcional(10),
                UsoCfdiPreferido: Opcional(4), MetodoPagoPreferido: Opcional(5), FormaPagoPreferida: Opcional(6),
                Telefono: Opcional(8), CorreoPrincipal: Opcional(7),
                Calle: null, NumeroExterior: null, NumeroInterior: null, Colonia: null, Localidad: null,
                Referencia: null, Municipio: null, Estado: null, Pais: null, CodigoPostal: null,
                Activo: activo),
            null, activoExplicito);
    }

    private static RenglonDeImportacionDeCliente Malo(int numero, string error) => new(numero, null, error);
}
