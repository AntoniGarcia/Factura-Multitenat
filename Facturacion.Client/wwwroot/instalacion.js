// Conserva el evento de instalación de la PWA para mostrar el botón solo cuando el navegador lo permita.
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
