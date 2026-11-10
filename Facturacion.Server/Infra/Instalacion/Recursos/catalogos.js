"use strict";
(async () => {
    const estado = document.getElementById("estado");
    const detalle = document.getElementById("detalle");
    const progreso = document.getElementById("avance");
    for (;;) {
        try {
            const respuesta = await fetch("/api/instalacion/catalogos", { cache: "no-store", credentials: "omit", signal: AbortSignal.timeout(10000) });
            if (!respuesta.ok) {
                const problema = await respuesta.json().catch(() => null);
                if (respuesta.status === 503 && problema?.type === "urn:facturacion:error:base-en-mantenimiento") {
                    estado.textContent = "La base está en mantenimiento.";
                    estado.setAttribute("role", "alert");
                    progreso.hidden = true;
                    detalle.textContent = "Abre «Actualización de la base» para consultar el resultado del script. La carga de catálogos está pausada durante el mantenimiento.";
                    return;
                }
                throw new Error();
            }
            const datos = await respuesta.json();
            progreso.max = datos.total;
            progreso.value = datos.completados;
            if (datos.listo) { window.location.reload(); return; }
            if (datos.estado === "error") {
                estado.textContent = "No se pudo completar la carga.";
                estado.setAttribute("role", "alert");
                detalle.textContent = "Revisa los archivos SAT y los permisos de la base de datos.";
                return;
            }
            estado.textContent = datos.estado === "esperando" ? "Esperando otra carga…"
                : datos.catalogo ? `Cargando ${datos.catalogo}` : "Comprobando catálogos…";
            detalle.textContent = `${datos.completados} de ${datos.total} catálogos preparados.`;
        } catch { estado.textContent = "Reconectando con el servidor…"; }
        await new Promise(resolve => setTimeout(resolve, 1500));
    }
})();
