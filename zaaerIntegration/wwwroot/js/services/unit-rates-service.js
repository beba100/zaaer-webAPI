(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/property";

    function unwrap(res) {
        return res && (res.data !== undefined ? res.data : res);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.UnitRatesService = {
        listRoomTypeRates() {
            return api.get(`${base}/room-type-rates`).then(unwrap);
        },

        updateRoomTypeRate(rateId, body) {
            return api.put(`${base}/room-type-rates/${rateId}`, body).then(unwrap);
        },

        getRatesCalendar(params) {
            return api.get(`${base}/rates-calendar`, params || {}).then(unwrap);
        },

        upsertDailyRates(body) {
            return api.put(`${base}/rates-calendar/daily`, body).then(unwrap);
        }
    };
})(window);
