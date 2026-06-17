(function (window) {
    "use strict";

    const DATE_QUERY_KEYS = new Set([
        "fromDate",
        "toDate",
        "serviceDate",
        "date",
        "dateFrom",
        "dateTo",
        "invoiceDate",
        "receiptDate"
    ]);

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return value;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function normalizeDateQueryParam(value) {
        if (value === undefined || value === null || value === "") {
            return value;
        }

        if (value instanceof Date) {
            return formatLocalDateParam(value);
        }

        if (typeof value === "string") {
            const trimmed = value.trim();
            if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
                return trimmed;
            }

            const parsed = new Date(trimmed);
            if (!Number.isNaN(parsed.getTime())) {
                return formatLocalDateParam(parsed);
            }
        }

        return value;
    }

    function cleanDateQueryParams(params) {
        const out = {};
        Object.keys(params || {}).forEach((key) => {
            const value = params[key];
            if (value === undefined || value === null || value === "") {
                return;
            }

            out[key] = DATE_QUERY_KEYS.has(key) ? normalizeDateQueryParam(value) : value;
        });
        return out;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsDateParam = {
        formatLocalDateParam,
        normalizeDateQueryParam,
        cleanDateQueryParams
    };
})(window);
