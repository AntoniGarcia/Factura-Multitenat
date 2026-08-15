// Blazor no tiene forma de escribir un atributo en <html> sin pasar por JS: el elemento
// raíz vive fuera de la raíz de componentes (#app). Esta es la única función que este
// archivo expone.
window.aplicarTema = function (tema) {
    document.documentElement.setAttribute('data-tema', tema);
};
