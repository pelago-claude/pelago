// Modalità assistita: segnala al pannello (Blazor) quale elemento con data-help è sotto il mouse.
window.DPAssist = (function () {
    let ref = null;
    let enabled = false;
    let timer = null;
    let lastKey = null;

    // Il contesto cambia solo se il mouse SI FERMA su un elemento: attraversare altri riquadri per raggiungere il pannello
    // (o per leggere una spiegazione lunga) non deve cambiare quello che si sta leggendo.
    const DWELL_MS = 400;

    function onOver(e) {
        if (!enabled || !ref) return;
        // sopra il pannello il contesto resta fermo: si annulla anche l'attesa in corso verso un riquadro appena sfiorato
        if (e.target.closest && e.target.closest('.assistant, .as-bubble')) { clearTimeout(timer); return; }
        const el = e.target.closest ? e.target.closest('[data-help]') : null;
        if (!el) { clearTimeout(timer); return; }
        const key = el.getAttribute('data-help');
        if (!key || key === lastKey) { clearTimeout(timer); return; }
        clearTimeout(timer);
        timer = setTimeout(() => {
            lastKey = key;
            ref.invokeMethodAsync('Hover', key);
        }, DWELL_MS);
    }

    // il mouse esce dalla finestra: niente cambi in sospeso
    function onLeaveWindow(e) {
        if (!e.relatedTarget) clearTimeout(timer);
    }

    function onKey(e) {
        if (e.key === 'F1' && ref) {
            e.preventDefault();
            ref.invokeMethodAsync('ToggleFromKey');
        }
    }

    function init(dotnetRef) {
        ref = dotnetRef;
        document.addEventListener('mouseover', onOver, true);
        document.addEventListener('mouseout', onLeaveWindow, true);
        document.addEventListener('keydown', onKey, true);
    }

    function setEnabled(value) {
        enabled = value;
        document.body.classList.toggle('assist-on', value);
        if (!value) {
            lastKey = null;
            clearTimeout(timer);
        }
    }

    function scrollToEnd(id) {
        const el = document.getElementById(id);
        if (el) el.scrollTop = el.scrollHeight;
    }

    function focusInput(id) {
        const el = document.getElementById(id);
        if (el) el.focus();
    }

    return { init, setEnabled, scrollToEnd, focusInput };
})();
