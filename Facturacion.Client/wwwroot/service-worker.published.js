// Service worker de producción. Cachea SOLO el app shell —los archivos que hacen falta para
// arrancar la aplicación sin red— y JAMÁS una respuesta de /api/ (CLAUDE.md §4). Un catálogo
// del SAT cacheado y viejo produce comprobantes mal emitidos: esto no es una comodidad, es
// una salvaguarda, y por eso la exclusión de /api/ es la primera línea de onFetch, no algo
// que se deduce de qué patrones SÍ están en la lista de inclusión.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;

// El app shell: lo necesario para que la interfaz pinte sin red. Los catálogos del SAT, las
// listas de clientes y todo lo demás vienen siempre de /api/, nunca de aquí.
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: instalando');

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: activando');

    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    // Exclusión explícita, antes que cualquier otra cosa: ninguna petición a /api/ pasa
    // jamás por la caché, ni para leer ni para servir una respuesta ya guardada. Va directo
    // a la red, siempre.
    if (esPeticionDeApi(event.request)) {
        return fetch(event.request);
    }

    if (event.request.method !== 'GET') {
        return fetch(event.request);
    }

    // Toda navegación (F5, abrir un enlace) sirve el shell cacheado, salvo que la URL sea
    // uno de los propios archivos versionados del manifiesto.
    const debeServirIndice = event.request.mode === 'navigate'
        && !manifestUrlList.some(url => url === event.request.url);

    const peticion = debeServirIndice ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const respuestaCacheada = await cache.match(peticion);

    return respuestaCacheada || fetch(event.request);
}

function esPeticionDeApi(request) {
    return new URL(request.url).pathname.startsWith('/api/');
}
