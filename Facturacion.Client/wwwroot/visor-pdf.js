const documentos = new WeakMap();

export async function cargar(canvas, flujo) {
    const estado = { cerrado: false, pagina: 1, tarea: null, pdf: null, render: null, cola: Promise.resolve() };
    documentos.set(canvas, estado);
    try {
        const pdfjs = await import('./vendor/pdfjs-6.5.438/pdf.min.mjs');
        if (estado.cerrado) return 0;
        pdfjs.GlobalWorkerOptions.workerSrc = new URL('./vendor/pdfjs-6.5.438/pdf.worker.min.mjs', import.meta.url).href;
        const data = new Uint8Array(await flujo.arrayBuffer());
        if (estado.cerrado) return 0;
        estado.tarea = pdfjs.getDocument({ data, isEvalSupported: false, useWasm: false,
            disableWorkerRendering: true, disableFontFace: true, useSystemFonts: false });
        estado.pdf = await estado.tarea.promise;
        if (estado.cerrado) return 0;
        await mostrarPagina(canvas, 1);
        estado.observador = new ResizeObserver(() => {
            clearTimeout(estado.temporizador);
            estado.temporizador = setTimeout(() => mostrarPagina(canvas, estado.pagina).catch(() => {}), 150);
        });
        estado.observador.observe(canvas.parentElement);
        return estado.pdf.numPages;
    } catch (error) {
        await liberar(canvas);
        throw error;
    }
}

export async function mostrarPagina(canvas, numero) {
    const estado = documentos.get(canvas);
    if (!estado || estado.cerrado) return;
    estado.cola = estado.cola.catch(() => {}).then(async () => {
        if (estado.cerrado) return;
        const pagina = await estado.pdf.getPage(numero);
        if (estado.cerrado) return;
        const normal = pagina.getViewport({ scale: 1 });
        const ancho = Math.max(1, canvas.parentElement.clientWidth - 2);
        const escala = Math.min(window.devicePixelRatio || 1, 2);
        const vista = pagina.getViewport({ scale: ancho / normal.width * escala });
        canvas.width = Math.floor(vista.width);
        canvas.height = Math.floor(vista.height);
        canvas.style.width = `${ancho}px`;
        estado.render = pagina.render({ canvasContext: canvas.getContext('2d'), viewport: vista });
        try { await estado.render.promise; }
        finally { estado.render = null; pagina.cleanup(); }
        estado.pagina = numero;
        canvas.setAttribute('aria-label', `Página ${numero} de ${estado.pdf.numPages}`);
    });
    return estado.cola;
}

export async function liberar(canvas) {
    const estado = documentos.get(canvas);
    if (!estado || estado.cerrado) return;
    estado.cerrado = true;
    clearTimeout(estado.temporizador);
    estado.observador?.disconnect();
    estado.render?.cancel();
    await estado.cola.catch(() => {});
    if (estado.tarea) await estado.tarea.destroy();
    canvas.width = 0;
    canvas.height = 0;
    documentos.delete(canvas);
}
