(() => {
    'use strict';
    const el = id => document.getElementById(id);
    let token = '', preparado = null, ocupado = false, seguimiento = null;
    let puedeActualizar = false, ejecutando = false;
    function actualizarBotonRevision() {
        el('revision').querySelector('button').disabled = ocupado || ejecutando || !puedeActualizar;
        el('motivo-revision').textContent = !puedeActualizar
            ? 'Revisar script está deshabilitado hasta resolver los requisitos indicados arriba. Seleccionar un archivo no lo envía al servidor.'
            : ejecutando ? 'Espera a que termine la actualización en curso.' : '';
    }
    function mensaje(texto, error = false) { el('mensaje').textContent = texto; el('mensaje').dataset.error = String(error); }
    async function api(ruta = '', opciones = {}) {
        const respuesta = await fetch('/api/mantenimiento-base' + ruta, {
            ...opciones, cache: 'no-store', headers: { ...(opciones.headers || {}), ...(token ? { Authorization: 'Mantenimiento ' + token } : {}) }
        });
        if (!respuesta.ok) {
            if (respuesta.status === 401) {
                token = ''; el('acceso').hidden = false; el('actualizacion').hidden = true;
                clearTimeout(seguimiento);
                throw new Error('Confirma de nuevo tus credenciales. El acceso puede haber vencido.');
            }
            let problema; try { problema = await respuesta.json(); } catch { }
            throw new Error(problema?.detail || problema?.detalle || 'No se pudo completar la solicitud.');
        }
        return respuesta.json();
    }
    function mostrar(estado) {
        if (!estado) { el('resultado').textContent = 'Sin actualizaciones registradas.'; return; }
        el('resultado').textContent = `${estado.estado}: ${estado.mensaje}`;
        ejecutando = ['en_cola', 'ejecutando'].includes(estado.estado);
        actualizarBotonRevision();
        if (ejecutando) seguimiento = setTimeout(actualizar, 2000);
    }
    function mostrarEsquema(esquema) {
        const lista = el('estado-diferencias');
        lista.replaceChildren();
        if (!esquema || esquema.error) {
            el('estado-historial').textContent = esquema?.error || 'No se pudo leer el estado de la base.';
            el('estado-estructura').textContent = '';
            return;
        }
        const pendientes = esquema.migracionesPendientes || [];
        el('estado-historial').textContent = !esquema.historialEf
            ? 'La base no tiene historial de migraciones de EF (__EFMigrationsHistory). Su estado se juzga solo por la estructura.'
            : `Historial de EF: ${esquema.migracionesRegistradas} migraciones registradas; la última es ${esquema.ultimaMigracion || 'ninguna'}. `
              + (pendientes.length ? `Faltan en el historial: ${pendientes.join(', ')}.` : 'No falta ninguna de esta versión.');
        const diferencias = esquema.diferencias || [];
        el('estado-estructura').textContent = diferencias.length
            ? `La estructura no coincide con esta versión (${diferencias.length} diferencias):`
            : 'La estructura coincide con esta versión.';
        const maximo = 30;
        for (const texto of diferencias.slice(0, maximo)) {
            const item = document.createElement('li'); item.textContent = texto; lista.append(item);
        }
        if (diferencias.length > maximo) {
            const item = document.createElement('li'); item.textContent = `…y ${diferencias.length - maximo} más.`; lista.append(item);
        }
    }
    async function actualizar() {
        clearTimeout(seguimiento);
        puedeActualizar = false;
        actualizarBotonRevision();
        try {
            const datos = await api();
            el('destino').textContent = `${datos.servidor} / ${datos.base} — SQL ${datos.versionSql}`;
            puedeActualizar = datos.puedeActualizar;
            el('permisos').textContent = puedeActualizar ? 'Conexión lista para actualizar.' : 'La conexión de la aplicación no cumple estos requisitos de SQL Server. Solicita su revisión al administrador SQL:';
            el('requisitos-faltantes').replaceChildren();
            for (const requisito of datos.requisitosFaltantes || []) {
                const item = document.createElement('li'); item.textContent = requisito;
                el('requisitos-faltantes').append(item);
            }
            mostrarEsquema(datos.esquema);
            ejecutando = false;
            actualizarBotonRevision();
            mostrar(datos.ultimoIntento);
        } catch (e) { mensaje(e.message, true); if (token) seguimiento = setTimeout(actualizar, 5000); }
    }
    async function ejecutarFormulario(evento, accion) {
        evento.preventDefault(); if (ocupado) return;
        ocupado = true; const boton = evento.target.querySelector('button'); boton.disabled = true;
        try { await accion(evento.target); } catch (e) { mensaje(e.message, true); }
        finally { ocupado = false; boton.disabled = false; actualizarBotonRevision(); }
    }
    el('comprobar-requisitos').addEventListener('click', actualizar);
    el('acceso').addEventListener('submit', e => ejecutarFormulario(e, async form => {
        const p = { correo: form.correo.value, contrasena: form.contrasena.value }; form.contrasena.value = '';
        const datos = await api('/acceder', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) });
        token = datos.token; el('acceso').hidden = true; el('actualizacion').hidden = false;
        mensaje('Acceso confirmado.'); await actualizar();
    }));
    el('revision').addEventListener('submit', e => ejecutarFormulario(e, async form => {
        const archivo = form.archivo.files[0];
        if (!archivo || archivo.size > 2 * 1024 * 1024) throw new Error('Selecciona un script SQL de hasta 2 MB.');
        preparado = null; el('ejecucion').hidden = true;
        const datos = new FormData(); datos.append('archivo', archivo);
        preparado = await api('/revisar', { method: 'POST', body: datos });
        el('archivo-revisado').textContent = preparado.nombre;
        el('ejecucion').hidden = false; el('ejecucion').reset();
        mensaje(preparado.mensaje); mostrar(preparado);
    }));
    el('ejecucion').addEventListener('submit', e => ejecutarFormulario(e, async form => {
        if (!preparado) throw new Error('Revisa el script antes de ejecutar.');
        const p = { id: preparado.id, contrasena: form.contrasena.value, respaldoConfirmado: form.respaldo.checked };
        form.contrasena.value = '';
        const estado = await api('/ejecutar', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) });
        preparado = null; form.hidden = true; mensaje('Actualización iniciada. Conserva el respaldo hasta comprobar el resultado.'); mostrar(estado);
    }));
})();
