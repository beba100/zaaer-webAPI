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

    const base = "/api/v1/pms/hotel-targets";

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.HotelTargetsService = {
        getTargetReport(fromDate, toDate) {
            return api().get(`${base}/report`, cleanParams({ fromDate, toDate })).then(unwrap);
        },
        listSettings() {
            return api().get(`${base}/settings`).then(unwrap);
        },
        createSetting(body) {
            return api().post(`${base}/settings`, body || {}).then(unwrap);
        },
        updateSetting(id, body) {
            return api().put(`${base}/settings/${id}`, body || {}).then(unwrap);
        }
    };
})(window);
