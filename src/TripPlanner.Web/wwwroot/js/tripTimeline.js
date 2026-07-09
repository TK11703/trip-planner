// The trip timeline is a first-party Blazor component (Components/Timeline/TripTimeline.razor).
// It renders a single horizontally scrollable resource grid using CSS sticky headers and a sticky
// leg label column. The "running" day header is achieved with native CSS position: sticky on each
// day label, which the browser handles on the compositor (perfectly smooth, no scroll jitter):
//   1. A day entering from the right stays LEFT-aligned in its cell until its label reaches center.
//   2. While the day spans the viewport center, sticky pins the label at the center.
//   3. Once the day is pushed past center, sticky clamps the label to the cell's right edge and it
//      scrolls away RIGHT-aligned.
// JavaScript only computes each label's sticky `left` target (which depends on the viewport width
// and the label's own text width) on init and on resize — never on scroll.
(function () {
    function apply(scrollEl) {
        const root = scrollEl.closest('.ttl');
        if (!root) return;
        const labelW = parseFloat(getComputedStyle(root).getPropertyValue('--ttl-label-w')) || 0;
        // Center of the visible area, excluding the sticky leg-label column, in scrollport coords.
        const centerX = labelW + (scrollEl.clientWidth - labelW) / 2;
        const days = scrollEl.querySelectorAll('.ttl-day');
        for (let i = 0; i < days.length; i++) {
            const label = days[i].firstElementChild;
            if (!label) continue;
            const textWidth = label.offsetWidth;
            label.style.left = Math.round(centerX - textWidth / 2) + 'px';
        }
    }

    const tripTimeline = {
        init: function (scrollEl) {
            if (!scrollEl) return;
            const state = scrollEl.__ttlSticky || (scrollEl.__ttlSticky = {});
            if (state.onResize) window.removeEventListener('resize', state.onResize);

            const run = function () { apply(scrollEl); };
            state.onResize = run;
            window.addEventListener('resize', run);
            // Defer one frame so the grid has laid out before we measure label widths.
            window.requestAnimationFrame(run);
        },
        // Scrolls the timeline horizontally so a chosen day's left edge aligns to the
        // start of the scroll track. offsetX is the target scrollLeft in pixels.
        scrollToDate: function (scrollEl, offsetX) {
            if (!scrollEl) return;
            const max = Math.max(0, scrollEl.scrollWidth - scrollEl.clientWidth);
            const left = Math.min(Math.max(offsetX || 0, 0), max);
            const reduceMotion = window.matchMedia
                && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
            scrollEl.scrollTo({ left: left, behavior: reduceMotion ? 'auto' : 'smooth' });
        },
        dispose: function (scrollEl) {
            if (!scrollEl || !scrollEl.__ttlSticky) return;
            const state = scrollEl.__ttlSticky;
            if (state.onResize) window.removeEventListener('resize', state.onResize);
            delete scrollEl.__ttlSticky;
        }
    };

    window.tripTimeline = tripTimeline;
})();
