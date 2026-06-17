(function (window, $) {
    "use strict";

    const cultureKey = "zaaer.ui.culture";
    const api = window.Zaaer && window.Zaaer.ApiService;
    const ticketService = window.Zaaer && window.Zaaer.ResortTicketService;
    const stationUtil = window.Zaaer && window.Zaaer.ResortTicketGateStation;

    function currentCulture() {
        const queryCulture = new URLSearchParams(window.location.search).get("culture");
        const value = (queryCulture || window.localStorage.getItem(cultureKey) || "ar").toLowerCase();
        return value === "en" ? "en" : "ar";
    }

    function initCulture() {
        const culture = currentCulture();
        const isAr = culture === "ar";
        window.localStorage.setItem(cultureKey, culture);
        document.documentElement.lang = isAr ? "ar-SA" : "en-US";
        document.documentElement.dir = isAr ? "rtl" : "ltr";
        return culture;
    }

    function t(key) {
        const dictionary = (window.ZaaerI18n && window.ZaaerI18n[currentCulture()]) || {};
        return dictionary[key] || key;
    }

    function applyI18n() {
        document.querySelectorAll("[data-i18n]").forEach((el) => {
            const key = el.getAttribute("data-i18n");
            if (key) {
                el.textContent = t(key);
            }
        });
        document.title = t("resortTickets.gate.title");
    }

    function gateReturnUrl() {
        const path = window.location.pathname + window.location.search;
        return encodeURIComponent(path || "/resort-ticket-gate.html");
    }

    function showForbidden() {
        $("#resortTicketGateHost").empty().append(
            $("<div/>")
                .addClass("resort-ticket-gate__forbidden")
                .append($("<h2/>").text(t("common.forbidden")))
                .append($("<p/>").text(t("resortTickets.gate.permissionHint")))
        );
    }

    function applyStationPwaMeta(stationCode, stationLabel, themeColor) {
        if (!stationCode) {
            return;
        }

        const hotelCode = api && api.getHotelCode ? api.getHotelCode() : "";
        const params = new URLSearchParams({ station: stationCode });
        if (hotelCode) {
            params.set("hotelCode", hotelCode);
        }

        const manifestHref = `/api/v1/pms/resort-tickets/gate/manifest?${params.toString()}`;
        let manifestLink = document.querySelector('link[rel="manifest"]');
        if (!manifestLink) {
            manifestLink = document.createElement("link");
            manifestLink.rel = "manifest";
            document.head.appendChild(manifestLink);
        }
        manifestLink.href = manifestHref;
        manifestLink.crossOrigin = "use-credentials";

        const icon192 = `/api/v1/pms/resort-tickets/gate/icon?station=${encodeURIComponent(stationCode)}&size=192`;
        const icon512 = `/api/v1/pms/resort-tickets/gate/icon?station=${encodeURIComponent(stationCode)}&size=512`;

        document.querySelectorAll('link[rel="icon"], link[rel="apple-touch-icon"]').forEach((el) => el.remove());

        const favicon = document.createElement("link");
        favicon.rel = "icon";
        favicon.type = "image/png";
        favicon.sizes = "192x192";
        favicon.href = icon192;
        document.head.appendChild(favicon);

        const apple = document.createElement("link");
        apple.rel = "apple-touch-icon";
        apple.href = icon512;
        document.head.appendChild(apple);

        const theme = themeColor || "#0f172a";
        let themeMeta = document.querySelector('meta[name="theme-color"]');
        if (!themeMeta) {
            themeMeta = document.createElement("meta");
            themeMeta.name = "theme-color";
            document.head.appendChild(themeMeta);
        }
        themeMeta.content = theme;

        if (stationLabel) {
            const appleTitle = document.querySelector('meta[name="apple-mobile-web-app-title"]');
            if (appleTitle) {
                appleTitle.content = stationLabel;
            }
        }
    }

    function findCachedStationMeta(stationCode) {
        if (!api || !api.getGateStations || !stationCode) {
            return null;
        }

        return (api.getGateStations() || []).find((row) => {
            const code = (row.stationCode || row.StationCode || "").toLowerCase();
            return code === stationCode.toLowerCase();
        });
    }

    function bootGate(stationCode, stationLabel) {
        const hotelName =
            (api.getHotelName && api.getHotelName()) ||
            window.localStorage.getItem("zaaer.tenant.hotelName") ||
            window.localStorage.getItem("zaaer.tenant.hotelCode") ||
            "—";
        $("#gateHotelName").text(hotelName);

        if (stationLabel) {
            $("#gateStationBadge").text(stationLabel).prop("hidden", false);
            document.title = `${t("resortTickets.gate.title")} · ${stationLabel}`;
        } else {
            $("#gateStationBadge").prop("hidden", true);
        }

        applyStationPwaMeta(
            stationCode,
            stationLabel,
            (findCachedStationMeta(stationCode) || {}).themeColor ||
                (findCachedStationMeta(stationCode) || {}).ThemeColor
        );

        $("#gateLogoutBtn").on("click", () => {
            if (api.clearToken) {
                api.clearToken();
            }
            window.location.href = `/login.html?returnUrl=${gateReturnUrl()}`;
        });

        if (window.Zaaer.ResortTicketScanAudio && window.Zaaer.ResortTicketScanAudio.prime) {
            document.body.addEventListener(
                "click",
                () => window.Zaaer.ResortTicketScanAudio.prime(),
                { once: true, capture: true }
            );
        }

        window.Zaaer.ResortTicketScannerPanel.mount("#resortTicketGateHost", {
            mode: "gate",
            t,
            stationCode,
            stationLabel
        });

        if (window.Zaaer.ResortTicketGatePwa && window.Zaaer.ResortTicketGatePwa.init) {
            window.Zaaer.ResortTicketGatePwa.init(t, { stationCode });
        }
    }

    function resolveStationLabel(stationCode, types) {
        if (!stationCode || !stationUtil) {
            return "";
        }
        return stationUtil.resolveStationLabel(stationCode, types, t, currentCulture() === "ar");
    }

    $(function () {
        initCulture();
        applyI18n();

        if (!api || !api.getToken()) {
            window.location.href = `/login.html?returnUrl=${gateReturnUrl()}`;
            return;
        }

        const stationCode = stationUtil ? stationUtil.readStationCode() : "";

        const ensure = api.ensurePermissionsReady ? api.ensurePermissionsReady() : $.when();
        ensure.always(() => {
            if (!api.hasPermission("resort_tickets.validate")) {
                showForbidden();
                return;
            }

            if (stationCode && ticketService && ticketService.listTypes) {
                ticketService
                    .listTypes()
                    .then((types) => {
                        bootGate(stationCode, resolveStationLabel(stationCode, types));
                    })
                    .catch(() => {
                        bootGate(stationCode, stationCode);
                    });
                return;
            }

            bootGate("", "");
        });
    });
})(window, jQuery);
