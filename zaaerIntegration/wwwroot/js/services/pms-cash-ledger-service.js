(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/cash-ledger";

    function unwrap(res) {
        if (res && res.success === false) {
            throw new Error(res.message || "Request failed.");
        }
        return res && (res.data !== undefined ? res.data : res);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsCashLedgerService = {
        report(fromDate, toDate) {
            const params = {};
            if (fromDate) {
                params.fromDate = fromDate;
            }
            if (toDate) {
                params.toDate = toDate;
            }
            return api.get(`${base}/report`, params).then(unwrap);
        },
        backfill() {
            return api.post(`${base}/backfill`, {}).then(unwrap);
        }
    };
})(window);
