/**
 * logsSignalR - SignalR client for real-time log updates
 */
(function () {
    'use strict';

    const HUB_URL = '/hubs/logs';
    const MAX_LIVE_LOGS = 100;

    let connection = null;
    let isConnected = false;
    let logs = [];

    /**
     * Connect to SignalR hub
     */
    function connect() {
        if (typeof signalR === 'undefined') {
            console.warn('[logsSignalR] signalR library not found. Include signalr.js or use fallback polling.');
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(HUB_URL)
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveLog', function (entry) {
            addLogEntry(entry);
            updateLogTable();
        });

        connection.onclose(function () {
            isConnected = false;
            updateConnectionIndicator();
        });

        connection.onreconnected(function () {
            isConnected = true;
            updateConnectionIndicator();
        });

        connection.onreconnecting(function () {
            isConnected = false;
            updateConnectionIndicator();
        });

        // Start connection
        connection.start()
            .then(function () {
                isConnected = true;
                updateConnectionIndicator();
            })
            .catch(function (err) {
                console.error('[logsSignalR] Connection failed:', err);
                isConnected = false;
                updateConnectionIndicator();
            });
    }

    /**
     * Add a log entry to the live list
     */
    function addLogEntry(entry) {
        // Add CSS class based on action type
        entry.cssClass = getCssClass(entry.actionType);

        logs.unshift(entry);

        // Keep only MAX_LIVE_LOGS
        if (logs.length > MAX_LIVE_LOGS) {
            logs = logs.slice(0, MAX_LIVE_LOGS);
        }
    }

    /**
     * Get CSS class for action type
     */
    function getCssClass(actionType) {
        switch (actionType) {
            case 'ROBOT_CREATED':
            case 'ORDER_COMPLETED':
            case 'LOGIN_SUCCESS':
            case 'ORDER_SYNCED':
                return 'log-success';
            case 'ROBOT_UPDATED':
            case 'ORDER_STATUS_CHANGE':
            case 'USER_ROLE_CHANGED':
                return 'log-warning';
            case 'ROBOT_DELETED':
            case 'ERROR':
            case 'LOGOUT_FAILED':
                return 'log-error';
            case 'LOGIN':
            case 'LOGOUT':
            case 'INFO':
                return 'log-info';
            default:
                return '';
        }
    }

    /**
     * Update the log table in the DOM
     */
    function updateLogTable() {
        const tbody = document.getElementById('live-logs-body');
        if (!tbody) return;

        tbody.innerHTML = '';

        for (const log of logs) {
            const row = document.createElement('tr');
            row.className = log.cssClass || '';

            row.innerHTML =
                '<td>' + log.id + '</td>' +
                '<td class="log-timestamp">' + log.actionDate + '</td>' +
                '<td class="log-user">' + (log.userLogin || 'System') + '</td>' +
                '<td><span class="badge ' + getBadgeClass(log.actionType) + '">' + log.actionType + '</span></td>' +
                '<td class="log-details" title="' + (log.details || '') + '">' + (log.details || '') + '</td>';

            tbody.appendChild(row);
        }

        // Update count
        const countEl = document.getElementById('live-logs-count');
        if (countEl) countEl.textContent = logs.length;
    }

    /**
     * Get Bootstrap badge class for action type
     */
    function getBadgeClass(actionType) {
        switch (actionType) {
            case 'LOGIN': return 'bg-primary';
            case 'LOGOUT': return 'bg-secondary';
            case 'ORDER_STATUS_CHANGE': return 'bg-info';
            case 'ROBOT_CREATED': return 'bg-success';
            case 'ROBOT_UPDATED': return 'bg-warning text-dark';
            case 'ROBOT_DELETED': return 'bg-danger';
            case 'USER_ROLE_CHANGED': return 'bg-warning text-dark';
            case 'ORDER_SYNCED': return 'bg-success';
            default: return 'bg-secondary';
        }
    }

    /**
     * Update connection indicator
     */
    function updateConnectionIndicator() {
        const indicator = document.getElementById('signalr-status');
        if (!indicator) return;

        if (isConnected) {
            indicator.className = 'badge bg-success';
            indicator.textContent = '● Live';
        } else {
            indicator.className = 'badge bg-secondary';
            indicator.textContent = '○ Disconnected';
        }
    }

    /**
     * Disconnect from hub
     */
    function disconnect() {
        if (connection) {
            connection.stop();
            connection = null;
            isConnected = false;
        }
    }

    /**
     * Get current logs
     */
    function getLogs() {
        return logs;
    }

    /**
     * Initialize on DOM ready
     */
    document.addEventListener('DOMContentLoaded', function () {
        connect();

        // Cleanup on page unload
        window.addEventListener('unload', disconnect);
    });

    // Export API
    window.LogsSignalR = {
        connect: connect,
        disconnect: disconnect,
        getLogs: getLogs,
        isConnected: function () { return isConnected; }
    };
})();
