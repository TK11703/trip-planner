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

    // Slots per day is fixed by the component (24h * 60m / 30m slot = 48). The slot width
    // is exposed as the CSS custom property --ttl-slot-w so the day width stays in sync
    // with the component's SlotWidthPx constant.
    const SLOTS_PER_DAY = 48;

    // Reports the day whose column contains the horizontal center of the visible track back
    // to the Blazor component. Returns nothing; guards against missing layout.
    function reportCenteredDay(scrollEl) {
        const state = scrollEl.__ttlSticky;
        if (!state || !state.dotNetRef) return;
        // Suppress reporting while a programmatic (jump/step/shortcut) scroll is settling so
        // the jumped-to active date is not overridden by the centered day of the resting view.
        if (state.suppressUntil && Date.now() < state.suppressUntil) return;
        const root = scrollEl.closest('.ttl');
        if (!root) return;
        const slotW = parseFloat(getComputedStyle(root).getPropertyValue('--ttl-slot-w')) || 0;
        const labelW = parseFloat(getComputedStyle(root).getPropertyValue('--ttl-label-w')) || 0;
        const dayWidth = slotW * SLOTS_PER_DAY;
        if (dayWidth <= 0) return;
        const dayCount = scrollEl.querySelectorAll('.ttl-day').length;
        if (dayCount <= 0) return;
        const centerTrackX = scrollEl.scrollLeft + (scrollEl.clientWidth - labelW) / 2;
        let dayIndex = Math.floor(centerTrackX / dayWidth);
        dayIndex = Math.min(Math.max(dayIndex, 0), dayCount - 1);
        if (dayIndex === state.lastIndex) return;
        state.lastIndex = dayIndex;
        try {
            state.dotNetRef.invokeMethodAsync('SetCenteredDayIndex', dayIndex);
        } catch (e) {
            // Circuit may be disconnected; ignore.
        }
    }

    const tripTimeline = {
        init: function (scrollEl, dotNetRef) {
            if (!scrollEl) return;
            const state = scrollEl.__ttlSticky || (scrollEl.__ttlSticky = {});
            if (state.onResize) window.removeEventListener('resize', state.onResize);
            if (state.onScroll) scrollEl.removeEventListener('scroll', state.onScroll);

            state.dotNetRef = dotNetRef || state.dotNetRef || null;
            if (state.lastIndex === undefined) state.lastIndex = -1;

            const run = function () { apply(scrollEl); };
            state.onResize = run;
            window.addEventListener('resize', run);

            // Debounced (trailing) scroll listener: compute the centered day only after
            // scrolling settles, and report it to .NET only when the day changes.
            if (state.dotNetRef) {
                const onScroll = function () {
                    if (state.scrollTimer) clearTimeout(state.scrollTimer);
                    state.scrollTimer = setTimeout(function () { reportCenteredDay(scrollEl); }, 120);
                };
                state.onScroll = onScroll;
                scrollEl.addEventListener('scroll', onScroll, { passive: true });
            }

            // Defer one frame so the grid has laid out before we measure label widths.
            window.requestAnimationFrame(run);
        },
        // Scrolls the timeline horizontally so a chosen day's left edge aligns to the
        // start of the scroll track. offsetX is the target scrollLeft in pixels.
        scrollToDate: function (scrollEl, offsetX) {
            if (!scrollEl) return;
            const state = scrollEl.__ttlSticky || (scrollEl.__ttlSticky = {});
            // The component has already set the active date to the jumped target; suppress
            // scroll-driven centered-day reports until this programmatic scroll settles.
            state.suppressUntil = Date.now() + 1000;
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
            if (state.onScroll) scrollEl.removeEventListener('scroll', state.onScroll);
            if (state.scrollTimer) clearTimeout(state.scrollTimer);
            state.dotNetRef = null;
            delete scrollEl.__ttlSticky;
        }
    };

    window.tripTimeline = tripTimeline;
})();
