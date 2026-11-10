// Registro del service worker. Va en un archivo aparte y no en línea dentro de index.html
// porque la CSP prohíbe 'unsafe-inline' en script-src.
window.registrarPwa = function () {
    if (!('serviceWorker' in navigator)) return;
    const registrar = () => navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
        .catch(() => console.warn('No se pudo preparar el acceso sin conexión.'));
    if ('requestIdleCallback' in window) window.requestIdleCallback(registrar, { timeout: 5000 });
    else setTimeout(registrar, 1000);
};
