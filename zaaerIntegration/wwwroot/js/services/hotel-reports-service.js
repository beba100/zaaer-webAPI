(function (window) {
    "use strict";

    function api() {
        return window.Zaaer && window.Zaaer.ApiService;
    }

    function unwrap(res) {
        if (!res) {
            return res;
        }
        return res.data !== undefined ? res.data : (res.Data !== undefined ? res.Data : res);
    }

    function cleanParams(params) {
        const out = {};
        Object.keys(params || {}).forEach((key) => {
            const val = params[key];
            if (val !== undefined && val !== null && val !== "") {
                out[key] = val;
            }
        });
        return out;
    }

    const base = "/api/v1/pms/hotel-reports";

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HotelReportsService = {
        getBookingsReport(fromDate, toDate) {
            return api().get(`${base}/reports/bookings`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getReceiptsReport(fromDate, toDate) {
            return api().get(`${base}/reports/receipts`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDisbursementsReport(fromDate, toDate) {
            return api().get(`${base}/reports/disbursements`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getInvoicesReport(fromDate, toDate) {
            return api().get(`${base}/reports/invoices`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getCreditNotesReport(fromDate, toDate) {
            return api().get(`${base}/reports/credit-notes`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDailyJournalReport(fromDate, toDate) {
            return api().get(`${base}/reports/daily-journal`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getNetworkCashPaymentsReport(fromDate, toDate) {
            return api().get(`${base}/reports/network-cash-payments`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getDeparturesReport(fromDate, toDate) {
            return api().get(`${base}/reports/departures`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getOnlineBookingsReport(fromDate, toDate) {
            return api().get(`${base}/reports/online-bookings`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getUnitTransfersReport(fromDate, toDate) {
            return api().get(`${base}/reports/unit-transfers`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        getMonthEndClosingReport(fromDate, toDate) {
            return api().get(`${base}/reports/month-end-closing`, cleanParams({ fromDate, toDate })).then(unwrap);
        }
    };
})(window);
