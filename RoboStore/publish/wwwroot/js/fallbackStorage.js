/**
 * FallbackStorage - localStorage wrapper with safety and queue support
 * Used when DB is unavailable
 */
(function () {
    'use strict';

    const MAX_STORAGE_MB = 5;
    const MAX_STORAGE_BYTES = MAX_STORAGE_MB * 1024 * 1024;
    const STORAGE_KEY_PREFIX = 'rs_';
    const QUEUE_KEY = STORAGE_KEY_PREFIX + 'queue';

    /**
     * Safely serialize data to JSON
     */
    function safeSerialize(data) {
        try {
            return JSON.stringify(data);
        } catch (e) {
            console.error('[FallbackStorage] Serialization error:', e);
            return null;
        }
    }

    /**
     * Safely deserialize JSON to data
     */
    function safeDeserialize(json) {
        if (!json || typeof json !== 'string') return null;
        try {
            return JSON.parse(json);
        } catch (e) {
            console.error('[FallbackStorage] Deserialization error:', e);
            return null;
        }
    }

    /**
     * Estimate current storage usage
     */
    function getStorageUsageEstimate() {
        let totalBytes = 0;
        try {
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.startsWith(STORAGE_KEY_PREFIX)) {
                    const value = localStorage.getItem(key);
                    if (value) {
                        totalBytes += (key.length + value.length) * 2; // UTF-16
                    }
                }
            }
        } catch (e) {
            console.error('[FallbackStorage] Storage estimate error:', e);
        }
        return {
            usedBytes: totalBytes,
            usedMB: (totalBytes / (1024 * 1024)).toFixed(2),
            maxMB: MAX_STORAGE_MB,
            percentUsed: ((totalBytes / MAX_STORAGE_BYTES) * 100).toFixed(1)
        };
    }

    /**
     * Check if localStorage is available
     */
    function isAvailable() {
        try {
            const testKey = STORAGE_KEY_PREFIX + 'test';
            localStorage.setItem(testKey, '1');
            localStorage.removeItem(testKey);
            return true;
        } catch (e) {
            return false;
        }
    }

    /**
     * Save data to localStorage
     */
    function saveData(key, data) {
        if (!isAvailable()) {
            console.warn('[FallbackStorage] localStorage not available');
            return false;
        }
        const storageKey = STORAGE_KEY_PREFIX + key;
        const serialized = safeSerialize(data);
        if (!serialized) return false;

        try {
            // Check size limit
            const estimate = getStorageUsageEstimate();
            if (estimate.usedBytes + serialized.length * 2 > MAX_STORAGE_BYTES) {
                console.error('[FallbackStorage] Storage limit exceeded');
                return false;
            }
            localStorage.setItem(storageKey, serialized);
            return true;
        } catch (e) {
            console.error('[FallbackStorage] saveData error:', e);
            return false;
        }
    }

    /**
     * Load data from localStorage
     */
    function loadData(key) {
        if (!isAvailable()) return null;
        const storageKey = STORAGE_KEY_PREFIX + key;
        try {
            const value = localStorage.getItem(storageKey);
            return safeDeserialize(value);
        } catch (e) {
            console.error('[FallbackStorage] loadData error:', e);
            return null;
        }
    }

    /**
     * Clear specific key from localStorage
     */
    function clearData(key) {
        if (!isAvailable()) return false;
        const storageKey = STORAGE_KEY_PREFIX + key;
        try {
            localStorage.removeItem(storageKey);
            return true;
        } catch (e) {
            console.error('[FallbackStorage] clearData error:', e);
            return false;
        }
    }

    /**
     * Get all keys managed by FallbackStorage
     */
    function getAllKeys() {
        if (!isAvailable()) return [];
        const keys = [];
        try {
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.startsWith(STORAGE_KEY_PREFIX)) {
                    keys.push(key.replace(STORAGE_KEY_PREFIX, ''));
                }
            }
        } catch (e) {
            console.error('[FallbackStorage] getAllKeys error:', e);
        }
        return keys;
    }

    // ============ QUEUE OPERATIONS ============

    /**
     * Add item to sync queue
     */
    function addToQueue(item) {
        const queue = getQueue() || [];
        // Generate temporary ID for tracking
        const queueItem = {
            id: 'temp_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9),
            addedAt: new Date().toISOString(),
            type: item.type || 'unknown',
            data: item.data || item,
            status: 'pending'
        };
        queue.push(queueItem);
        return saveData(QUEUE_KEY, queue) ? queueItem.id : null;
    }

    /**
     * Get all queued items
     */
    function getQueue() {
        return loadData(QUEUE_KEY) || [];
    }

    /**
     * Remove specific item from queue
     */
    function removeQueueItem(id) {
        const queue = getQueue();
        const filtered = queue.filter(item => item.id !== id);
        if (filtered.length === queue.length) return false; // Not found
        return saveData(QUEUE_KEY, filtered);
    }

    /**
     * Update queue item status
     */
    function updateQueueItem(id, status, serverId) {
        const queue = getQueue();
        const item = queue.find(q => q.id === id);
        if (item) {
            item.status = status;
            if (serverId) item.serverId = serverId;
            if (status === 'synced') item.syncedAt = new Date().toISOString();
            return saveData(QUEUE_KEY, queue);
        }
        return false;
    }

    /**
     * Clear entire queue
     */
    function clearQueue() {
        return clearData(QUEUE_KEY);
    }

    // ============ CART-SPECIFIC HELPERS ============

    /**
     * Save cart to localStorage
     */
    function saveCart(cartArray) {
        return saveData('cart', cartArray);
    }

    /**
     * Load cart from localStorage
     */
    function loadCart() {
        return loadData('cart') || [];
    }

    /**
     * Save pending orders
     */
    function savePendingOrders(orders) {
        return saveData('pendingOrders', orders);
    }

    /**
     * Load pending orders
     */
    function loadPendingOrders() {
        return loadData('pendingOrders') || [];
    }

    // Export public API
    window.FallbackStorage = {
        saveData: saveData,
        loadData: loadData,
        clearData: clearData,
        getAllKeys: getAllKeys,
        addToQueue: addToQueue,
        getQueue: getQueue,
        removeQueueItem: removeQueueItem,
        updateQueueItem: updateQueueItem,
        clearQueue: clearQueue,
        getStorageUsageEstimate: getStorageUsageEstimate,
        isAvailable: isAvailable,
        saveCart: saveCart,
        loadCart: loadCart,
        savePendingOrders: savePendingOrders,
        loadPendingOrders: loadPendingOrders,
        STORAGE_KEY_PREFIX: STORAGE_KEY_PREFIX,
        MAX_STORAGE_MB: MAX_STORAGE_MB
    };
})();
