"use strict";
(() => {
    const porId = id => document.getElementById(id);
    const formulario = porId("formulario");
    const campos = porId("campos");
    const guardar = porId("guardar");
    const formularioOperador = porId("formulario-operador");
    let probada = false;
    let ocupado = false;
    let cerrada = false;
    let finalizando = false;
    let accesoPendiente = null;

    async function abrirAcceso() {
        if (finalizando) return;
        finalizando = true;
        porId("abrir-acceso").disabled = true;
        try {
            if (!accesoPendiente) {
                const respuesta = await fetch("/api/instalacion/finalizar", {
                    method: "POST", credentials: "omit", cache: "no-store"
                });
                const resultado = await respuesta.json();
                if (!respuesta.ok) {
                    mensaje(resultado.detalle || resultado.detail || "No se pudo finalizar.", true, resultado.traceId);
                    return;
                }
                accesoPendiente = resultado.acceso;
            }
            mensaje("Abriendo acceso…");
            for (let intento = 0; intento < 40; intento++) {
                try {
                    const estado = await fetch("/api/version", {
                        credentials: "omit", cache: "no-store", redirect: "manual",
                        signal: AbortSignal.timeout(2000)
                    });
                    if (estado.ok || estado.type === "opaqueredirect") {
                        window.location.replace(accesoPendiente);
                        return;
                    }
                } catch { }
                await new Promise(resolve => setTimeout(resolve, 500));
            }
            mensaje("El acceso tarda en responder. Vuelve a intentar.", true);
        } catch {
            mensaje("No se pudo abrir el acceso. Vuelve a intentar.", true);
        } finally {
            finalizando = false;
            porId("abrir-acceso").disabled = false;
        }
    }
    porId("abrir-acceso").addEventListener("click", abrirAcceso);

    function mensaje(texto, error = false, traza = "") {
        const zona = porId("mensaje");
        zona.className = error ? "error" : "exito";
        zona.setAttribute("role", error ? "alert" : "status");
        zona.hidden = false;
        porId("texto-mensaje").textContent = texto;
        porId("traza").textContent = traza ? `Referencia para soporte: ${traza}` : "";
    }

    function terminar(texto) {
        cerrada = true;
        formulario.reset();
        formulario.hidden = true;
        porId("finalizado").hidden = false;
        if (texto) porId("detalle-finalizado").textContent = texto;
        porId("paso-conexion").removeAttribute("aria-current");
        porId("paso-guardar").setAttribute("aria-current", "step");
    }

    function parametros() {
        return {
            servidor: porId("servidor").value,
            baseDeDatos: porId("base").value,
            usuario: porId("usuario").value,
            contrasena: porId("contrasena").value,
            usarAutenticacionWindows: porId("autenticacion").value === "windows",
            confiarEnCertificado: porId("confiar").checked
        };
    }

    async function enviar(accion) {
        if (ocupado || cerrada || !formulario.reportValidity()) return;
        ocupado = true;
        const datos = parametros();
        campos.disabled = true;
        porId("probar").textContent = accion === "guardar" ? "Guardando…" : "Comprobando…";
        try {
            const respuesta = await fetch(`/api/instalacion/${accion}`, {
                method: "POST",
                credentials: "omit",
                cache: "no-store",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(datos)
            });
            const resultado = await respuesta.json();
            if (!respuesta.ok) {
                probada = false;
                if (respuesta.status === 409) terminar();
                mensaje(resultado.detalle || resultado.detail || "No se pudo completar la conexión.", true, resultado.traceId);
                return;
            }
            if (accion === "guardar") {
                mensaje("Conexión guardada.");
                await iniciar();
            } else {
                probada = true;
                mensaje("Conexión correcta.");
            }
        } catch {
            probada = false;
            mensaje("Sin respuesta del servidor. Vuelve a intentar.", true);
        } finally {
            datos.contrasena = "";
            ocupado = false;
            campos.disabled = false;
            guardar.disabled = !probada || cerrada;
            porId("probar").textContent = "Probar conexión";
        }
    }

    formulario.addEventListener("submit", evento => { evento.preventDefault(); enviar("probar"); });
    porId("autenticacion").addEventListener("change", () => {
        const windows = porId("autenticacion").value === "windows";
        porId("grupo-usuario").hidden = windows;
        porId("grupo-contrasena").hidden = windows;
        for (const id of ["usuario", "contrasena"]) {
            porId(id).required = !windows;
            porId(id).disabled = windows;
            if (windows) porId(id).value = "";
        }
        porId("ayuda-autenticacion").textContent = windows
            ? "Usa la cuenta de Windows del servidor."
            : "";
        probada = false;
        guardar.disabled = true;
    });
    guardar.addEventListener("click", () => { if (probada) enviar("guardar"); });
    formulario.addEventListener("input", () => {
        if (probada) {
            probada = false;
            guardar.disabled = true;
            mensaje("Vuelve a probar la conexión.");
        }
    });
    document.querySelectorAll("[data-campo]").forEach(boton => boton.addEventListener("click", () => {
        const campo = porId(boton.dataset.campo);
        const mostrar = campo.type === "password";
        campo.type = mostrar ? "text" : "password";
        boton.textContent = mostrar ? "Ocultar" : "Mostrar";
        boton.setAttribute("aria-label", `${mostrar ? "Ocultar" : "Mostrar"} contraseña SQL`);
    }));

    async function iniciar() {
        try {
            const respuesta = await fetch("/api/instalacion/estado", { cache: "no-store", credentials: "omit" });
            if (!respuesta.ok) throw new Error();
            const estado = await respuesta.json();
            if (estado.conexionGuardada) {
                terminar(estado.errorCarga || undefined);
                if (estado.errorCarga) {
                    porId("titulo-finalizado").textContent = "Revisa la instalación";
                    mensaje(estado.errorCarga, true);
                } else if (estado.operadorPendiente) {
                    porId("finalizado").hidden = true;
                    formularioOperador.hidden = false;
                    porId("paso-guardar").setAttribute("aria-current", "step");
                    porId("paso-listo").removeAttribute("aria-current");
                    document.querySelector("h1").textContent = "Crea el operador principal";
                } else if (estado.operadorCreado) {
                    porId("titulo-finalizado").textContent = "Instalación lista";
                    porId("detalle-finalizado").textContent = estado.principalExistente
                        ? ""
                        : "Revisa el operador principal.";
                    porId("paso-guardar").removeAttribute("aria-current");
                    porId("paso-listo").setAttribute("aria-current", "step");
                }
                porId("lista-pendientes").replaceChildren();
                porId("detalle-arranque").textContent = estado.configuracionPendiente.length
                    ? "Completa la configuración pendiente."
                    : "";
                porId("pendientes").hidden = estado.configuracionPendiente.length === 0;
                if (estado.configuracionPendiente.length) {
                    porId("pendientes").hidden = false;
                    for (const clave of estado.configuracionPendiente) {
                        const item = document.createElement("li");
                        item.textContent = clave;
                        porId("lista-pendientes").appendChild(item);
                    }
                }
                if (!estado.errorCarga && estado.principalExistente && !estado.configuracionPendiente.length) {
                    porId("confirmar-acceso").hidden = false;
                    await abrirAcceso();
                }
            } else formulario.hidden = false;
        } catch {
            mensaje("No se pudo consultar la instalación. Recarga la página.", true);
        } finally { porId("cargando").hidden = true; }
    }
    formularioOperador.addEventListener("submit", async evento => {
        evento.preventDefault();
        if (ocupado || !formularioOperador.reportValidity()) return;
        if (porId("contrasena-operador").value !== porId("confirmacion-operador").value) {
            mensaje("Las contraseñas no coinciden.", true);
            porId("confirmacion-operador").focus();
            return;
        }
        ocupado = true;
        const datos = {
            nombre: porId("nombre-operador").value, correo: porId("correo-operador").value,
            contrasena: porId("contrasena-operador").value, confirmacion: porId("confirmacion-operador").value
        };
        porId("campos-operador").disabled = true;
        porId("crear-operador").textContent = "Creando…";
        try {
            const respuesta = await fetch("/api/instalacion/operador", {
                method: "POST", credentials: "omit", cache: "no-store",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(datos)
            });
            const resultado = await respuesta.json();
            if (!respuesta.ok) {
                mensaje(resultado.detalle || resultado.detail || "No se pudo crear el operador.", true, resultado.traceId);
                if (respuesta.status === 409) {
                    formularioOperador.reset();
                    formularioOperador.hidden = true;
                    await iniciar();
                }
                return;
            }
            formularioOperador.reset();
            formularioOperador.hidden = true;
            await iniciar();
        } catch {
            mensaje("No se pudo confirmar el alta. Recarga la página.", true);
        } finally {
            datos.contrasena = "";
            datos.confirmacion = "";
            porId("contrasena-operador").value = "";
            porId("confirmacion-operador").value = "";
            porId("campos-operador").disabled = false;
            porId("crear-operador").textContent = "Crear operador principal";
            ocupado = false;
        }
    });
    iniciar();
})();
