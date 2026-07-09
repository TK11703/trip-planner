(() => {
    const cookieKey = 'tp-theme';
    const legacyStorageKey = 'trip-planner.theme-mode';

    function browserMode() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function normalize(mode) {
        return mode === 'dark' ? 'dark' : 'light';
    }

    function readCookie(name) {
        const match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        return match ? decodeURIComponent(match[1]) : null;
    }

    function writeCookie(name, value) {
        // One year, root path, Lax so it is sent on top-level navigations and the
        // server can render the correct theme on the very first response.
        document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=31536000; SameSite=Lax`;
    }

    function persistedMode() {
        // Prefer a theme the server already applied to <html>, then the cookie,
        // then a legacy localStorage value, then the browser preference.
        return document.documentElement.dataset.themeMode
            || readCookie(cookieKey)
            || localStorage.getItem(legacyStorageKey)
            || browserMode();
    }

    function apply(mode, source) {
        const normalized = normalize(mode);
        document.documentElement.dataset.bsTheme = normalized;
        document.documentElement.dataset.themeMode = normalized;
        document.documentElement.dataset.themeSource = source || 'deviceBrowser';
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', normalized === 'dark' ? '#0a1017' : '#f3f7f9');
        return normalized;
    }

    window.tripPlannerTheme = {
        applyInitialTheme: (preferredMode) => apply(preferredMode || persistedMode(), readCookie(cookieKey) ? 'accountPreference' : 'deviceBrowser'),
        applyTheme: (mode, source, persistForVisitor) => {
            const applied = apply(mode, source);
            if (persistForVisitor || source === 'accountPreference') {
                // Persist to a server-readable cookie so the theme is rendered
                // server-side on the next load with no visible flip.
                writeCookie(cookieKey, applied);
                localStorage.setItem(legacyStorageKey, applied);
            }
            return applied;
        },
        getAppliedMode: () => document.documentElement.dataset.themeMode || persistedMode(),
        getBrowserMode: browserMode
    };

    window.tripPlannerTheme.applyInitialTheme();

    // Blazor enhanced navigation morphs the <html> element to match the
    // server response, which does not carry the theme attributes. Re-apply
    // the persisted theme after each enhanced page load so it is retained
    // when navigating between pages.
    function reapplyTheme() {
        window.tripPlannerTheme.applyInitialTheme();
    }

    function attachEnhancedNavigationHandler() {
        if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
            window.Blazor.addEventListener('enhancedload', reapplyTheme);
        } else {
            setTimeout(attachEnhancedNavigationHandler, 50);
        }
    }

    attachEnhancedNavigationHandler();
})();
