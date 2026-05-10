/**
 * SyncTrigger - handles auto-sync when connection is restored
 * Listens for connectionRestored event and syncs queued orders
 */
(function () {
    'use strict';

    const SYNC_ENDPOINT = '/api/sync/orders';
    const SYNC_DEBOUNCE_MS = 2000; // Wait 2s after restore before syncing
    let syncDebounceTimer = null;
    let isSyncing = false;
    let syncLog = [];

    /**
     * Log sync activity
     */
    function log(message, type) {
        const entry = { time: new Date().toISOString(), message, type };
        syncLog.push(entry);
        console.log('[SyncTrigger]', entry.time, message);
        showSyncIndicator(type, message);
    }

    /**
     * Show sync status indicator
     */
    function showSyncIndicator(type, message) {
        // Remove existing indicator
        const existing = document.getElementById('sync-indicator');
        if (existing) existing.remove();

        const indicator = document.createElement('div');
        indicator.id = 'sync-indicator';
        indicator.className = 'sync-indicator';

        switch (type) {
            case 'syncing':
                indicator.classList.add('sync-syncing');
                indicator.innerHTML = '&#8635; Синхронизация...';
                break;
            case 'success':
                indicator.classList.add('sync-success');
                indicator.innerHTML = '&#10003; Синхронизировано';
                break;
            case 'error':
                indicator.classList.add('sync-error');
                indicator.innerHTML = '&#10007; Ошибка синхронизации';
                break;
            case 'info':
                indicator.classList.add('sync-syncing');
                indicator.textContent = message;
                break;
        }

        document.body.appendChild(indicator);

        // Auto-hide success/info after 3s
        if (type === 'success' || type === 'info') {
            setTimeout(() => {
                if (document.getElementById('sync-indicator')?.classList.contains('sync-' + type)) {
                    indicator.remove();
                }
            }, 3000);
        }
    }

    /**
     * Perform sync of all queued orders
     */
    async function performSync() {
        if (isSyncing) return;
        if (!window.FallbackStorage) {
            log('FallbackStorage not available', 'error');
            return;
        }

        const queue = window.FallbackStorage.getQueue();
        if (!queue || queue.length === 0) {
            log('Queue is empty, nothing to sync', 'info');
            return;
        }

        isSyncing = true;
        log(`Синхронизация ${queue.length} заказов...`, 'syncing');

        try {
            const response = await fetch(SYNC_ENDPOINT, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(queue)
            });

            if (!response.ok) {
                throw new Error('Sync request failed: ' + response.status);
            }

            const result = await response.json();

            if (result.success) {
                // Mark all synced items as processed
                for (const item of queue) {
                    window.FallbackStorage.updateQueueItem(item.id, 'synced');
                }

                // Clear fully synced items from queue
                for (const item of queue) {
                    window.FallbackStorage.removeQueueItem(item.id);
                }

                log(`Синхронизировано: ${result.successful} из ${result.total}`, 'success');

                // Dispatch event for other components
                window.dispatchEvent(new CustomEvent('syncCompleted', { detail: result }));
            } else {
                // Partial success - mark individual items
                for (const r of result.results || []) {
                    if (r.success) {
                        window.FallbackStorage.updateQueueItem(r.tempId, 'synced', r.orderId);
                    } else {
                        window.FallbackStorage.updateQueueItem(r.tempId, 'failed');
                    }
                }
                log(`Ошибки синхронизации: ${result.errors?.join(', ')}`, 'error');
            }
        } catch (e) {
            log('Синхронизация не удалась: ' + e.message, 'error');
            console.error('[SyncTrigger] Sync error:', e);
        } finally {
            isSyncing = false;
        }
    }

    /**
     * Handle connection restored event
     */
    function onConnectionRestored() {
        // Debounce to avoid sync during unstable connection
        if (syncDebounceTimer) {
            clearTimeout(syncDebounceTimer);
        }

        log('Соединение восстановлено, синхронизация через 2 сек...', 'info');

        syncDebounceTimer = setTimeout(() => {
            performSync();
        }, SYNC_DEBOUNCE_MS);
    }

    /**
     * Handle connection status change
     */
    function onStatusChanged(event) {
        const { status } = event.detail;
        if (status === 'online') {
            onConnectionRestored();
        }
    }

    /**
     * Force manual sync (e.g., button click)
     */
    function manualSync() {
        syncDebounceTimer = 0; // Skip debounce
        onConnectionRestored();
    }

    /**
     * Get sync log
     */
    function getSyncLog() {
        return syncLog;
    }

    /**
     * Get pending queue count
     */
    function getPendingCount() {
        if (!window.FallbackStorage) return 0;
        return window.FallbackStorage.getQueue().length;
    }

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        // Listen for connection restore
        window.addEventListener('connectionRestored', onConnectionRestored);
        window.addEventListener('connectionStatusChanged', onStatusChanged);
    });

    // Export API
    window.SyncTrigger = {
        sync: performSync,
        manualSync: manualSync,
        getLog: getSyncLog,
        getPendingCount: getPendingCount
    };
})();
