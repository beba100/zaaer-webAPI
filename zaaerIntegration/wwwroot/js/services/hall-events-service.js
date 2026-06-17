(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/hall-events";
    const dateParam = window.Zaaer && window.Zaaer.PmsDateParam;

    function unwrap(res) {
        if (!res) {
            return res;
        }
        return res.data !== undefined ? res.data : (res.Data !== undefined ? res.Data : res);
    }

    function formatLocalDateParam(value) {
        if (dateParam && typeof dateParam.formatLocalDateParam === "function") {
            return dateParam.formatLocalDateParam(value);
        }
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return value;
        }
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function cleanParams(params) {
        const out = {};
        Object.keys(params || {}).forEach((key) => {
            const value = params[key];
            if (value !== undefined && value !== null && value !== "") {
                out[key] = value instanceof Date ? formatLocalDateParam(value) : value;
            }
        });
        return out;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HallEventsService = {
        getLookups() {
            return api.get(`${base}/lookups`).then(unwrap);
        },
        listEvents(params) {
            const p = params || {};
            const out = cleanParams(p);
            if (p.fromDateHijri) {
                out.fromDateHijri = p.fromDateHijri;
            }
            if (p.toDateHijri) {
                out.toDateHijri = p.toDateHijri;
            }
            if (p.eventDateHijri) {
                out.eventDateHijri = p.eventDateHijri;
            }
            if (p.hijriYear != null && p.hijriYear !== "") {
                out.hijriYear = p.hijriYear;
            }
            if (p.hijriMonth != null && p.hijriMonth !== "") {
                out.hijriMonth = p.hijriMonth;
            }
            return api.get(base, out).then(unwrap);
        },
        updateSchedule(reservationId, body) {
            return api.put(`${base}/${encodeURIComponent(reservationId)}/schedule`, body || {}).then(unwrap);
        },
        updateEvent(reservationId, body) {
            return api.put(`${base}/${encodeURIComponent(reservationId)}`, body || {}).then(unwrap);
        },
        getEvent(reservationId) {
            return api.get(`${base}/${encodeURIComponent(reservationId)}`).then(unwrap);
        },
        createEvent(body) {
            return api.post(base, body || {}).then(unwrap);
        },
        transitionStatus(reservationId, body) {
            return api.post(`${base}/${encodeURIComponent(reservationId)}/transition`, body || {}).then(unwrap);
        },
        checkInEvent(reservationId) {
            return api.post(`${base}/${encodeURIComponent(reservationId)}/check-in`, {}).then(unwrap);
        },
        recordDeposit(reservationId, body) {
            return api.post(`${base}/${encodeURIComponent(reservationId)}/deposit`, body || {}).then(unwrap);
        },
        completeEvent(reservationId, body) {
            return api.post(`${base}/${encodeURIComponent(reservationId)}/complete`, body || {}).then(unwrap);
        },
        getSchedulerItems(fromDate, toDate) {
            return api.get(`${base}/scheduler`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDashboard() {
            return api.get(`${base}/dashboard`).then(unwrap);
        },
        getOccupancy() {
            return api.get(`${base}/occupancy`).then(unwrap);
        },
        syncStatuses() {
            return api.post(`${base}/sync-statuses`, {}).then(unwrap);
        },
        getFunctionSheet(reservationId) {
            return api.get(`${base}/${encodeURIComponent(reservationId)}/function-sheet`).then(unwrap);
        },
        upsertFunctionSheet(reservationId, body) {
            return api.put(`${base}/${encodeURIComponent(reservationId)}/function-sheet`, body || {}).then(unwrap);
        },
        approveFunctionSheet(reservationId) {
            return api.post(`${base}/${encodeURIComponent(reservationId)}/function-sheet/approve`, {}).then(unwrap);
        },
        printFunctionSheetUrl(reservationId) {
            return `${base}/${encodeURIComponent(reservationId)}/function-sheet/print`;
        },
        printContractUrl(reservationId) {
            return `${base}/${encodeURIComponent(reservationId)}/contract/print`;
        },
        listAlerts(unreadOnly) {
            return api.get(`${base}/alerts`, cleanParams({ unreadOnly: unreadOnly ? true : undefined })).then(unwrap);
        },
        markAlertRead(alertId) {
            return api.post(`${base}/alerts/${encodeURIComponent(alertId)}/read`, {}).then(unwrap);
        },
        getDailyReport(date) {
            return api.get(`${base}/reports/daily`, cleanParams({ date })).then(unwrap);
        },
        getUtilizationReport(fromDate, toDate) {
            return api.get(`${base}/reports/utilization`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getBookingsReport(fromDate, toDate, params) {
            return api.get(`${base}/reports/bookings`, cleanParams(Object.assign({ fromDate, toDate }, params || {}))).then(unwrap);
        },
        getReceiptsReport(fromDate, toDate) {
            return api.get(`${base}/reports/receipts`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDisbursementsReport(fromDate, toDate) {
            return api.get(`${base}/reports/disbursements`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getInvoicesReport(fromDate, toDate) {
            return api.get(`${base}/reports/invoices`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getCreditNotesReport(fromDate, toDate) {
            return api.get(`${base}/reports/credit-notes`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDailyJournalReport(fromDate, toDate) {
            return api.get(`${base}/reports/daily-journal`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getNetworkCashPaymentsReport(fromDate, toDate) {
            return api.get(`${base}/reports/network-cash-payments`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getUnpaidBalances(params) {
            return api.get(`${base}/unpaid-balances`, cleanParams(params || {})).then(unwrap);
        },
        getSettlement(reservationId) {
            return api.get(`${base}/${encodeURIComponent(reservationId)}/settlement`).then(unwrap);
        },
        updateHallPreparation(hallId, body) {
            return api.patch(`${base}/halls/${encodeURIComponent(hallId)}/preparation`, body || {}).then(unwrap);
        },
        getPaymentMethods() {
            return api.get("/api/v1/pms/lookups/payment-methods").then((res) => {
                const data = unwrap(res);
                return Array.isArray(data) ? data : [];
            });
        }
    };
})(window);
