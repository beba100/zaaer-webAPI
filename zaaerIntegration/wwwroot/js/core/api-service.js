(function (window, $) {
    "use strict";

    const storageKeys = {
        token: "zaaer.auth.token",
        refreshToken: "zaaer.auth.refreshToken",
        deviceId: "zaaer.auth.deviceId",
        hotelCode: "zaaer.tenant.hotelCode",
        hotelName: "zaaer.tenant.hotelName",
        hotelNameEn: "zaaer.tenant.hotelNameEn",
        userDisplayName: "zaaer.user.displayName",
        userDisplayNameEn: "zaaer.user.displayNameEn",
        permissions: "zaaer.auth.permissions",
        landingUrl: "zaaer.auth.landingUrl",
        gateStations: "zaaer.auth.gateStations"
    };

    let permissionCache = null;
    let refreshInFlight = null;

    function getOrCreateDeviceId() {
        let deviceId = window.localStorage.getItem(storageKeys.deviceId);
        if (!deviceId) {
            deviceId = (window.crypto && window.crypto.randomUUID)
                ? window.crypto.randomUUID()
                : `dev-${Date.now()}-${Math.random().toString(16).slice(2)}`;
            window.localStorage.setItem(storageKeys.deviceId, deviceId);
        }

        return deviceId;
    }

    function getDeviceName() {
        return (navigator.userAgent || "Unknown device").slice(0, 200);
    }

    function getQueryValue(name) {
        return new URLSearchParams(window.location.search).get(name);
    }

    function getHotelCode() {
        return getQueryValue("hotelCode") || window.localStorage.getItem(storageKeys.hotelCode);
    }

    function currentUiCulture() {
        if (window.Zaaer && window.Zaaer.LocalizationService && typeof window.Zaaer.LocalizationService.currentCulture === "function") {
            return window.Zaaer.LocalizationService.currentCulture();
        }
        return window.localStorage.getItem("zaaer.ui.culture") || "ar";
    }

    function buildHeaders() {
        const headers = {};
        const token = window.localStorage.getItem(storageKeys.token);
        const hotelCode = getHotelCode();

        if (token) {
            headers.Authorization = `Bearer ${token}`;
        }

        if (hotelCode) {
            headers["X-Hotel-Code"] = hotelCode;
        }

        headers["X-Ui-Culture"] = currentUiCulture();
        headers["X-Device-Id"] = getOrCreateDeviceId();

        return headers;
    }

    function withHotelCodeParam(params) {
        const next = params && typeof params === "object" ? { ...params } : {};
        const hotelCode = getHotelCode();
        if (hotelCode && next.hotelCode === undefined && next.HotelCode === undefined) {
            next.hotelCode = hotelCode;
        }

        return next;
    }

    function request(method, endpoint, data, internal) {
        const httpMethod = (method || "GET").toUpperCase();
        const params = withHotelCodeParam(data);
        const url = httpMethod === "GET" ? withQuery(endpoint, params) : endpoint;
        const deferred = $.Deferred();

        $.ajax({
            url,
            method: httpMethod,
            data: httpMethod === "GET" ? undefined : params,
            dataType: "json",
            contentType: httpMethod === "GET" ? undefined : "application/json",
            headers: buildHeaders()
        })
            .done(deferred.resolve)
            .fail(function onRequestFail(xhr) {
                if (xhr && xhr.status === 401 && getRefreshToken() && !internal) {
                    tryRefreshAccessToken()
                        .then(function () {
                            return request(method, endpoint, data, true);
                        })
                        .done(deferred.resolve)
                        .fail(function () {
                            handleAuthFailure(xhr);
                            deferred.reject(xhr);
                        });
                    return;
                }

                handleAuthFailure(xhr);
                deferred.reject(xhr);
            });

        return deferred.promise();
    }

    function isOversizedAuthRequestFailure(xhr) {
        if (!xhr || xhr.status !== 400) {
            return false;
        }

        const text = `${xhr.responseText || ""}`;
        return /HTTP Error 400/i.test(text) && /Bad Request/i.test(text);
    }

    function handleAuthFailure(xhr) {
        if (!xhr || xhr.status === 0) {
            return;
        }

        const path = (window.location.pathname || "").toLowerCase();
        const onLoginPage = /\/login\.html$/i.test(path);
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search || ""}`);

        if (xhr.status === 401 || (isOversizedAuthRequestFailure(xhr) && getToken())) {
            clearToken();
            if (!onLoginPage) {
                window.location.href = `/login.html?returnUrl=${returnUrl}`;
            }
        }
    }

    function setRefreshToken(token) {
        if (token) {
            window.localStorage.setItem(storageKeys.refreshToken, token);
        }
    }

    function getRefreshToken() {
        return window.localStorage.getItem(storageKeys.refreshToken);
    }

    function tryRefreshAccessToken() {
        const refreshToken = getRefreshToken();
        if (!refreshToken) {
            return $.Deferred().reject().promise();
        }

        if (refreshInFlight) {
            return refreshInFlight;
        }

        refreshInFlight = $.ajax({
            url: "/api/Auth/refresh",
            method: "POST",
            contentType: "application/json",
            dataType: "json",
            data: JSON.stringify({
                refreshToken: refreshToken,
                deviceId: getOrCreateDeviceId(),
                hotelCode: getHotelCode()
            })
        }).done((response) => {
            applyAuthResponse(response, { skipAutoRefresh: true });
        }).always(() => {
            refreshInFlight = null;
        });

        return refreshInFlight;
    }

    function setToken(token) {
        if (token) {
            window.localStorage.setItem(storageKeys.token, token);
        }
    }

    function getToken() {
        return window.localStorage.getItem(storageKeys.token);
    }

    function clearToken() {
        window.localStorage.removeItem(storageKeys.token);
        window.localStorage.removeItem(storageKeys.refreshToken);
        window.localStorage.removeItem(storageKeys.landingUrl);
        window.localStorage.removeItem(storageKeys.gateStations);
        clearPermissionCache();
    }

    function getLandingUrl() {
        return window.localStorage.getItem(storageKeys.landingUrl) || "";
    }

    function getGateStations() {
        try {
            const raw = window.localStorage.getItem(storageKeys.gateStations);
            return raw ? JSON.parse(raw) : [];
        } catch {
            return [];
        }
    }

    function requireToken() {
        if (!getToken() && !/\/login\.html$/i.test(window.location.pathname)) {
            const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search || ""}`);
            window.location.href = `/login.html?returnUrl=${returnUrl}`;
            return false;
        }

        return true;
    }

    function decodeTokenPayload() {
        const token = getToken();
        if (!token || token.split(".").length < 2) {
            return null;
        }

        try {
            const payload = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
            return JSON.parse(decodeURIComponent(escape(window.atob(payload))));
        } catch {
            return null;
        }
    }

    function normalizePermissionList(source) {
        if (!source) {
            return [];
        }

        if (Array.isArray(source)) {
            return source.map((x) => `${x}`.trim()).filter(Boolean);
        }

        if (typeof source === "string") {
            return source.split(",").map((x) => x.trim()).filter(Boolean);
        }

        return [];
    }

    function readPermissionCacheFromStorage() {
        try {
            const raw = window.localStorage.getItem(storageKeys.permissions);
            if (!raw) {
                return null;
            }

            return normalizePermissionList(JSON.parse(raw));
        } catch {
            return null;
        }
    }

    function setPermissionCache(list) {
        permissionCache = normalizePermissionList(list);
        window.localStorage.setItem(storageKeys.permissions, JSON.stringify(permissionCache));
        return permissionCache;
    }

    function clearPermissionCache() {
        permissionCache = null;
        window.localStorage.removeItem(storageKeys.permissions);
    }

    function getJwtPermissions() {
        const payload = decodeTokenPayload();
        if (!payload) {
            return [];
        }

        if (typeof payload.permissions === "string" && payload.permissions.length) {
            return normalizePermissionList(payload.permissions);
        }

        if (Array.isArray(payload.permission)) {
            return normalizePermissionList(payload.permission);
        }

        if (typeof payload.permission === "string" && payload.permission.length) {
            return [payload.permission];
        }

        return [];
    }

    function getEffectivePermissions() {
        if (permissionCache && permissionCache.length) {
            return permissionCache.slice();
        }

        const stored = readPermissionCacheFromStorage();
        if (stored && stored.length) {
            permissionCache = stored;
            return permissionCache.slice();
        }

        return [];
    }

    const SYSTEM_LEGACY_NAV_MENU = {
        "rbac.users.manage": "nav.menu.system.users",
        "rbac.roles.manage": "nav.menu.system.roles",
        "rbac.permissions.view": "nav.menu.system.permissions",
        "admin.numbering.manage": "nav.menu.system.numbering"
    };

    function hasPermission(code) {
        if (!code) {
            return false;
        }

        if (Array.isArray(code)) {
            return code.some((entry) => hasPermission(entry));
        }

        const target = `${code}`.toLowerCase();
        const permissions = getEffectivePermissions().map((p) => `${p}`.toLowerCase());

        const navMenuEquivalent = SYSTEM_LEGACY_NAV_MENU[target];
        if (navMenuEquivalent) {
            return permissions.includes(navMenuEquivalent.toLowerCase());
        }

        if (permissions.includes(target)) {
            return true;
        }

        for (const prefix of ["hotel.reports.", "resort.reports.", "hall.reports."]) {
            if (target.startsWith(prefix)) {
                const parent = prefix.slice(0, -1);
                if (permissions.includes(parent)) {
                    return true;
                }
            }
        }

        return false;
    }

    function ensurePermissionsReady() {
        const cached = getEffectivePermissions();
        if (cached.length) {
            return $.Deferred().resolve(cached).promise();
        }

        return refreshPermissions();
    }

    function resolveLandingUrl(requestedUrl) {
        const raw = (requestedUrl || "").trim();
        const path = (raw.split("?")[0] || "").toLowerCase();
        const roleLanding = getLandingUrl();

        if (path && path !== "/room-board.html" && path !== "/" && path !== "/login.html") {
            if (
                /\/resort-ticket-gate(-home)?\.html$/i.test(path) ||
                /\/resort-ticket-scanner\.html$/i.test(path)
            ) {
                if (hasPermission("resort_tickets.validate")) {
                    return raw || path;
                }
            }

            return raw || "/room-board.html";
        }

        if (roleLanding && hasPermission("resort_tickets.validate")) {
            return roleLanding;
        }

        if (hasPermission("room_board.view")) {
            return "/room-board.html";
        }

        if (hasPermission("resort_tickets.validate")) {
            return "/resort-ticket-gate.html";
        }

        if (hasPermission("resort_tickets.view")) {
            return "/resort-tickets.html";
        }

        if (hasPermission("pos.view")) {
            return "/pos.html";
        }

        return "/room-board.html";
    }

    let permissionAutoRefreshTimer = null;
    let permissionVisibilityBound = false;
    let permissionRefreshInFlight = null;
    let permissionRefreshFailCount = 0;
    const PERMISSION_REFRESH_INTERVAL_MS = 120000;

    function permissionListsEqual(a, b) {
        const left = (a || []).map((x) => `${x}`.toLowerCase()).sort();
        const right = (b || []).map((x) => `${x}`.toLowerCase()).sort();
        if (left.length !== right.length) {
            return false;
        }

        for (let i = 0; i < left.length; i += 1) {
            if (left[i] !== right[i]) {
                return false;
            }
        }

        return true;
    }

    function notifyPermissionsRefreshed(list) {
        $(document).trigger("zaaer:permissions-refreshed", [list]);
    }

    function refreshSessionToken() {
        const deferred = $.Deferred();

        $.ajax({
            url: "/api/Auth/refresh-session",
            method: "POST",
            contentType: "application/json",
            dataType: "json",
            headers: buildHeaders()
        })
            .done((response) => {
                applyAuthResponse(response, { skipAutoRefresh: true });
                deferred.resolve(response);
            })
            .fail(function (xhr) {
                if (xhr && xhr.status === 401 && getRefreshToken()) {
                    tryRefreshAccessToken()
                        .done(deferred.resolve)
                        .fail(function () {
                            handleAuthFailure(xhr);
                            deferred.reject(xhr);
                        });
                    return;
                }

                if (xhr && xhr.status !== 0) {
                    handleAuthFailure(xhr);
                }
                deferred.reject(xhr);
            });

        return deferred.promise();
    }

    function refreshPermissions(options) {
        if (permissionRefreshInFlight) {
            return permissionRefreshInFlight;
        }

        if (!getToken()) {
            return $.Deferred().resolve(getEffectivePermissions()).promise();
        }

        permissionRefreshInFlight = $.ajax({
            url: "/api/Auth/my-permissions",
            method: "GET",
            dataType: "json",
            headers: buildHeaders(),
            timeout: 30000
        })
            .then((data) => {
                permissionRefreshFailCount = 0;

                const list = normalizePermissionList(
                    Array.isArray(data) ? data : (data && data.permissions)
                );
                const previous = getEffectivePermissions();
                const changed = !permissionListsEqual(list, previous);

                setPermissionCache(list);
                notifyPermissionsRefreshed(list);

                const forceSessionRefresh = options && options.forceSessionRefresh;
                if (changed || forceSessionRefresh) {
                    return refreshSessionToken()
                        .then(() => list)
                        .catch(() => list);
                }

                return list;
            })
            .fail((xhr) => {
                if (!xhr || xhr.status === 0) {
                    return getEffectivePermissions();
                }

                permissionRefreshFailCount += 1;

                if (xhr.status === 401) {
                    handleAuthFailure(xhr);
                }

                return getEffectivePermissions();
            })
            .always(() => {
                permissionRefreshInFlight = null;
            });

        return permissionRefreshInFlight;
    }

    function startPermissionAutoRefresh(options) {
        if (permissionAutoRefreshTimer) {
            window.clearInterval(permissionAutoRefreshTimer);
            permissionAutoRefreshTimer = null;
        }

        const intervalMs = (options && options.intervalMs) || PERMISSION_REFRESH_INTERVAL_MS;

        function tick() {
            if (!getToken()) {
                return;
            }

            if (permissionRefreshFailCount >= 5) {
                return;
            }

            refreshPermissions();
        }

        if (!permissionVisibilityBound) {
            permissionVisibilityBound = true;
            document.addEventListener("visibilitychange", function onVis() {
                if (document.visibilityState === "visible" && permissionRefreshFailCount < 4) {
                    window.setTimeout(tick, 1500);
                }
            });
        }

        permissionAutoRefreshTimer = window.setInterval(tick, intervalMs);
        window.setTimeout(tick, 3000);
    }

    function getJwtRoles() {
        const payload = decodeTokenPayload();
        if (!payload) {
            return [];
        }

        if (typeof payload.roles === "string" && payload.roles.length) {
            return payload.roles.split(",").map((x) => x.trim()).filter(Boolean);
        }

        return [];
    }

    function login(body) {
        const payload = Object.assign({}, body || {}, {
            deviceId: getOrCreateDeviceId(),
            deviceName: getDeviceName()
        });

        return $.ajax({
            url: "/api/Auth/login",
            method: "POST",
            contentType: "application/json",
            dataType: "json",
            data: JSON.stringify(payload)
        }).done((response) => {
            applyAuthResponse(response);
        });
    }

    function logout() {
        return $.ajax({
            url: "/api/Auth/logout",
            method: "POST",
            contentType: "application/json",
            dataType: "json",
            headers: buildHeaders()
        }).always(() => {
            clearToken();
        });
    }

    function applyAuthResponse(response, options) {
        if (response && response.token) {
            setToken(response.token);
            if (response.refreshToken) {
                setRefreshToken(response.refreshToken);
            }
            if (response.tenantCode) {
                window.localStorage.setItem(storageKeys.hotelCode, response.tenantCode);
            }
            if (response.tenantName) {
                window.localStorage.setItem(storageKeys.hotelName, response.tenantName);
            }
            if (response.tenantNameEn) {
                window.localStorage.setItem(storageKeys.hotelNameEn, response.tenantNameEn);
            } else {
                window.localStorage.removeItem(storageKeys.hotelNameEn);
            }
            if (response.fullName) {
                window.localStorage.setItem(storageKeys.userDisplayName, response.fullName);
            } else if (response.username) {
                window.localStorage.setItem(storageKeys.userDisplayName, response.username);
            }
            if (response.fullNameEn) {
                window.localStorage.setItem(storageKeys.userDisplayNameEn, response.fullNameEn);
            } else {
                window.localStorage.removeItem(storageKeys.userDisplayNameEn);
            }

            if (Array.isArray(response.permissions)) {
                setPermissionCache(response.permissions);
                notifyPermissionsRefreshed(permissionCache);
            } else {
                setPermissionCache(getJwtPermissions());
            }

            if (response.landingUrl) {
                window.localStorage.setItem(storageKeys.landingUrl, response.landingUrl);
            } else {
                window.localStorage.removeItem(storageKeys.landingUrl);
            }

            if (Array.isArray(response.gateStations)) {
                window.localStorage.setItem(storageKeys.gateStations, JSON.stringify(response.gateStations));
            } else {
                window.localStorage.removeItem(storageKeys.gateStations);
            }

            if (!options || !options.skipAutoRefresh) {
                startPermissionAutoRefresh({ intervalMs: PERMISSION_REFRESH_INTERVAL_MS });
            }
        }
    }

    function getHotelName() {
        return window.localStorage.getItem(storageKeys.hotelName) || "";
    }

    function getHotelNameEn() {
        return window.localStorage.getItem(storageKeys.hotelNameEn) || "";
    }

    function getUserDisplayName() {
        return window.localStorage.getItem(storageKeys.userDisplayName) || "";
    }

    function getUserDisplayNameEn() {
        return window.localStorage.getItem(storageKeys.userDisplayNameEn) || "";
    }

    function getLocalizedHotelLabel(hotels) {
        const isArabic = isUiArabic();
        const code = getHotelCode();

        if (code && Array.isArray(hotels)) {
            const row = hotels.find((h) => (h.code || h.Code) === code);
            if (row) {
                if (isArabic) {
                    return ((row.name || row.Name || "") + "").trim() || code;
                }

                const codeLabel = ((row.code || row.Code || code) + "").trim();
                const nameEn = ((row.nameEn || row.NameEn || "") + "").trim();
                return codeLabel || nameEn || ((row.name || row.Name || "") + "").trim();
            }
        }

        if (isArabic) {
            const stored = getHotelName();
            return (stored && stored.trim()) || code || "";
        }

        if (code) {
            return code;
        }

        const storedEn = getHotelNameEn();
        if (storedEn && storedEn.trim()) {
            return storedEn.trim();
        }

        return getHotelName() || "";
    }

    function getLocalizedUserDisplayName() {
        if (isUiArabic()) {
            const stored = getUserDisplayName();
            if (stored && stored.trim()) {
                return stored.trim();
            }

            const payload = decodeTokenPayload();
            return (payload && (payload.username || payload.Username)) || "";
        }

        const storedEn = getUserDisplayNameEn();
        if (storedEn && storedEn.trim()) {
            return storedEn.trim();
        }

        const payload = decodeTokenPayload();
        return (payload && (payload.username || payload.Username)) || getUserDisplayName() || "";
    }

    function isUiArabic() {
        const loc = (window.Zaaer && window.Zaaer.LocalizationService) || window.LocalizationService;
        if (loc && typeof loc.isArabic === "function") {
            return loc.isArabic();
        }
        if (loc && typeof loc.currentCulture === "function") {
            return loc.currentCulture() === "ar";
        }

        return (document.documentElement.lang || "").toLowerCase().startsWith("ar")
            || document.documentElement.dir === "rtl";
    }

    function normalizePickerHotels(hotels) {
        return (hotels || [])
            .map((h) => {
                const code = String(h.code || h.Code || "").trim();
                const name = String(h.name || h.Name || "").trim();
                const nameEn = String(h.nameEn || h.NameEn || "").trim();
                const searchText = [code, name, nameEn].filter(Boolean).join(" ").toLowerCase();
                return Object.assign({}, h, { code, name, nameEn, searchText });
            })
            .filter((h) => h.code);
    }

    function hotelRowLabel(row, isArabic) {
        if (!row) {
            return "";
        }

        const name = (row.name || row.Name || "").trim();
        const nameEn = (row.nameEn || row.NameEn || "").trim();
        const code = (row.code || row.Code || "").trim();
        if (isArabic) {
            return name || code;
        }

        return code || nameEn || name;
    }

    function hotelPickerSearchFilterExpression(filterValue) {
        const term = String(filterValue || "")
            .trim()
            .toLowerCase();
        if (!term) {
            return null;
        }

        return ["searchText", "contains", term];
    }

    /**
     * Display label for the active tenant: stored name, then hotel-codes list, then code.
     */
    function resolveActiveHotelLabel(hotels) {
        const label = getLocalizedHotelLabel(hotels);
        const code = getHotelCode();

        if (code && Array.isArray(hotels)) {
            const row = hotels.find((h) => (h.code || h.Code) === code);
            if (row) {
                if (isUiArabic() && (row.name || row.Name)) {
                    window.localStorage.setItem(storageKeys.hotelName, (row.name || row.Name).trim());
                }

                const nameEn = (row.nameEn || row.NameEn || "").trim();
                if (!isUiArabic() && nameEn) {
                    window.localStorage.setItem(storageKeys.hotelNameEn, nameEn);
                }
            }
        }

        return label;
    }

    function switchHotel(body) {
        return $.ajax({
            url: "/api/Auth/switch-hotel",
            method: "POST",
            contentType: "application/json",
            dataType: "json",
            data: JSON.stringify(body || {}),
            headers: buildHeaders()
        }).done((response) => {
            applyAuthResponse(response);
        }).fail(handleAuthFailure);
    }

    function ajaxJson(method, url, body, internal) {
        const deferred = $.Deferred();

        $.ajax({
            url,
            method,
            contentType: "application/json",
            dataType: "json",
            data: body !== undefined ? JSON.stringify(body) : undefined,
            headers: buildHeaders()
        })
            .done(deferred.resolve)
            .fail(function onAjaxJsonFail(xhr) {
                if (xhr && xhr.status === 401 && getRefreshToken() && !internal) {
                    tryRefreshAccessToken()
                        .then(function () {
                            return ajaxJson(method, url, body, true);
                        })
                        .done(deferred.resolve)
                        .fail(function () {
                            handleAuthFailure(xhr);
                            deferred.reject(xhr);
                        });
                    return;
                }

                handleAuthFailure(xhr);
                deferred.reject(xhr);
            });

        return deferred.promise();
    }

    function withQuery(endpoint, params) {
        const effectiveParams = withHotelCodeParam(params);
        if (!effectiveParams || Object.keys(effectiveParams).length === 0) {
            return endpoint;
        }

        const qs = $.param(effectiveParams);
        if (!qs) {
            return endpoint;
        }

        const sep = endpoint.indexOf("?") >= 0 ? "&" : "?";
        return `${endpoint}${sep}${qs}`;
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.ApiService = {
        storageKeys,
        login,
        logout,
        setToken,
        getToken,
        clearToken,
        getRefreshToken,
        tryRefreshAccessToken,
        getOrCreateDeviceId,
        requireToken,
        decodeTokenPayload,
        getJwtPermissions,
        getEffectivePermissions,
        refreshPermissions,
        refreshSessionToken,
        startPermissionAutoRefresh,
        hasPermission,
        ensurePermissionsReady,
        resolveLandingUrl,
        getLandingUrl,
        getGateStations,
        debugPermissions() {
            return {
                cached: getEffectivePermissions(),
                jwt: getJwtPermissions(),
                hasCancelReceipt: hasPermission("payments.cancel")
            };
        },
        getJwtRoles,
        switchHotel,
        applyAuthResponse,
        getHotelCode,
        getHotelName,
        getHotelNameEn,
        getUserDisplayName,
        getUserDisplayNameEn,
        getLocalizedHotelLabel,
        getLocalizedUserDisplayName,
        resolveActiveHotelLabel,
        isUiArabic,
        setHotelCode(hotelCode) {
            if (hotelCode) {
                window.localStorage.setItem(storageKeys.hotelCode, hotelCode);
            }
        },
        get(endpoint, params) {
            return request("GET", endpoint, params || {});
        },
        post(endpoint, body, params) {
            return ajaxJson("POST", withQuery(endpoint, params || {}), body);
        },
        put(endpoint, body, params) {
            return ajaxJson("PUT", withQuery(endpoint, params || {}), body);
        },
        patch(endpoint, body, params) {
            return ajaxJson("PATCH", withQuery(endpoint, params || {}), body);
        },
        delete(endpoint, params) {
            return ajaxJson("DELETE", withQuery(endpoint, params || {}));
        },
        getBlob(endpoint, options) {
            options = options || {};
            return new Promise(function (resolve, reject) {
                $.ajax({
                    url: endpoint,
                    method: "GET",
                    headers: buildHeaders(),
                    xhrFields: { responseType: "blob" },
                    processData: false,
                    timeout: options.timeoutMs || 0
                })
                    .done(function (data, _textStatus, jqXHR) {
                        if (jqXHR && jqXHR.status >= 400) {
                            reject(jqXHR);
                            return;
                        }
                        resolve(data);
                    })
                    .fail(function (jqXHR) {
                        handleAuthFailure(jqXHR);
                        reject(jqXHR);
                    });
            });
        },
        openBlobInNewTab(blob, revokeDelayMs) {
            const url = URL.createObjectURL(blob);
            const win = window.open(url, "_blank", "noopener,noreferrer");
            if (revokeDelayMs !== 0) {
                window.setTimeout(function () {
                    URL.revokeObjectURL(url);
                }, revokeDelayMs == null ? 60000 : revokeDelayMs);
            }
            return win;
        },
        postForm(endpoint, formData, params) {
            return $.ajax({
                url: withQuery(endpoint, params || {}),
                method: "POST",
                data: formData,
                processData: false,
                contentType: false,
                dataType: "json",
                headers: buildHeaders()
            }).fail(handleAuthFailure);
        },
        putForm(endpoint, formData, params) {
            return $.ajax({
                url: withQuery(endpoint, params || {}),
                method: "PUT",
                data: formData,
                processData: false,
                contentType: false,
                dataType: "json",
                headers: buildHeaders()
            }).fail(handleAuthFailure);
        }
    };

    function tenantDisplayName(item, isArabic) {
        return hotelRowLabel(item, isArabic);
    }

    function dedupeTenantIds(ids) {
        return [...new Set((ids || []).map((x) => Number(x)).filter((x) => x > 0))];
    }

    function tenantIdsFromHotels(hotels) {
        return dedupeTenantIds(
            (hotels || []).map((h) => h.tenantId || h.TenantId)
        );
    }

    function buildMultiHotelPickerColumns(translate, isArabicFn) {
        return [
            {
                caption: translate("roomBoard.hotelName") || "Hotel",
                calculateCellValue(row) {
                    return hotelRowLabel(row, isArabicFn());
                },
                calculateFilterExpression: hotelPickerSearchFilterExpression
            },
            {
                dataField: "searchText",
                visible: false,
                showInColumnChooser: false,
                allowSorting: false,
                allowFiltering: false
            }
        ];
    }

    function buildSingleHotelPickerColumns(translate, isArabicFn) {
        const iconCol = {
            caption: "",
            width: 44,
            alignment: "center",
            allowFiltering: false,
            allowSorting: false,
            cellTemplate(container) {
                $("<span />")
                    .addClass("dx-icon dx-icon-home pms-hotel-picker-row-icon")
                    .attr("aria-hidden", "true")
                    .appendTo(container);
            }
        };

        const searchCol = {
            dataField: "searchText",
            visible: false,
            showInColumnChooser: false,
            allowSorting: false,
            allowFiltering: false
        };

        if (isArabicFn()) {
            return [
                iconCol,
                {
                    caption: translate("roomBoard.hotelName") || "Hotel",
                    calculateCellValue(row) {
                        return hotelRowLabel(row, true);
                    },
                    calculateFilterExpression: hotelPickerSearchFilterExpression
                },
                searchCol
            ];
        }

        return [
            iconCol,
            {
                dataField: "code",
                caption: translate("roomBoard.hotelCodeShort") || "Code",
                width: 140,
                calculateFilterExpression: hotelPickerSearchFilterExpression
            },
            searchCol
        ];
    }

    const MOBILE_HOTEL_PICKER_MQ = "(max-width: 900px)";

    function isMobileHotelPickerLayout() {
        return window.matchMedia(MOBILE_HOTEL_PICKER_MQ).matches;
    }

    function hotelPickerDropDownWidth() {
        return Math.min(420, Math.max(280, window.innerWidth - 16));
    }

    function hotelPickerGridSearchWidth() {
        return Math.min(260, Math.max(180, window.innerWidth - 56));
    }

    function hotelPickerGridViewportHeight() {
        const vh = window.innerHeight;
        if (isMobileHotelPickerLayout()) {
            return Math.min(360, Math.max(200, Math.floor(vh * 0.55) - 80));
        }

        return Math.min(400, Math.max(220, Math.floor(vh * 0.5) - 120));
    }

    function buildHotelPickerDropDownOptions(headerMode, translate) {
        return {
            container: document.body,
            width: hotelPickerDropDownWidth(),
            height: "auto",
            maxHeight: isMobileHotelPickerLayout() ? "78vh" : "72vh",
            showTitle: !headerMode,
            title: translate("roomBoard.hotelPickerTitle") || translate("roomBoard.hotelCode"),
            showCloseButton: !headerMode,
            hideOnOutsideClick: true,
            hideOnParentScroll: true,
            shading: false,
            wrapperAttr: { class: "pms-hotel-picker-dropdown pms-hotel-picker-dropdown--instant" }
        };
    }

    function createSingleHotelLookup($host, options) {
        const hotels = normalizePickerHotels(options.dataSource || []);
        const isArabicFn = typeof options.isArabic === "function" ? options.isArabic : isUiArabic;
        const translate = options.t || ((k) => k);
        const rowHeight = options.rowHeight || 52;
        const headerMode = options.headerMode === true;
        let gridInstance = null;
        let outsideCloseHandler = null;

        function dropDownInstance() {
            try {
                return $host.dxDropDownBox("instance") || null;
            } catch {
                return null;
            }
        }

        function isInsideHotelPickerUi(target) {
            if (!target) {
                return false;
            }

            const el = target.nodeType === 1 ? target : target.parentElement;
            if (!el) {
                return false;
            }

            if ($host[0] && ($host[0] === el || $host[0].contains(el))) {
                return true;
            }

            return !!(
                el.closest(".pms-hotel-picker-dropdown") ||
                el.closest(".dx-header-filter-menu") ||
                el.closest(".dx-datagrid-filter-panel")
            );
        }

        function bindOutsideCloseHandler() {
            unbindOutsideCloseHandler();
            outsideCloseHandler = (e) => {
                const box = dropDownInstance();
                if (!box || !box.option("opened")) {
                    return;
                }

                if (isInsideHotelPickerUi(e.target)) {
                    return;
                }

                box.close();
            };

            document.addEventListener("pointerdown", outsideCloseHandler, true);
        }

        function unbindOutsideCloseHandler() {
            if (!outsideCloseHandler) {
                return;
            }

            document.removeEventListener("pointerdown", outsideCloseHandler, true);
            outsideCloseHandler = null;
        }

        function syncPickerDropDownWidth() {
            const box = dropDownInstance();
            if (!box) {
                return;
            }

            const dropDown = box.option("dropDownOptions") || {};
            box.option("dropDownOptions", {
                ...dropDown,
                ...buildHotelPickerDropDownOptions(headerMode, translate),
                width: hotelPickerDropDownWidth()
            });
        }

        function syncGridSelection(code) {
            if (!gridInstance || !code) {
                return;
            }

            gridInstance.selectRows([code], false);
        }

        function applyConfirmedHotel(code) {
            const box = dropDownInstance();
            if (!box || !code) {
                return;
            }

            const previous = box.option("value");
            if (code === previous) {
                return;
            }

            box.option("value", code);
            if (typeof options.onValueChanged === "function") {
                options.onValueChanged({ value: code, previousValue: previous });
            }
        }

        function pickHotelAndClose(code) {
            if (!code) {
                return;
            }

            applyConfirmedHotel(code);
            const box = dropDownInstance();
            if (box) {
                box.close();
            }
        }

        return $host.dxDropDownBox({
            label: headerMode ? "" : options.label || "",
            labelMode: options.labelMode || (headerMode ? "hidden" : "floating"),
            hint: options.hint || "",
            valueExpr: "code",
            displayExpr: (item) => tenantDisplayName(item, isArabicFn()),
            dataSource: hotels,
            value: options.value || null,
            width: options.width || (headerMode ? 220 : "100%"),
            applyValueMode: "instantly",
            openOnFieldClick: true,
            showClearButton: options.showClearButton === true,
            rtlEnabled: options.rtlEnabled,
            buttons: [
                {
                    name: "hotel",
                    location: "before",
                    options: {
                        icon: "home",
                        stylingMode: "text",
                        disabled: true,
                        tabIndex: -1,
                        hint: translate("roomBoard.hotelCode") || "Hotel"
                    }
                }
            ],
            dropDownOptions: buildHotelPickerDropDownOptions(headerMode, translate),
            contentTemplate() {
                const $gridHost = $("<div />").addClass("pms-hotel-picker-grid-host");
                const gridViewportHeight = hotelPickerGridViewportHeight();

                const po = window.Zaaer.PmsGridOptions;
                gridInstance = $gridHost.dxDataGrid(
                    po.merge(po.baseline(), {
                        dataSource: hotels,
                        keyExpr: "code",
                        height: gridViewportHeight,
                        rowHeight,
                        showColumnHeaders: false,
                        hoverStateEnabled: true,
                        focusedRowEnabled: true,
                        selection: { mode: "single" },
                        searchPanel: { visible: true, width: hotelPickerGridSearchWidth() },
                        elementAttr: { class: "pms-hotel-picker-grid" },
                        paging: { pageSize: 50 },
                        pager: {
                            visible: hotels.length > 50,
                            displayMode: "compact",
                            showInfo: true,
                            showNavigationButtons: true
                        },
                        columns: buildSingleHotelPickerColumns(translate, isArabicFn),
                        onRowClick(e) {
                            const code = e.data && (e.data.code || e.data.Code);
                            if (code) {
                                pickHotelAndClose(code);
                            }
                        }
                    })
                ).dxDataGrid("instance");

                $host.data("pmsHotelPickerGrid", gridInstance);

                const openedBox = dropDownInstance();
                syncGridSelection(openedBox && openedBox.option("value"));

                return $gridHost;
            },
            onOpened() {
                syncPickerDropDownWidth();
                bindOutsideCloseHandler();
                const box = dropDownInstance();
                syncGridSelection(box && box.option("value"));
            },
            onClosed() {
                unbindOutsideCloseHandler();
            }
        }).dxDropDownBox("instance");

        const mq = window.matchMedia(MOBILE_HOTEL_PICKER_MQ);
        const onLayoutChange = () => syncPickerDropDownWidth();
        $(window).on("resize.pmsHotelPicker orientationchange.pmsHotelPicker", onLayoutChange);
        if (typeof mq.addEventListener === "function") {
            mq.addEventListener("change", onLayoutChange);
        } else if (typeof mq.addListener === "function") {
            mq.addListener(onLayoutChange);
        }

        return dropDownInstance();
    }

    function createMultiHotelLookup($host, options) {
        const hotels = normalizePickerHotels(options.dataSource || []);
        const isArabicFn = typeof options.isArabic === "function" ? options.isArabic : () => false;
        const translate = options.t || ((k) => k);
        const showBulkActions = options.showSelectAll === true;
        let selectedIds = dedupeTenantIds(options.value);
        let gridInstance = null;
        let syncingSelection = false;

        function sameTenantIds(a, b) {
            const left = dedupeTenantIds(a);
            const right = dedupeTenantIds(b);
            if (left.length !== right.length) {
                return false;
            }

            return left.every((id, idx) => id === right[idx]);
        }

        function applySelection(ids, optionsSync) {
            const next = dedupeTenantIds(ids);
            if (sameTenantIds(next, selectedIds)) {
                return;
            }

            selectedIds = next;
            syncValue();

            if (!optionsSync && gridInstance) {
                syncingSelection = true;
                try {
                    gridInstance.selectRows(selectedIds, false);
                } finally {
                    syncingSelection = false;
                }
            }

            if (typeof options.onValueChanged === "function") {
                options.onValueChanged(selectedIds.slice());
            }
        }

        function syncValue() {
            const box = $host.dxDropDownBox("instance");
            if (box) {
                box.option("value", selectedIds.slice());
            }
        }

        function labelForIds(ids) {
            return dedupeTenantIds(ids)
                .map((id) => {
                    const row = hotels.find((h) => (h.tenantId || h.TenantId) === id);
                    return row ? tenantDisplayName(row, isArabicFn()) : String(id);
                })
                .join(isArabicFn() ? " ، " : ", ");
        }

        let dropDownInstance = null;

        dropDownInstance = $host.dxDropDownBox({
            label: options.label || "",
            labelMode: options.labelMode || "floating",
            valueExpr: "tenantId",
            displayExpr: () => labelForIds(selectedIds),
            dataSource: selectedIds,
            value: selectedIds,
            openOnFieldClick: true,
            rtlEnabled: isArabicFn(),
            width: options.width || "100%",
            dropDownOptions: {
                shading: false,
                hideOnOutsideClick: true,
                hideOnParentScroll: true,
                width: Math.min(480, Math.max(300, window.innerWidth - 24))
            },
            contentTemplate() {
                const $wrap = $("<div class='pms-hotel-multi-picker' />");

                if (showBulkActions && hotels.length) {
                    const $toolbar = $("<div class='pms-hotel-multi-picker__toolbar' />").appendTo($wrap);
                    $("<div />")
                        .appendTo($toolbar)
                        .dxButton({
                            text: translate("rbac.users.selectAllHotels") || "All hotels",
                            icon: "selectall",
                            stylingMode: "contained",
                            type: "default",
                            onClick() {
                                applySelection(tenantIdsFromHotels(hotels));
                            }
                        });
                    $("<div />")
                        .appendTo($toolbar)
                        .dxButton({
                            text: translate("rbac.users.clearHotels") || "Clear",
                            icon: "clear",
                            stylingMode: "outlined",
                            onClick() {
                                applySelection([]);
                            }
                        });
                }

                const $grid = $("<div />").appendTo($wrap);
                const po = window.Zaaer.PmsGridOptions;
                gridInstance = $grid
                    .dxDataGrid(
                        po.merge(po.baseline(), {
                            dataSource: hotels,
                            keyExpr: "tenantId",
                            height: Math.min(420, 120 + hotels.length * 40),
                            rowHeight: 40,
                            selectedRowKeys: selectedIds,
                            rtlEnabled: isArabicFn(),
                            elementAttr: { class: "pms-hotel-picker-grid pms-grid-compact" },
                            selection: { mode: "multiple", showCheckBoxesMode: "always" },
                            searchPanel: { visible: true, width: 260 },
                            headerFilter: { visible: true, search: { enabled: true } },
                            paging: { pageSize: 50 },
                            pager: {
                                visible: hotels.length > 50,
                                displayMode: "compact",
                                showInfo: true,
                                showNavigationButtons: true
                            },
                            columns: buildMultiHotelPickerColumns(translate, isArabicFn),
                            onInitialized(e) {
                                gridInstance = e.component;
                            },
                            onSelectionChanged(e) {
                                if (syncingSelection) {
                                    return;
                                }

                                applySelection(e.selectedRowKeys, true);
                            }
                        })
                    )
                    .dxDataGrid("instance");

                return $wrap;
            },
            onValueChanged(e) {
                if (Array.isArray(e.value)) {
                    selectedIds = dedupeTenantIds(e.value);
                }
            }
        }).dxDropDownBox("instance");

        dropDownInstance.setTenantIds = function (ids) {
            applySelection(ids);
        };

        dropDownInstance.getTenantIds = function () {
            return selectedIds.slice();
        };

        return dropDownInstance;
    }

    function refreshHeaderHotelPickerCulture($host) {
        if (!$host || !$host.length) {
            return;
        }

        let picker;
        try {
            picker = $host.dxDropDownBox("instance");
        } catch {
            picker = null;
        }

        if (!picker) {
            return;
        }

        const hotels = normalizePickerHotels(picker.option("dataSource") || []);
        const isArabicFn = () => isUiArabic();
        const translate = (key) => {
            const loc = window.Zaaer && window.Zaaer.LocalizationService;
            return loc && typeof loc.t === "function" ? loc.t(key) : key;
        };

        picker.option({
            rtlEnabled: isArabicFn(),
            displayExpr: (item) => tenantDisplayName(item, isArabicFn()),
            dataSource: hotels
        });

        const gridInstance = $host.data("pmsHotelPickerGrid");
        if (gridInstance) {
            gridInstance.option({
                rtlEnabled: isArabicFn(),
                columns: buildSingleHotelPickerColumns(translate, isArabicFn),
                dataSource: hotels
            });
        }
    }

    window.Zaaer.PmsHotelLookup = {
        tenantDisplayName,
        normalizePickerHotels,
        tenantIdsFromHotels,
        buildSingleHotelPickerColumns,
        buildMultiHotelPickerColumns,
        createSingleHotelLookup,
        createMultiHotelLookup,
        refreshHeaderHotelPickerCulture
    };

    if (getToken()) {
        startPermissionAutoRefresh({ intervalMs: PERMISSION_REFRESH_INTERVAL_MS });
    }
})(window, jQuery);
