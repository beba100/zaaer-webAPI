(function (window, DevExpress) {
    "use strict";

    const endpoint = "/api/v1/pms/room-board";

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        if (Number.isNaN(d.getTime())) {
            return value;
        }

        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function cleanParams(filters) {
        const params = {};

        Object.keys(filters || {}).forEach((key) => {
            const value = filters[key];
            if (value !== undefined && value !== null && value !== "") {
                params[key] = value instanceof Date ? formatLocalDateParam(value) : value;
            }
        });

        return params;
    }

    async function loadBoard(filters) {
        const response = await window.Zaaer.ApiService.get(endpoint, cleanParams(filters));
        return response.data || response;
    }

    /**
     * Master DB tenants for the hotel filter (Code = X-Hotel-Code / hotel_settings.hotel_code).
     */
    async function loadHotelCodes() {
        const response = await window.Zaaer.ApiService.get(`${endpoint}/hotel-codes`);
        const payload = response && typeof response === "object" ? response : {};
        const inner = payload.data !== undefined ? payload.data : payload.Data;
        return Array.isArray(inner) ? inner : [];
    }

    async function saveRoomCardColor(apartmentId, payload) {
        return window.Zaaer.ApiService.put(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/card-color`,
            payload || {}
        );
    }

    async function deleteRoomCardColor(apartmentId) {
        return window.Zaaer.ApiService.delete(`${endpoint}/apartments/${encodeURIComponent(apartmentId)}/card-color`);
    }

    async function applyApartmentQuickState(apartmentId, body) {
        return window.Zaaer.ApiService.post(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/quick-state`,
            body || {}
        );
    }

    async function getApartmentMaintenances(apartmentId, params) {
        return window.Zaaer.ApiService.get(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/maintenances`,
            params || {}
        );
    }

    async function createApartmentMaintenance(apartmentId, body, params) {
        return window.Zaaer.ApiService.post(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/maintenances`,
            body || {},
            params || {}
        );
    }

    async function updateApartmentMaintenance(apartmentId, maintenanceId, body, params) {
        return window.Zaaer.ApiService.put(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/maintenances/${encodeURIComponent(maintenanceId)}`,
            body || {},
            params || {}
        );
    }

    async function cancelApartmentMaintenance(apartmentId, maintenanceId, params) {
        return window.Zaaer.ApiService.delete(
            `${endpoint}/apartments/${encodeURIComponent(apartmentId)}/maintenances/${encodeURIComponent(maintenanceId)}`,
            params || {}
        );
    }

    function createRoomsStore(getFilters, postFilter) {
        let cachedFilterKey = null;
        let cachedBoardPromise = null;

        function currentFilters() {
            return typeof getFilters === "function" ? getFilters() : {};
        }

        function filterKey(filters) {
            return JSON.stringify(cleanParams(filters || {}));
        }

        async function getBoard(forceReload) {
            const filters = currentFilters();
            const key = filterKey(filters);
            if (forceReload || key !== cachedFilterKey || !cachedBoardPromise) {
                cachedFilterKey = key;
                cachedBoardPromise = loadBoard(filters).catch((err) => {
                    if (cachedFilterKey === key) {
                        cachedBoardPromise = null;
                    }
                    throw err;
                });
            }

            return cachedBoardPromise;
        }

        function applyPostFilter(board) {
            let rooms = board && board.rooms ? board.rooms : [];
            if (typeof postFilter === "function") {
                rooms = rooms.filter(postFilter);
            }
            return rooms;
        }

        return new DevExpress.data.CustomStore({
            key: "apartmentId",
            load: async () => {
                return applyPostFilter(await getBoard(true));
            },
            byKey: async (key) => {
                const rooms = applyPostFilter(await getBoard(false));
                return (rooms || []).find((room) => room.apartmentId === key);
            }
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomBoardService = {
        loadBoard,
        loadHotelCodes,
        saveRoomCardColor,
        deleteRoomCardColor,
        applyApartmentQuickState,
        getApartmentMaintenances,
        createApartmentMaintenance,
        updateApartmentMaintenance,
        cancelApartmentMaintenance,
        createRoomsStore
    };
})(window, DevExpress);
