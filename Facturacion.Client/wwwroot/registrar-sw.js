// Registro del service worker. Va en un archivo aparte y no en línea dentro de index.html
// porque la CSP prohíbe 'unsafe-inline' en script-src.
window.registrarPwa = function () {
    if (!('serviceWorker' in navigator)) return;
    const registrar = () => navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
        .catch(() => console.warn('No se pudo preparar el acceso sin conexión.'));
    if ('requestIdleCallback' in window) window.requestIdleCallback(registrar, { timeout: 5000 });
    else setTimeout(registrar, 1000);
};

// Fuerza la comprobación y activación del nuevo service worker antes de recargar el documento.
window.actualizarYRecargarPwa = async function () {
    try {
        if ('serviceWorker' in navigator) {
            const registro = await navigator.serviceWorker.getRegistration();
            if (registro) {
                await registro.update();
                if (registro.waiting) {
                    registro.waiting.postMessage('SKIP_WAITING');
                }
            }
        }
    } catch {
        // Si falla la actualización del service worker, igual recargamos el documento.
    }
    window.location.reload();
};
