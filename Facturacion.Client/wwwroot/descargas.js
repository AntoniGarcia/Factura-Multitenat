async function crearUrl(tipoContenido, flujo) {
    const contenido = await flujo.arrayBuffer();
    return URL.createObjectURL(new Blob([contenido], { type: tipoContenido }));
}

export async function guardarDescarga(nombre, tipoContenido, flujo) {
    const url = await crearUrl(tipoContenido, flujo);
    const enlace = document.createElement("a");

    enlace.href = url;
    enlace.download = nombre;
    enlace.style.display = "none";
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function crearVistaPrevia(tipoContenido, flujo) {
    return crearUrl(tipoContenido, flujo);
}

export function liberarVistaPrevia(url) {
    URL.revokeObjectURL(url);
}
