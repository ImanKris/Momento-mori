/**
 * RoboStore Theme System
 * Handles light/dark theme switching with localStorage persistence
 */
(function () {
    'use strict';

    const THEME_KEY = 'robostore_theme';
    const THEME_ATTR = 'data-theme';
    const TRANSITION_CLASS = 'theme-transition';

    // Available themes
    const THEMES = {
        LIGHT: 'light',
        DARK: 'dark'
    };

    // Get stored theme or system preference
    function getPreferredTheme() {
        const stored = localStorage.getItem(THEME_KEY);
        if (stored && (stored === THEMES.LIGHT || stored === THEMES.DARK)) {
            return stored;
        }
        // Check system preference
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
            return THEMES.DARK;
        }
        return THEMES.LIGHT;
    }

    // Apply theme to document
    function applyTheme(theme, skipTransition = false) {
        if (!theme || !THEMES[theme.toUpperCase()]) {
            theme = THEMES.LIGHT;
        }

        const html = document.documentElement;

        if (!skipTransition) {
            html.classList.add(TRANSITION_CLASS);
        }

        html.setAttribute(THEME_ATTR, theme);

        if (!skipTransition) {
            // Remove transition class after animation
            setTimeout(() => {
                html.classList.remove(TRANSITION_CLASS);
            }, 300);
        }

        // Update theme toggle button
        updateToggleButton(theme);

        // Store preference
        localStorage.setItem(THEME_KEY, theme);

        // Dispatch event for other components
        window.dispatchEvent(new CustomEvent('themeChanged', { detail: { theme } }));
    }

    // Toggle between themes
    function toggleTheme() {
        const current = document.documentElement.getAttribute(THEME_ATTR) || THEMES.LIGHT;
        const next = current === THEMES.LIGHT ? THEMES.DARK : THEMES.LIGHT;
        applyTheme(next);
    }

    // Update toggle button icon based on theme
    function updateToggleButton(theme) {
        const btn = document.querySelector('.theme-toggle');
        if (btn) {
            btn.textContent = theme === THEMES.DARK ? '☀️' : '🌙';
            btn.setAttribute('aria-label', theme === THEMES.DARK ? 'Switch to light theme' : 'Switch to dark theme');
        }
    }

    // Initialize theme on page load
    function init() {
        const theme = getPreferredTheme();
        applyTheme(theme, true);

        // Set up toggle buttons
        document.querySelectorAll('.theme-toggle').forEach(btn => {
            btn.addEventListener('click', toggleTheme);
            btn.setAttribute('role', 'button');
            btn.setAttribute('tabindex', '0');
            btn.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    toggleTheme();
                }
            });
        });

        // Listen for system theme changes
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
                // Only auto-switch if user hasn't set a preference
                if (!localStorage.getItem(THEME_KEY)) {
                    applyTheme(e.matches ? THEMES.DARK : THEMES.LIGHT);
                }
            });
        }
    }

    // Export API
    window.ThemeSystem = {
        init: init,
        toggle: toggleTheme,
        set: (theme) => applyTheme(theme),
        get: () => document.documentElement.getAttribute(THEME_ATTR) || THEMES.LIGHT,
        THEMES: THEMES
    };

    // Auto-init when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
