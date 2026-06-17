/* global window */
(function () {
    "use strict";

    const base = "/api/v1/pms/corporate-customers";

    function unwrap(res) {
        if (!res) {
            return null;
        }
        if (res.data !== undefined) {
            return res.data;
        }
        if (res.Data !== undefined) {
            return res.Data;
        }
        return res;
    }

    function pick(obj, camel, pascal) {
        if (!obj) {
            return undefined;
        }
        if (obj[camel] !== undefined) {
            return obj[camel];
        }
        if (obj[pascal] !== undefined) {
            return obj[pascal];
        }
        return undefined;
    }

    /** Value stored on reservations when <c>zaaer_id</c> is allocated (else <c>corporate_id</c>). */
    function reservationCorporateId(corp) {
        const z = pick(corp, "zaaerId", "ZaaerId");
        const internal = pick(corp, "corporateId", "CorporateId");
        if (z != null && Number(z) > 0) {
            return Number(z);
        }
        return internal != null ? Number(internal) : null;
    }

    function getCorporate(id, hotelId) {
        const params = {};
        if (hotelId != null && `${hotelId}`.trim() !== "") {
            params.hotelId = hotelId;
        }
        return window.Zaaer.ApiService.get(`${base}/${id}`, params).then(unwrap);
    }

    function createCorporate(body) {
        return window.Zaaer.ApiService.post(base, body || {}).then(unwrap);
    }

    function updateCorporate(id, body, hotelId) {
        const params = {};
        if (hotelId != null && `${hotelId}`.trim() !== "") {
            params.hotelId = hotelId;
        }
        return window.Zaaer.ApiService.put(`${base}/${id}`, body || {}, params).then(unwrap);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsCorporateCustomerService = {
        getCorporate,
        createCorporate,
        updateCorporate,
        reservationCorporateId
    };
})();
