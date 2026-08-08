// ── Domain.Client.Modules.Blazor/Shared/VirtualList.razor.js ──
//
// Die GESAMTE JS-Oberfläche der Virtualisierung. ~30 Zeilen, rein lesend
// (scrollTop / clientHeight) plus zwei Setter (scrollToPx / adjustBy).
// Kein DOM-Bau, kein Rendering — das macht Blazor.

export function init(dotnetRef, viewport) {
    let ticking = false;

    // Scroll/Resize gedrosselt an .NET melden (max. 1× pro Frame).
    const melde = () => {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(() => {
            dotnetRef.invokeMethodAsync('OnViewport', viewport.scrollTop, viewport.clientHeight);
            ticking = false;
        });
    };

    viewport.addEventListener('scroll', melde, { passive: true });
    const ro = new ResizeObserver(melde);
    ro.observe(viewport);

    melde(); // initialer Bereich, sobald gemountet

    return {
        // View-Operationen — die Datenstruktur kennt weder scrollTop noch Pixel.
        scrollToPx: (px) => { viewport.scrollTop = px; },
        adjustBy:   (dpx) => { viewport.scrollTop += dpx; },
        report:     () => melde(),   // frischen Viewport-Report anstoßen (Erst-Population)
        dispose: () => {
            viewport.removeEventListener('scroll', melde);
            ro.disconnect();
        }
    };
}
