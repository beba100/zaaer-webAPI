(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/resort-tickets";
    const dateParam = window.Zaaer && window.Zaaer.PmsDateParam;

    function unwrap(res) {
        if (!res) {
            return res;
        }

        if (res.data !== undefined) {
            return res.data;
        }

        return res.Data !== undefined ? res.Data : res;
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
        if (dateParam && typeof dateParam.cleanDateQueryParams === "function") {
            return dateParam.cleanDateQueryParams(params);
        }

        const out = {};
        Object.keys(params || {}).forEach((key) => {
            const value = params[key];
            if (value !== undefined && value !== null && value !== "") {
                out[key] = value instanceof Date ? formatLocalDateParam(value) : value;
            }
        });
        return out;
    }

    function unwrapArray(res) {
        const data = unwrap(res);
        return Array.isArray(data) ? data : [];
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ResortTicketService = {
        getLookups() {
            return api.get(`${base}/lookups`).then(unwrap);
        },
        getPaymentMethods() {
            return api.get("/api/v1/pms/lookups/payment-methods").then(unwrapArray);
        },
        getBanks() {
            return api.get("/api/v1/pms/lookups/banks").then(unwrapArray);
        },
        listTypes(params) {
            return api.get(`${base}/types`, cleanParams(params)).then(unwrap);
        },
        getType(id) {
            return api.get(`${base}/types/${encodeURIComponent(id)}`).then(unwrap);
        },
        createType(body) {
            return api.post(`${base}/types`, body || {}).then(unwrap);
        },
        updateType(id, body) {
            return api.put(`${base}/types/${encodeURIComponent(id)}`, body || {}).then(unwrap);
        },
        setTypeActive(id, isActive) {
            return api.patch(`${base}/types/${encodeURIComponent(id)}/active`, { isActive: !!isActive }).then(unwrap);
        },
        listOrders(params) {
            return api.get(`${base}/orders`, cleanParams(params)).then(unwrap);
        },
        getOrder(id) {
            return api.get(`${base}/orders/${encodeURIComponent(id)}`).then(unwrap);
        },
        createOrder(body) {
            return api.post(`${base}/orders`, body || {}).then(unwrap);
        },
        cancelOrder(id, body) {
            return api.post(`${base}/orders/${encodeURIComponent(id)}/cancel`, body || {}).then(unwrap);
        },
        lookupByQr(qrCode, stationCode) {
            const params = { qr: String(qrCode || "").trim() };
            if (stationCode) {
                params.station = String(stationCode).trim();
            }
            return api.get(`${base}/by-qr`, params).then(unwrap);
        },
        redeemTicket(qrCode, stationCode) {
            const body = { qrCode: String(qrCode || "").trim() };
            if (stationCode) {
                body.stationCode = String(stationCode).trim();
            }
            return api.post(`${base}/redeem`, body).then(unwrap);
        },
        printOrderUrl(id, paper) {
            return `${base}/orders/${encodeURIComponent(id)}/print?paper=${encodeURIComponent(paper || "thermal")}`;
        },
        printTicketUrl(id, paper) {
            return `${base}/${encodeURIComponent(id)}/print?paper=${encodeURIComponent(paper || "thermal")}`;
        },
        getBusinessConfig() {
            return api.get(`${base}/config/business-hours`).then(unwrap);
        },
        updateBusinessConfig(body) {
            return api.put(`${base}/config/business-hours`, body || {}).then(unwrap);
        },
        listPendingInvoiceOrders(params) {
            return api.get(`${base}/finance/pending-invoices`, cleanParams(params || {})).then(unwrap);
        },
        listTicketInvoices(params) {
            return api.get(`${base}/finance/invoices`, cleanParams(params || {})).then(unwrap);
        },
        createTicketInvoices(body) {
            return api.post(`${base}/finance/invoices`, body || {}).then(unwrap);
        },
        listTicketReceipts(params) {
            return api.get(`${base}/finance/receipts`, cleanParams(params || {})).then(unwrapArray);
        },
        getFinanceReconciliation(params) {
            return api.get(`${base}/finance/reconciliation`, cleanParams(params || {})).then(unwrap);
        },
        sendInvoiceToZatca(invoiceId) {
            return api.post("/api/v1/pms/integrations/zatca/send-document", {
                documentKind: "invoice",
                documentId: invoiceId
            });
        },
        sendCreditNoteToZatca(creditNoteId) {
            return api.post("/api/v1/pms/integrations/zatca/send-document", {
                documentKind: "credit_note",
                documentId: creditNoteId
            });
        },
        formatLocalDateParam
    };
})(window);
