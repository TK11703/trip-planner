(() => {
    const storageKey = 'trip-planner.theme-mode';
    const sourceKey = 'trip-planner.theme-source';

    function browserMode() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function normalize(mode) {
        return mode === 'dark' ? 'dark' : 'light';
    }

    function apply(mode, source) {
        const normalized = normalize(mode);
        document.documentElement.dataset.bsTheme = normalized;
        document.documentElement.dataset.themeMode = normalized;
        document.documentElement.dataset.themeSource = source || 'deviceBrowser';
        const meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', normalized === 'dark' ? '#0f1720' : '#f6f1e8');
        return normalized;
    }

    window.tripPlannerTheme = {
        applyInitialTheme: (preferredMode) => apply(preferredMode || localStorage.getItem(storageKey) || browserMode(), localStorage.getItem(sourceKey) || 'deviceBrowser'),
        applyTheme: (mode, source, persistForVisitor) => {
            const applied = apply(mode, source);
            if (persistForVisitor) {
                localStorage.setItem(storageKey, applied);
                localStorage.setItem(sourceKey, source || 'temporaryVisitorChoice');
            } else if (source === 'accountPreference') {
                localStorage.removeItem(storageKey);
                localStorage.setItem(sourceKey, source);
            }
            return applied;
        },
        getAppliedMode: () => document.documentElement.dataset.themeMode || browserMode(),
        getBrowserMode: browserMode
    };

    window.tripPlannerTheme.applyInitialTheme();
})();
