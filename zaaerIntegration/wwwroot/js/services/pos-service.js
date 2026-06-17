(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/pos";

    function unwrap(res) {
        return res && (res.data !== undefined ? res.data : res);
    }

    function catalogBase() {
        return `${base}/catalog`;
    }

    function ordersBase() {
        return `${base}/orders`;
    }

    function uploadHeaders() {
        const headers = {};
        const token = api.getToken();
        const hotelCode = api.getHotelCode();
        if (token) {
            headers.Authorization = `Bearer ${token}`;
        }
        if (hotelCode) {
            headers["X-Hotel-Code"] = hotelCode;
        }
        return headers;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PosService = {
        uploadItemImageUrl() {
            return `${catalogBase()}/items/upload-image`;
        },
        uploadItemImageHeaders: uploadHeaders,
        listOutlets() {
            return api.get(`${catalogBase()}/outlets`).then(unwrap);
        },
        createOutlet(body) {
            return api.post(`${catalogBase()}/outlets`, body).then(unwrap);
        },
        updateOutlet(id, body) {
            return api.put(`${catalogBase()}/outlets/${id}`, body).then(unwrap);
        },
        listCategories() {
            return api.get(`${catalogBase()}/categories`).then(unwrap);
        },
        createCategory(body) {
            return api.post(`${catalogBase()}/categories`, body).then(unwrap);
        },
        updateCategory(id, body) {
            return api.put(`${catalogBase()}/categories/${id}`, body).then(unwrap);
        },
        listItems(params) {
            return api.get(`${catalogBase()}/items`, params || {}).then(unwrap);
        },
        createItem(body) {
            return api.post(`${catalogBase()}/items`, body).then(unwrap);
        },
        updateItem(id, body) {
            return api.put(`${catalogBase()}/items/${id}`, body).then(unwrap);
        },
        listTables(params) {
            return api.get(`${catalogBase()}/tables`, params || {}).then(unwrap);
        },
        createTable(body) {
            return api.post(`${catalogBase()}/tables`, body).then(unwrap);
        },
        updateTable(id, body) {
            return api.put(`${catalogBase()}/tables/${id}`, body).then(unwrap);
        },
        getPosMenu(outletId) {
            return api.get(`${catalogBase()}/pos-menu/${outletId}`).then(unwrap);
        },
        listRecentOrders(params) {
            return api.get(`${ordersBase()}/recent`, params || {}).then(unwrap);
        },
        listOrders(params) {
            return api.get(ordersBase(), params || {}).then(unwrap);
        },
        getOrder(orderId) {
            return api.get(`${ordersBase()}/${orderId}`).then(unwrap);
        },
        listInHouseReservations() {
            return api.get(`${ordersBase()}/in-house-reservations`).then(unwrap);
        },
        createOrder(body) {
            return api.post(ordersBase(), body).then(unwrap);
        },
        updateOrderReceipt(orderId, body) {
            return api.patch(`${ordersBase()}/${orderId}/receipt`, body).then(unwrap);
        },
        cancelOrder(orderId) {
            return api.post(`${ordersBase()}/${orderId}/cancel`, {}).then(unwrap);
        },
        updateTransferredOrder(orderId, body) {
            return api.put(`${ordersBase()}/${orderId}/transferred`, body).then(unwrap);
        },
        getPaymentMethods() {
            return api.get("/api/v1/pms/lookups/payment-methods").then(unwrap);
        }
    };
})(window);
