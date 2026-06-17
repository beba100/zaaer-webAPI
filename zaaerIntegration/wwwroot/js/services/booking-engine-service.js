(function (window) {
    "use strict";

    const publicBase = "/api/v1/public/booking";
    const adminBase = "/api/v1/pms/booking-engine";

    function unwrap(raw) {
        if (raw == null) {
            return null;
        }
        if (typeof raw === "string") {
            try {
                return JSON.parse(raw);
            } catch {
                return raw;
            }
        }
        let cur = raw;
        for (let i = 0; i < 3; i += 1) {
            if (cur && typeof cur === "object" && cur.data !== undefined) {
                cur = cur.data;
            } else {
                break;
            }
        }
        return cur;
    }

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    async function publicGet(path, params) {
        const qs = params ? `?${new URLSearchParams(params).toString()}` : "";
        const res = await fetch(`${publicBase}${path}${qs}`, {
            headers: { Accept: "application/json" }
        });
        const json = await res.json();
        if (!res.ok) {
            throw new Error(json.message || "Request failed");
        }
        return unwrap(json);
    }

    async function publicPost(path, body) {
        const res = await fetch(`${publicBase}${path}`, {
            method: "POST",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(body || {})
        });
        const json = await res.json();
        if (!res.ok) {
            throw new Error(json.message || "Request failed");
        }
        return unwrap(json);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.BookingEngineService = {
        formatLocalDateParam,
        loadHotels() {
            return publicGet("/hotels");
        },
        loadProfile(hotel) {
            return publicGet("/profile", { hotel });
        },
        search(params) {
            return publicGet("/search", params);
        },
        lookupReturningGuest(params) {
            return publicGet("/guest-lookup", params);
        },
        validateCoupon(body) {
            return publicPost("/validate-coupon", body);
        },
        confirm(body) {
            return publicPost("/confirm", body);
        },
        listCoupons() {
            return window.Zaaer.ApiService.get(`${adminBase}/coupons`).then(unwrap);
        },
        createCoupon(body) {
            return window.Zaaer.ApiService.post(`${adminBase}/coupons`, body).then(unwrap);
        },
        updateCoupon(couponId, body) {
            return window.Zaaer.ApiService.put(`${adminBase}/coupons/${encodeURIComponent(couponId)}`, body).then(unwrap);
        },
        deleteCoupon(couponId) {
            return window.Zaaer.ApiService.delete(`${adminBase}/coupons/${encodeURIComponent(couponId)}`).then(unwrap);
        },
        loadAdminSettings() {
            return window.Zaaer.ApiService.get(`${adminBase}/settings`).then(unwrap);
        },
        saveAdminSettings(body) {
            return window.Zaaer.ApiService.put(`${adminBase}/settings`, body).then(unwrap);
        },
        listAvailabilityOverrides(fromDate, toDate) {
            const q = {};
            if (fromDate) {
                q.fromDate = fromDate;
            }
            if (toDate) {
                q.toDate = toDate;
            }
            return window.Zaaer.ApiService.get(`${adminBase}/availability-overrides`, q).then(unwrap);
        },
        saveAvailabilityOverrides(body) {
            return window.Zaaer.ApiService.put(`${adminBase}/availability-overrides`, body).then(unwrap);
        },
        uploadImage(file) {
            const fd = new FormData();
            fd.append("file", file);
            return window.Zaaer.ApiService.postForm(`${adminBase}/upload-image`, fd).then(unwrap);
        },
        addMedia(body) {
            return window.Zaaer.ApiService.post(`${adminBase}/media`, body).then(unwrap);
        },
        deleteMedia(mediaId) {
            return window.Zaaer.ApiService.delete(`${adminBase}/media/${encodeURIComponent(mediaId)}`).then(unwrap);
        },
        loadAdminRoomTypes() {
            return window.Zaaer.ApiService.get(`${adminBase}/room-types`).then(unwrap);
        }
    };
})(window);
