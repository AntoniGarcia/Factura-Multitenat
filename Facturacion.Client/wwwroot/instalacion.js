// El navegador dispara 'beforeinstallprompt' solo cuando decide que la PWA es instalable
// (manifiesto válido, service worker registrado, HTTPS). Guardamos ese evento para poder
// mostrar el botón de instalación "solo cuando el navegador lo permite", como pide
// PROMPT-FASES-A.md en la fase 2, y para poder disparar el prompt del sistema operativo
// más tarde, cuando el usuario haga clic.
let eventoDiferido = null;
let referenciaDotNet = null;

window.addEventListener('beforeinstallprompt', event => {
    event.preventDefault();
    eventoDiferido = event;
    referenciaDotNet?.invokeMethodAsync('NotificarInstalable');
});

window.addEventListener('appinstalled', () => {
    eventoDiferido = null;
    referenciaDotNet?.invokeMethodAsync('NotificarInstalada');
});

window.registrarInstalador = function (referencia) {
    referenciaDotNet = referencia;
    if (eventoDiferido) referencia.invokeMethodAsync('NotificarInstalable');
};

window.instalarPwa = async function () {
    if (!eventoDiferido) return false;

    eventoDiferido.prompt();
    const resultado = await eventoDiferido.userChoice;
    eventoDiferido = null;

    return resultado.outcome === 'accepted';
};
