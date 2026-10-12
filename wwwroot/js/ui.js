// Scorciatoie e piccoli aiuti dell'interfaccia: ricerca rapida (Ctrl+K), modalità privacy (F2).
window.DPUi = (function () {
    let ref = null;

    function init(dotnetRef) {
        ref = dotnetRef;
        document.addEventListener('keydown', e => {
            if (!ref) return;
            if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                ref.invokeMethodAsync('OpenPalette');
            } else if (e.key === 'F2') {
                e.preventDefault();
                ref.invokeMethodAsync('TogglePrivacy');
            }
        }, true);
    }

    function setPrivacy(on) {
        document.body.classList.toggle('privacy', on);
    }

    // Cambia la lingua dell'interfaccia: attributo lang della pagina e formati dei grafici (strings = parole fisse dei grafici già tradotte).
    function setLanguage(culture, strings) {
        document.documentElement.lang = (culture || 'it-IT').split('-')[0];
        if (window.DPCharts) window.DPCharts.setLocale(culture, strings);
    }

    function focus(id) {
        const el = document.getElementById(id);
        if (el) { el.focus(); if (el.select) el.select(); }
    }

    function keepVisible(selector) {
        const el = document.querySelector(selector);
        if (el) el.scrollIntoView({ block: 'nearest' });
    }

    return { init, setPrivacy, setLanguage, focus, keepVisible };
})();
