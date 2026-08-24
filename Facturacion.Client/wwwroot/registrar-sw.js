// Registro del service worker. Va en un archivo aparte y no en línea dentro de index.html
// porque la CSP prohíbe 'unsafe-inline' en script-src (ARQUITECTURA.md §4).
navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' });
