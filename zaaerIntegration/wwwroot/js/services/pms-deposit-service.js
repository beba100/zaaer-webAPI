(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/deposits";

    function unwrap(res) {
        if (res && res.success === false) {
            throw new Error(res.message || "Request failed.");
        }
        return res && (res.data !== undefined ? res.data : res);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsDepositService = {
        list(fromDate, toDate) {
            const params = {};
            if (fromDate) {
                params.fromDate = fromDate;
            }
            if (toDate) {
                params.toDate = toDate;
            }
            return api.get(base, params).then(unwrap);
        },
        getById(receiptId) {
            return api.get(`${base}/${receiptId}`).then(unwrap);
        },
        create(body) {
            return api.post(base, body).then(unwrap);
        },
        update(receiptId, body) {
            return api.put(`${base}/${receiptId}`, body).then(unwrap);
        },
        remove(receiptId) {
            return api.delete(`${base}/${receiptId}`).then(unwrap);
        },
        getBanks() {
            return api.get(`${base}/banks`).then(unwrap);
        },
        getPaymentMethods() {
            return api.get(`${base}/payment-methods`).then(unwrap);
        },
        getImages(receiptId) {
            return api.get(`${base}/${receiptId}/images`).then(unwrap);
        },
        uploadImages(receiptId, files) {
            const formData = new FormData();
            (files || []).forEach((file) => {
                formData.append("images", file);
            });
            return api.postForm(`${base}/${receiptId}/images`, formData).then((res) => unwrap(res));
        },
        deleteImage(receiptId, imageId) {
            return api.delete(`${base}/${receiptId}/images/${imageId}`).then(unwrap);
        }
    };
})(window);
