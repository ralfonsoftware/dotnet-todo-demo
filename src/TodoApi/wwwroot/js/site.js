(() => {
    'use strict';

    const root = document.documentElement;
    const toggle = document.querySelector('[data-theme-toggle]');

    const syncToggle = () => {
        if (!toggle) {
            return;
        }

        const dark = root.dataset.theme === 'dark';
        toggle.setAttribute('aria-label', dark ? 'Switch to light theme' : 'Switch to dark theme');
        toggle.setAttribute('aria-pressed', dark ? 'true' : 'false');
    };

    syncToggle();

    toggle?.addEventListener('click', () => {
        const next = root.dataset.theme === 'dark' ? 'light' : 'dark';
        root.dataset.theme = next;

        try {
            localStorage.setItem('todo-theme', next);
        } catch {
            /* Storage can be unavailable in private contexts. */
        }

        syncToggle();
    });

    document.addEventListener('submit', (event) => {
        const form = event.target;

        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const message = form.dataset.confirm;
        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key !== 'n' && event.key !== 'N') {
            return;
        }

        if (event.metaKey || event.ctrlKey || event.altKey) {
            return;
        }

        const target = event.target;
        if (target instanceof Element && target.closest('input, textarea, select, [contenteditable="true"]')) {
            return;
        }

        const input = document.getElementById('NewTitle');
        if (!input) {
            return;
        }

        event.preventDefault();
        input.focus();
    });
})();
