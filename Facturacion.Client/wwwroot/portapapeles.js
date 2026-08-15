// Copiar al portapapeles desde PanelErrores. navigator.clipboard exige un contexto seguro
// (https), que ya es lo único que sirve esta aplicación.
window.copiarAlPortapapeles = function (texto) {
    return navigator.clipboard.writeText(texto);
};
