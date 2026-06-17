(function (window, $) {
    "use strict";

    const cultureKey = "zaaer.ui.culture";
    const api = window.Zaaer && window.Zaaer.ApiService;

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
        document.title = t("resortTickets.gateHome.title");
    }

    function gateHomeReturnUrl() {
        return encodeURIComponent(window.location.pathname + window.location.search);
    }

    function stationLabel(row) {
        const isAr = currentCulture() === "ar";
        const ar = (row.nameAr || row.NameAr || "").trim();
        const en = (row.nameEn || row.NameEn || "").trim();
        return isAr ? ar || en || row.stationCode : en || ar || row.stationCode;
    }

    function renderTiles(stations) {
        const $host = $("#gateHomeTiles").empty();
        if (!stations.length) {
            $("#gateHomeEmpty")
                .prop("hidden", false)
                .html(`<p>${t("resortTickets.gateHome.noStations")}</p>`);
            return;
        }

        $("#gateHomeEmpty").prop("hidden", true);

        stations.forEach((row) => {
            const code = row.stationCode || row.StationCode || "";
            const label = stationLabel(row);
            const gateUrl = row.gateUrl || row.GateUrl || `/resort-ticket-gate.html?station=${encodeURIComponent(code)}`;
            const iconUrl =
                row.iconUrl192 ||
                row.IconUrl192 ||
                `/api/v1/pms/resort-tickets/gate/icon?station=${encodeURIComponent(code)}&size=192`;
            const theme = row.themeColor || row.ThemeColor || "#0f172a";

            const $tile = $("<article/>")
                .addClass("resort-ticket-gate-home__tile")
                .attr("role", "listitem")
                .css("--gate-tile-accent", theme)
                .appendTo($host);

            $("<img/>")
                .addClass("resort-ticket-gate-home__tile-icon")
                .attr({ src: iconUrl, alt: label, width: 72, height: 72 })
                .appendTo($tile);

            $("<h2/>").addClass("resort-ticket-gate-home__tile-title").text(label).appendTo($tile);

            $("<p/>")
                .addClass("resort-ticket-gate-home__tile-code")
                .text(code)
                .appendTo($tile);

            const $actions = $("<div/>").addClass("resort-ticket-gate-home__tile-actions").appendTo($tile);

            $("<a/>")
                .addClass("resort-ticket-gate-home__tile-btn resort-ticket-gate-home__tile-btn--primary")
                .attr("href", gateUrl)
                .text(t("resortTickets.gateHome.openScanner"))
                .appendTo($actions);

            $("<a/>")
                .addClass("resort-ticket-gate-home__tile-btn")
                .attr("href", `${gateUrl}${gateUrl.indexOf("?") >= 0 ? "&" : "?"}install=1`)
                .text(t("resortTickets.gateHome.installHint"))
                .appendTo($actions);
        });
    }

    function loadStations() {
        const cached = api.getGateStations ? api.getGateStations() : [];
        if (cached.length) {
            renderTiles(cached);
        }

        return api
            .get("/api/v1/pms/resort-tickets/gate/my-stations")
            .then((response) => {
                const rows = (response && response.data) || [];
                if (Array.isArray(rows) && rows.length) {
                    renderTiles(rows);
                } else if (!cached.length) {
                    renderTiles([]);
                }
            })
            .catch(() => {
                if (!cached.length) {
                    renderTiles([]);
                }
            });
    }

    $(function () {
        initCulture();
        applyI18n();

        if (!api || !api.getToken()) {
            window.location.href = `/login.html?returnUrl=${gateHomeReturnUrl()}`;
            return;
        }

        const hotelName =
            (api.getHotelName && api.getHotelName()) ||
            window.localStorage.getItem("zaaer.tenant.hotelName") ||
            window.localStorage.getItem("zaaer.tenant.hotelCode") ||
            "—";
        $("#gateHomeHotelName").text(hotelName);

        $("#gateHomeLogoutBtn").on("click", () => {
            if (api.clearToken) {
                api.clearToken();
            }
            window.location.href = `/login.html?returnUrl=${gateHomeReturnUrl()}`;
        });

        const ensure = api.ensurePermissionsReady ? api.ensurePermissionsReady() : $.when();
        ensure.always(() => {
            if (!api.hasPermission("resort_tickets.validate")) {
                $("#gateHomeTiles").empty();
                $("#gateHomeEmpty")
                    .prop("hidden", false)
                    .html(`<p>${t("resortTickets.gate.permissionHint")}</p>`);
                return;
            }

            loadStations();
        });
    });
})(window, jQuery);
