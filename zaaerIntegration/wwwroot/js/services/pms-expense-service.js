(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/expenses";

    function unwrap(res) {
        if (res && res.success === false) {
            throw new Error(res.message || "Request failed.");
        }
        return res && (res.data !== undefined ? res.data : res);
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
    window.Zaaer.PmsExpenseService = {
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
        searchCompanies(search) {
            return api.get(`${base}/companies`, { search: search || "" }).then(unwrap);
        },
        lookupCompanyByTax(taxNumber) {
            return api.get(`${base}/companies/lookup`, { taxNumber: taxNumber || "" }).then((res) => {
                if (res && res.success === false) {
                    throw new Error(res.message || "Request failed.");
                }
                return {
                    found: !!(res && res.found),
                    data: res && res.data != null ? res.data : null
                };
            });
        },
        getApprovalHistory(expenseId) {
            return api.get(`${base}/${expenseId}/approval-history`).then(unwrap);
        },
        getById(expenseId) {
            return api.get(`${base}/${expenseId}`).then(unwrap);
        },
        create(body) {
            return api.post(base, body).then(unwrap);
        },
        update(expenseId, body) {
            return api.put(`${base}/${expenseId}`, body).then(unwrap);
        },
        remove(expenseId) {
            return api.delete(`${base}/${expenseId}`).then(unwrap);
        },
        getCategories() {
            return api.get(`${base}/categories`).then(unwrap);
        },
        getTaxConfig() {
            return api.get(`${base}/tax-config`).then(unwrap);
        },
        getImages(expenseId) {
            return api.get(`${base}/${expenseId}/images`).then(unwrap);
        },
        uploadImages(expenseId, files) {
            const formData = new FormData();
            (files || []).forEach((file) => {
                formData.append("images", file);
            });
            return api.postForm(`${base}/${expenseId}/images`, formData).then((res) => unwrap(res));
        },
        deleteImage(expenseId, imageId) {
            return api.delete(`${base}/${expenseId}/images/${imageId}`).then(unwrap);
        },
        uploadHeaders
    };
})(window);
