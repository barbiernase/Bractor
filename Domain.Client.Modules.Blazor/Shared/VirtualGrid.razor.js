// ── Domain.Client.Modules.Blazor/Shared/VirtualGrid.razor.js ──
//
// Wie VirtualList.razor.js, aber meldet zusätzlich die Viewport-BREITE: das Grid leitet
// Zellbreite (= Breite / Spalten) und daraus die Zellhöhe (= Zellbreite / Seitenverhältnis)
// ab. Rein lesend (scrollTop / clientHeight / clientWidth) plus zwei View-Setter.

export function init(dotnetRef, viewport) {
    let ticking = false;

    const melde = () => {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(() => {
            dotnetRef.invokeMethodAsync(
                'OnViewport', viewport.scrollTop, viewport.clientHeight, viewport.clientWidth);
            ticking = false;
        });
    };

    viewport.addEventListener('scroll', melde, { passive: true });
    const ro = new ResizeObserver(melde);
    ro.observe(viewport);

    melde(); // initialer Bereich, sobald gemountet

    return {
        scrollToPx: (px) => { viewport.scrollTop = px; },
        report:     () => melde(),
        dispose: () => {
            viewport.removeEventListener('scroll', melde);
            ro.disconnect();
        }
    };
}
