(() => {
    'use strict';
    const pendientes = [];
    let activas = 0;
    let falloDefinitivo = false;
    const esperar = ms => new Promise(resolve => setTimeout(resolve, ms));

    function programar(accion) {
        return new Promise((resolve, reject) => {
            if (falloDefinitivo) { reject(new Error('La descarga del arranque se detuvo.')); return; }
            pendientes.push({ accion, resolve, reject });
            continuar();
        });
    }

    function continuar() {
        while (activas < 4 && pendientes.length) {
            const tarea = pendientes.shift();
            activas++;
            tarea.accion().then(tarea.resolve, tarea.reject).finally(() => { activas--; continuar(); });
        }
    }

    async function descargar(uri, integridad) {
        for (let intento = 0; intento < 3; intento++) {
            try {
                const respuesta = await fetch(uri, { integrity: integridad, cache: 'no-cache',
                    signal: AbortSignal.timeout(30000) });
                if (!respuesta.ok) throw new Error('Recurso de arranque no disponible.');
                // El turno se libera al terminar el cuerpo, no al recibir las cabeceras.
                const contenido = await respuesta.arrayBuffer();
                return new Response(contenido, { headers: { 'Content-Type': respuesta.headers.get('Content-Type') || 'application/octet-stream' } });
            } catch (error) {
                if (intento === 2 || falloDefinitivo) {
                    falloDefinitivo = true;
                    while (pendientes.length) pendientes.shift().reject(error);
                    throw error;
                }
                const aviso = document.getElementById('estado-arranque');
                if (aviso) aviso.textContent = 'Se interrumpió una descarga. Reintentando…';
                await esperar(1000 * (intento + 1));
            }
        }
    }

    function mostrarFallo() {
        const aviso = document.getElementById('estado-arranque');
        if (aviso) {
            aviso.textContent = 'No se pudo completar la descarga de la aplicación. Comprueba tu conexión y vuelve a intentarlo.';
            aviso.setAttribute('role', 'alert');
        }
        const boton = document.getElementById('reintentar-arranque');
        if (boton) boton.hidden = false;
    }

    document.getElementById('reintentar-arranque')?.addEventListener('click', () => window.location.reload());
    // Si falla el script inicial, todavía no existe Blazor para devolver una promesa.
    if (!window.Blazor?.start) {
        mostrarFallo();
        return;
    }
    window.Blazor.start({
        applicationCulture: 'es-MX',
        loadBootResource(tipo, nombre, uri, integridad) {
            if (tipo === 'dotnetjs') return null;
            return programar(() => descargar(uri, integridad));
        }
    }).catch(mostrarFallo);
})();
