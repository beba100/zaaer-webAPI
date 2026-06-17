(function (window) {
    "use strict";

    const api = window.Zaaer && window.Zaaer.ApiService;
    const base = "/api/v1/pms/property";

    function unwrap(res) {
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
    window.Zaaer.PropertySettingsService = {
        uploadImageUrl() {
            return `${base}/upload-image`;
        },
        uploadImageHeaders: uploadHeaders,

        getMode() {
            return api.get(`${base}/mode`).then(unwrap);
        },

        getLookups() {
            return api.get(`${base}/lookups`).then(unwrap);
        },

        listBuildings() {
            return api.get(`${base}/buildings`).then(unwrap);
        },
        getBuilding(id) {
            return api.get(`${base}/buildings/${id}`).then(unwrap);
        },
        createBuilding(body) {
            return api.post(`${base}/buildings`, body).then(unwrap);
        },
        updateBuilding(id, body) {
            return api.put(`${base}/buildings/${id}`, body).then(unwrap);
        },
        deleteBuilding(id) {
            return api.delete(`${base}/buildings/${id}`).then(unwrap);
        },

        listRoomTypes() {
            return api.get(`${base}/room-types`).then(unwrap);
        },
        getRoomType(id) {
            return api.get(`${base}/room-types/${id}`).then(unwrap);
        },
        createRoomType(body) {
            return api.post(`${base}/room-types`, body).then(unwrap);
        },
        updateRoomType(id, body) {
            return api.put(`${base}/room-types/${id}`, body).then(unwrap);
        },
        deleteRoomType(id) {
            return api.delete(`${base}/room-types/${id}`).then(unwrap);
        },

        listApartments(params) {
            return api.get(`${base}/apartments`, params || {}).then(unwrap);
        },
        getApartment(id) {
            return api.get(`${base}/apartments/${id}`).then(unwrap);
        },
        createApartment(body) {
            return api.post(`${base}/apartments`, body).then(unwrap);
        },
        updateApartment(id, body) {
            return api.put(`${base}/apartments/${id}`, body).then(unwrap);
        },
        deleteApartment(id) {
            return api.delete(`${base}/apartments/${id}`).then(unwrap);
        },

        listFacilities() {
            return api.get(`${base}/facilities`).then(unwrap);
        },
        getFacility(id) {
            return api.get(`${base}/facilities/${id}`).then(unwrap);
        },
        createFacility(body) {
            return api.post(`${base}/facilities`, body).then(unwrap);
        },
        updateFacility(id, body) {
            return api.put(`${base}/facilities/${id}`, body).then(unwrap);
        },
        deleteFacility(id) {
            return api.delete(`${base}/facilities/${id}`).then(unwrap);
        }
    };
})(window);
