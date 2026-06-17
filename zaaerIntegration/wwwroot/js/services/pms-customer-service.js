/* global window */
(function () {
    "use strict";

    const base = "/api/v1/pms/customers";

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

    /** Integration id used on reservations (zaaer_id when allocated, else internal customer_id). */
    function reservationCustomerId(customer) {
        const z = pick(customer, "zaaerId", "ZaaerId");
        const internal = pick(customer, "customerId", "CustomerId");
        if (z != null && Number(z) > 0) {
            return Number(z);
        }
        return internal != null ? Number(internal) : null;
    }

    async function getCustomer(id, hotelId) {
        const params = {};
        if (hotelId != null && `${hotelId}`.trim() !== "") {
            params.hotelId = hotelId;
        }
        const response = await window.Zaaer.ApiService.get(`${base}/${id}`, params);
        return unwrap(response);
    }

    async function createCustomer(body) {
        const response = await window.Zaaer.ApiService.post(base, body || {});
        return unwrap(response);
    }

    async function updateCustomer(id, body, hotelId) {
        const params = {};
        if (hotelId != null && `${hotelId}`.trim() !== "") {
            params.hotelId = hotelId;
        }
        const response = await window.Zaaer.ApiService.put(`${base}/${id}`, body || {}, params);
        return unwrap(response);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsCustomerService = {
        getCustomer,
        createCustomer,
        updateCustomer,
        reservationCustomerId
    };
})();
