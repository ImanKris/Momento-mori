/**
 * ConnectionStatus - monitors DB availability and manages UI status indicator
 */
(function () {
    'use strict';

    const STATUS_KEY = 'rs_connection_status';
    const HEALTH_ENDPOINT = '/health/db';
    const DEFAULT_POLL_INTERVAL = 10000; // 10 seconds
    const FAST_POLL_INTERVAL = 3000; // 3 seconds when recovering
    const MAX_RETRIES_BEFORE_FALLBACK = 3;

    let currentStatus = 'unknown'; // 'online' | 'fallback' | 'error' | 'unknown'
    let pollInterval = null;
    let consecutiveFailures = 0;
    let isRetrying = false;

    /**
     * Get status display info
     */
    function getStatusInfo(status) {
        switch (status) {
            case 'online':
                return {
                    class: 'status-online',
                    icon: '●',
                    text: 'Онлайн',
                    title: 'Подключение к базе данных активно'
                };
            case 'fallback':
                return {
                    class: 'status-fallback',
                    icon: '◐',
                    text: 'Автономный режим',
                    title: 'Нет связи с сервером. Данные сохраняются локально.'
                };
            case 'error':
                return {
                    class: 'status-error',
                    icon: '✕',
                    text: 'Ошибка',
                    title: 'Критическая ошибка подключения'
                };
            default:
                return {
                    class: 'status-unknown',
                    icon: '?',
                    text: 'Проверка...',
                    title: 'Проверка подключения...'
                };
        }
    }

    /**
     * Update UI status indicator
     */
    function updateUI(status) {
        const info = getStatusInfo(status);
        const indicator = document.getElementById('connection-status');
        if (!indicator) return;

        indicator.className = 'connection-status ' + info.class;
        indicator.title = info.title;

        const iconEl = indicator.querySelector('.status-icon');
        const textEl = indicator.querySelector('.status-text');

        if (iconEl) iconEl.textContent = info.icon;
        if (textEl) textEl.textContent = info.text;

        // Also update navbar status if exists
        const navStatus = document.getElementById('nav-connection-status');
        if (navStatus) {
            navStatus.className = 'navbar-status ' + info.class;
            navStatus.textContent = info.icon + ' ' + info.text;
        }

        // Persist status
        try {
            localStorage.setItem(STATUS_KEY, status);
        } catch (e) { /* ignore */ }
    }

    /**
     * Get saved status from localStorage
     */
    function getSavedStatus() {
        try {
            return localStorage.getItem(STATUS_KEY);
        } catch (e) {
            return null;
        }
    }

    /**
     * Check DB health via endpoint
     */
    async function checkHealth() {
        try {
            const controller = new AbortController();
            const timeoutId = setTimeout(() => controller.abort(), 5000);

            const response = await fetch(HEALTH_ENDPOINT, {
                method: 'GET',
                signal: controller.signal,
                cache: 'no-store'
            });

            clearTimeout(timeoutId);

            if (response.ok) {
                const data = await response.json().catch(() => ({}));
                return { ok: true, data };
            } else {
                return { ok: false, status: response.status };
            }
        } catch (e) {
            return { ok: false, error: e.message };
        }
    }

    /**
     * Determine new status based on health check result
     */
    function determineStatus(healthResult, previousStatus) {
        if (healthResult.ok) {
            consecutiveFailures = 0;
            return 'online';
        }

        consecutiveFailures++;

        if (consecutiveFailures >= MAX_RETRIES_BEFORE_FALLBACK) {
            return previousStatus === 'online' ? 'fallback' : previousStatus;
        }

        // Temporary failure, keep current status but flag retry
        return previousStatus;
    }

    /**
     * Main poll loop
     */
    async function poll() {
        const previousStatus = currentStatus;
        const healthResult = await checkHealth();
        const newStatus = determineStatus(healthResult, previousStatus);

        if (newStatus !== currentStatus) {
            currentStatus = newStatus;
            updateUI(currentStatus);
            onStatusChange(currentStatus, healthResult);
        }

        // Adjust poll speed based on state
        if (currentStatus === 'online') {
            setPollInterval(DEFAULT_POLL_INTERVAL);
            isRetrying = false;
        } else if (currentStatus === 'fallback' && !isRetrying) {
            // Faster polling when trying to recover
            setPollInterval(FAST_POLL_INTERVAL);
            isRetrying = true;
        }
    }

    /**
     * Called when status changes - override for custom behavior
     */
    function onStatusChange(newStatus, healthResult) {
        // Dispatch custom event for other scripts
        window.dispatchEvent(new CustomEvent('connectionStatusChanged', {
            detail: { status: newStatus, health: healthResult }
        }));

        // If came back online, trigger sync
        if (newStatus === 'online' && isRetrying) {
            window.dispatchEvent(new CustomEvent('connectionRestored'));
        }
    }

    /**
     * Set poll interval
     */
    function setPollInterval(interval) {
        if (pollInterval) {
            clearInterval(pollInterval);
        }
        pollInterval = setInterval(poll, interval);
    }

    /**
     * Start monitoring
     */
    function start() {
        // Initialize with saved or unknown status
        const saved = getSavedStatus();
        currentStatus = saved || 'unknown';
        updateUI(currentStatus);

        // Initial poll
        poll();

        // Start regular polling
        setPollInterval(DEFAULT_POLL_INTERVAL);

        // Listen for manual retry requests
        document.addEventListener('retryConnection', () => {
            consecutiveFailures = 0;
            poll();
        });
    }

    /**
     * Get current status
     */
    function getStatus() {
        return currentStatus;
    }

    /**
     * Force refresh status
     */
    async function refresh() {
        consecutiveFailures = 0;
        await poll();
    }

    /**
     * Stop monitoring
     */
    function stop() {
        if (pollInterval) {
            clearInterval(pollInterval);
            pollInterval = null;
        }
    }

    // Export API
    window.ConnectionStatus = {
        start: start,
        stop: stop,
        getStatus: getStatus,
        refresh: refresh,
        STATUS: { ONLINE: 'online', FALLBACK: 'fallback', ERROR: 'error', UNKNOWN: 'unknown' }
    };
})();
