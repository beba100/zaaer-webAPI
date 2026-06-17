(function (window, $, DevExpress) {
    "use strict";

    const state = {
        date: new Date(),
        viewMode: "cards",
        hotelCode: "",
        availableHotels: [],
        buildingIds: [],
        floorIds: [],
        roomTypeIds: [],
        status: "",
        statuses: [],
        guestColors: [],
        rentalTypes: [],
        alert: "",
        search: "",
        board: null,
        boardReadyHotelCode: "",
        isResort: false,
        isHall: false
    };

    const NAV_COLLAPSED_STORAGE_KEY = "zaaer.roomBoard.sidebarCollapsed";
    const UI_STATE_STORAGE_KEY = "zaaer.roomBoard.uiState.v1";

    let loadPanel;
    let boardLoadToken = 0;
    let boardLoadInProgress = false;
    let suppressHotelPickerSync = false;
    let schedulerInitialized = false;
    let gridInitialized = false;
    let suppressStatusTagChange = false;
    let suppressDateChange = false;
    let numberingPopup;
    let numberingPopupInitialized = false;

    const defaultDocumentTypes = [
        { docCode: "customer", label: "Customer", prefix: "GUS", padding: 4, sample: "GUS0001", includeHotel: false, usesZaaerId: true },
        { docCode: "reservation", label: "Reservation", prefix: "REV", padding: 4, sample: "REV0001", includeHotel: false, usesZaaerId: true },
        { docCode: "payment_receipt", label: "Payment receipt", prefix: "REC", padding: 4, sample: "REC0001", includeHotel: false, usesZaaerId: true },
        { docCode: "payment_refund", label: "Payment refund", prefix: "PAY", padding: 4, sample: "PAY0001", includeHotel: false, usesZaaerId: true },
        { docCode: "invoice", label: "Invoice", prefix: "INVO", padding: 4, sample: "INVO-16-0001", includeHotel: true, usesZaaerId: true },
        { docCode: "order", label: "Order", prefix: "ORD", padding: 4, sample: "ORD0001", includeHotel: false, usesZaaerId: true },
        { docCode: "credit_note", label: "Credit note", prefix: "CRED", padding: 4, sample: "CRED0001", includeHotel: false, usesZaaerId: true }
    ];

    function t(key) {
        return window.Zaaer.LocalizationService.t(key);
    }

    function roomBoardRoomTypeLabel() {
        if (state.isHall) {
            return t("property.venue.hallCategory");
        }
        return state.isResort ? t("roomBoard.chaletType") : t("roomBoard.roomType");
    }

    function roomBoardRoomTypeDropDownWidth() {
        return state.isResort ? 400 : 300;
    }

    function applyResortFilterLabels() {
        const $roomType = $("#roomTypeFilter");
        if (!$roomType.length || !$roomType.data("dxTagBox")) {
            return;
        }

        $roomType.dxTagBox("instance").option({
            label: roomBoardRoomTypeLabel(),
            dropDownOptions: {
                width: roomBoardRoomTypeDropDownWidth(),
                wrapperAttr: { class: "room-board-room-type-filter-dropdown" }
            }
        });
    }

    function getHotelPickerInstance($host) {
        if (!$host || !$host.length) {
            return null;
        }

        try {
            return $host.dxDropDownBox("instance") || null;
        } catch {
            return null;
        }
    }

    function getRefreshButtonInstance() {
        try {
            return $("#refreshButton").dxButton("instance") || null;
        } catch {
            return null;
        }
    }

    function resolveHotelLabel(code) {
        const normalized = `${code ?? ""}`.trim();
        if (!normalized) {
            return "";
        }

        const row = (state.availableHotels || []).find(
            (h) => `${h.code || h.Code || ""}`.trim() === normalized
        );
        if (row) {
            const api = window.Zaaer.ApiService;
            if (api && typeof api.resolveActiveHotelLabel === "function") {
                return api.resolveActiveHotelLabel([row]) || normalized;
            }
        }

        return normalized;
    }

    function setBoardChromeBusy(busy, hotelSwitch) {
        const blockPicker = !!hotelSwitch;
        if (blockPicker || !busy) {
            const picker = getHotelPickerInstance($("#pmsHeaderHotelPicker"));
            if (picker) {
                picker.option("disabled", blockPicker && !!busy);
            }
        }

        const refreshBtn = getRefreshButtonInstance();
        if (refreshBtn) {
            refreshBtn.option("disabled", !!busy);
        }

        $(".room-board-shell").toggleClass("room-board-shell--board-loading", !!busy && !!hotelSwitch);
    }

    function syncHotelPickerValue(code) {
        const normalized = `${code ?? ""}`.trim();
        suppressHotelPickerSync = true;
        try {
            const picker = getHotelPickerInstance($("#pmsHeaderHotelPicker"));
            if (picker && normalized && picker.option("value") !== normalized) {
                picker.option("value", normalized);
            }

            if (normalized) {
                window.Zaaer.ApiService.setHotelCode(normalized);
            }

            state.hotelCode = normalized;
            applyActiveHotelTitle();
        } finally {
            suppressHotelPickerSync = false;
        }
    }

    function revertHotelSelection(previousCode) {
        const prev = `${previousCode ?? ""}`.trim();
        if (!prev) {
            return;
        }

        syncHotelPickerValue(prev);
    }

    function setBoardSwitchOverlay(active, hotelLabel) {
        const $main = $("#roomBoardMain");
        if (!$main.length) {
            return;
        }

        let $overlay = $main.children(".room-board-switch-overlay");
        if (active) {
            $main.addClass("room-board-main--switching");
            if (!$overlay.length) {
                $overlay = $("<div>")
                    .addClass("room-board-switch-overlay")
                    .attr("role", "status")
                    .attr("aria-live", "polite")
                    .appendTo($main);
                const $card = $("<div>").addClass("room-board-switch-overlay-card").appendTo($overlay);
                $("<div>")
                    .addClass("room-board-switch-overlay-indicator")
                    .appendTo($card)
                    .dxLoadIndicator({ visible: true, height: 36, width: 36 });
                $("<p>").addClass("room-board-switch-overlay-text").appendTo($card);
            }

            $overlay.find(".room-board-switch-overlay-text").text(
                t("roomBoard.loadingHotelSwitch").replace("{hotel}", hotelLabel || "")
            );
            $overlay.prop("hidden", false);
            return;
        }

        $main.removeClass("room-board-main--switching");
        if ($overlay.length) {
            $overlay.prop("hidden", true);
        }
    }

    function applyActiveHotelTitle() {
        const api = window.Zaaer.ApiService;
        const label = api.resolveActiveHotelLabel(state.availableHotels) || t("roomBoard.title");
        document.title = `${label} — ${t("app.title")}`;

        const $pickerHost = $("#pmsHeaderHotelPicker");
        const picker = getHotelPickerInstance($pickerHost);
        if (picker) {
            const code = api.getHotelCode();
            if (code && picker.option("value") !== code) {
                picker.option("value", code);
            }
            return;
        }

        if ($pickerHost.length && $pickerHost.hasClass("pms-header-hotel-picker--breadcrumb")) {
            if (!$pickerHost.children().length || $pickerHost.find(".room-board-bc-current").length) {
                $pickerHost.empty().append($("<strong/>", { class: "room-board-bc-current" }).text(label));
            }
            return;
        }

        const $pageTitle = $("#pageTitle");
        if ($pageTitle.length) {
            $pageTitle.text(label);
        }
    }

    function syncRoomBoardStickyChrome() {
        const $chrome = $(".room-board-sticky-top");
        if (!$chrome.length) {
            return;
        }

        const height = Math.ceil($chrome.outerHeight(true) || 0);
        const topPx = height > 0 ? `${height}px` : "148px";
        document.documentElement.style.setProperty("--room-board-chrome-top", topPx);
    }

    function isArabic() {
        return window.Zaaer.LocalizationService.currentCulture() === "ar";
    }

    function persistRoomBoardUiState() {
        try {
            localStorage.setItem(
                UI_STATE_STORAGE_KEY,
                JSON.stringify({
                    viewMode: state.viewMode,
                    status: state.status || "",
                    statuses: Array.isArray(state.statuses) ? state.statuses : [],
                    guestColors: Array.isArray(state.guestColors) ? state.guestColors : [],
                    rentalTypes: Array.isArray(state.rentalTypes) ? state.rentalTypes : [],
                    alert: state.alert || ""
                })
            );
        } catch {
            /* storage unavailable */
        }
    }

    function restoreRoomBoardUiState() {
        try {
            const raw = localStorage.getItem(UI_STATE_STORAGE_KEY);
            if (!raw) {
                return;
            }

            const saved = JSON.parse(raw);
            if (
                saved.viewMode === "cards" ||
                saved.viewMode === "rack" ||
                saved.viewMode === "grid" ||
                saved.viewMode === "calendar"
            ) {
                state.viewMode = saved.viewMode;
            }

            state.status = typeof saved.status === "string" ? saved.status : "";
            state.statuses = Array.isArray(saved.statuses) ? saved.statuses.slice() : [];
            state.guestColors = Array.isArray(saved.guestColors)
                ? saved.guestColors.map(normalizeGuestColorKey).filter(Boolean)
                : [];
            state.rentalTypes = Array.isArray(saved.rentalTypes)
                ? saved.rentalTypes.map(normalizeRentalTypeFilterKey).filter(Boolean)
                : [];
            state.alert = typeof saved.alert === "string" ? saved.alert : "";
        } catch {
            /* ignore corrupt state */
        }
    }

    function applyRestoredUiToWidgets() {
        const viewGroup = $("#viewModeSwitch").dxButtonGroup("instance");
        if (viewGroup && state.viewMode) {
            viewGroup.option("selectedItemKeys", [state.viewMode]);
        }

        const statusFilter = $("#statusFilter").dxTagBox("instance");
        if (statusFilter) {
            suppressStatusTagChange = true;
            statusFilter.option("value", (state.statuses || []).slice());
            suppressStatusTagChange = false;
        }

        const colorFilter = $("#guestColorFilter").dxTagBox("instance");
        if (colorFilter) {
            colorFilter.option("value", (state.guestColors || []).slice());
        }

        const rentalFilter = $("#rentalTypeFilter").dxTagBox("instance");
        if (rentalFilter) {
            rentalFilter.option("value", (state.rentalTypes || []).slice());
        }
    }

    function normalizeRentalTypeFilterKey(value) {
        const x = `${value || ""}`.trim().toLowerCase();
        if (!x) {
            return "";
        }

        if (x === "mixed" || x.includes("mixed")) {
            return "mixed";
        }

        if (x.includes("month")) {
            return "monthly";
        }

        if (x.includes("year")) {
            return "yearly";
        }

        if (x.includes("hour") || x.includes("inhour")) {
            return "hourly";
        }

        return "daily";
    }

    function roomHasCurrentStayForRentalFilter(room) {
        const st = `${(room && room.operationalStatus) || ""}`.trim().toLowerCase();
        return st === "occupied" || st === "reserved";
    }

    function roomRentalFilterKey(room) {
        if (room && room.hasMixedRentalPeriods) {
            return "mixed";
        }

        return normalizeRentalTypeFilterKey(room && room.rentalType);
    }

    function rentalTypeFilterLabel(key, count) {
        const labelKey = `roomBoard.rentalType.${key}`;
        const label = t(labelKey);
        const name = label !== labelKey ? label : key;
        const countText = t("roomBoard.guestColorFilterCount").replace("{0}", String(count || 0));
        return { label: name, countText, text: `${name} (${countText})` };
    }

    function roomMatchesRentalTypeFilter(room) {
        if (!state.rentalTypes.length) {
            return true;
        }

        if (!roomHasCurrentStayForRentalFilter(room)) {
            return false;
        }

        return state.rentalTypes.includes(roomRentalFilterKey(room));
    }

    function roomMatchesBoardClientFilters(room) {
        return roomMatchesGuestColorFilter(room) && roomMatchesRentalTypeFilter(room);
    }

    function normalizeGuestColorKey(value) {
        if (value == null || value === "") {
            return "";
        }

        let s = String(value).trim().toUpperCase();
        if (!s) {
            return "";
        }

        if (!s.startsWith("#")) {
            s = `#${s}`;
        }

        if (/^#[0-9A-F]{3}$/.test(s)) {
            s = `#${s[1]}${s[1]}${s[2]}${s[2]}${s[3]}${s[3]}`;
        }

        return /^#[0-9A-F]{6}$/.test(s) ? s : "";
    }

    function roomMatchesGuestColorFilter(room) {
        if (!state.guestColors.length) {
            return true;
        }

        const back = normalizeGuestColorKey(room && room.occupiedGuestBackColor);
        return back && state.guestColors.includes(back);
    }

    function getDisplayRooms() {
        const rooms = (state.board && state.board.rooms) || [];
        if (!state.guestColors.length && !state.rentalTypes.length) {
            return rooms;
        }

        return rooms.filter(roomMatchesBoardClientFilters);
    }

    function getDisplayBoard() {
        if (!state.board) {
            return null;
        }

        return {
            ...state.board,
            rooms: getDisplayRooms()
        };
    }

    function buildGuestColorFilterOptions(rooms) {
        const counts = new Map();

        (rooms || []).forEach((room) => {
            const key = normalizeGuestColorKey(room && room.occupiedGuestBackColor);
            if (!key) {
                return;
            }

            if (!counts.has(key)) {
                counts.set(key, {
                    id: key,
                    backColor: key,
                    textColor: normalizeGuestColorKey(room.occupiedTextColor) || "",
                    count: 0
                });
            }

            counts.get(key).count += 1;
        });

        return Array.from(counts.values())
            .sort((a, b) => b.count - a.count || a.id.localeCompare(b.id))
            .map((row) => ({
                ...row,
                label: `${row.id} (${t("roomBoard.guestColorFilterCount").replace("{0}", String(row.count))})`
            }));
    }

    function refreshGuestColorFilterOptions() {
        const inst = $("#guestColorFilter").dxTagBox("instance");
        if (!inst) {
            return;
        }

        const options = buildGuestColorFilterOptions((state.board && state.board.rooms) || []);
        const validIds = new Set(options.map((o) => o.id));
        const nextValues = (state.guestColors || []).filter((id) => validIds.has(id));

        inst.option("dataSource", options);
        if (nextValues.length !== state.guestColors.length) {
            state.guestColors = nextValues;
        }

        inst.option("value", state.guestColors.slice());
        inst.option("disabled", options.length === 0);
    }

    function refreshRentalTypeFilterOptions() {
        const inst = $("#rentalTypeFilter").dxTagBox("instance");
        if (!inst) {
            return;
        }

        const counts = new Map();
        ((state.board && state.board.rooms) || []).forEach((room) => {
            if (!roomHasCurrentStayForRentalFilter(room)) {
                return;
            }

            const key = roomRentalFilterKey(room);
            counts.set(key, (counts.get(key) || 0) + 1);
        });

        const order = ["daily", "monthly", "mixed", "yearly", "hourly"];
        const options = order
            .filter((key) => counts.has(key))
            .map((key) => {
                const parts = rentalTypeFilterLabel(key, counts.get(key));
                return {
                    id: key,
                    label: parts.label,
                    countText: parts.countText,
                    text: parts.text
                };
            });

        const validIds = new Set(options.map((o) => o.id));
        const nextValues = (state.rentalTypes || []).filter((id) => validIds.has(id));

        inst.option("dataSource", options);
        if (nextValues.length !== state.rentalTypes.length) {
            state.rentalTypes = nextValues;
        }

        inst.option("value", state.rentalTypes.slice());
        inst.option("disabled", options.length === 0);
    }

    function applyBoardClientFiltersToView() {
        if (!state.board) {
            return;
        }

        renderActiveView();
    }

    function renderGuestColorFilterItem(itemData, _itemIndex, itemElement) {
        const data = itemData || {};
        const $root = $("<div>").addClass("room-board-color-filter-item").appendTo($(itemElement).empty());
        $("<span>")
            .addClass("room-board-color-filter-swatch")
            .css("background-color", data.backColor || data.id || "#e2e8f0")
            .appendTo($root);
        $("<span>").addClass("room-board-color-filter-label").text(data.label || data.id || "").appendTo($root);
    }

    function renderGuestColorFilterTag(itemData, _tagIndex, tagElement) {
        const data = itemData || {};
        const $tag = $("<div>").addClass("room-board-color-filter-tag").appendTo($(tagElement).empty());
        $("<span>")
            .addClass("room-board-color-filter-swatch room-board-color-filter-swatch--tag")
            .css("background-color", data.backColor || data.id || "#e2e8f0")
            .appendTo($tag);
        $("<span>").addClass("room-board-color-filter-tag-text").text(data.id || "").appendTo($tag);
    }

    function renderRentalTypeFilterItem(itemData, _itemIndex, itemElement) {
        const data = itemData || {};
        const $root = $("<div>").addClass("room-board-rental-filter-item").appendTo($(itemElement).empty());
        $("<span>").addClass("room-board-rental-filter-label").text(data.label || data.text || "").appendTo($root);
        if (data.countText) {
            $("<span>").addClass("room-board-rental-filter-count").text(data.countText).appendTo($root);
        }
    }

    function renderRentalTypeFilterTag(itemData, _tagIndex, tagElement) {
        const data = itemData || {};
        const $tag = $("<div>").addClass("room-board-rental-filter-tag").appendTo($(tagElement).empty());
        $("<span>").addClass("room-board-rental-filter-tag-label").text(data.label || data.text || "").appendTo($tag);
        if (data.countText) {
            $("<span>").addClass("room-board-rental-filter-tag-count").text(data.countText).appendTo($tag);
        }
    }

    function getCalendarBoardRange() {
        const from = new Date(state.date);
        // Light default window for fast load; navigation can move by month.
        from.setDate(from.getDate() - 7);
        from.setHours(0, 0, 0, 0);

        const to = new Date(state.date);
        to.setMonth(to.getMonth() + 3);

        to.setDate(to.getDate() + 7);
        return { from, to };
    }

    function getFilters() {
        const filters = {
            date: state.date,
            fromDate: state.date,
            toDate: new Date(state.date.getFullYear(), state.date.getMonth(), state.date.getDate() + 30),
            buildingIds: state.buildingIds.join(","),
            floorIds: state.floorIds.join(","),
            roomTypeIds: state.roomTypeIds.join(","),
            status: state.status,
            statuses: state.statuses.join(","),
            alert: state.alert,
            search: state.search,
            viewMode: state.viewMode
        };

        if (state.viewMode === "calendar") {
            const range = getCalendarBoardRange();
            filters.fromDate = range.from;
            filters.toDate = range.to;
        }

        return filters;
    }

    function applyStaticText() {
        $("[data-i18n]").each(function () {
            const key = $(this).attr("data-i18n");
            $(this).text(t(key));
        });
        $("#businessDateText").text(new Intl.DateTimeFormat("en-GB").format(state.date));
    }

    function uniqueOptions(rooms, idField, textField) {
        const seen = new Set();
        return (rooms || [])
            .filter((room) => room[idField] && room[textField])
            .map((room) => ({ id: room[idField], text: room[textField] }))
            .filter((item) => {
                if (seen.has(item.id)) {
                    return false;
                }
                seen.add(item.id);
                return true;
            })
            .sort((a, b) => `${a.text}`.localeCompare(`${b.text}`));
    }

    function appendSummaryPillIcon($iconHost, item) {
        if (item.iconKind === "broom") {
            $("<span>")
                .addClass("summary-pill-broom room-card-status-symbol room-card-symbol-broom")
                .attr("aria-hidden", "true")
                .appendTo($iconHost);
            return;
        }

        if (item.iconKind === "overstay") {
            const cardView = window.Zaaer && window.Zaaer.RoomCardView;
            if (cardView && typeof cardView.appendOverstaySymbol === "function") {
                cardView.appendOverstaySymbol($iconHost, "summary-pill-overstay");
                return;
            }

            $("<span>")
                .addClass("room-card-symbol-overstay summary-pill-overstay")
                .attr("aria-hidden", "true")
                .appendTo($iconHost);
            return;
        }

        $("<i>").addClass(`dx-icon dx-icon-${item.icon}`).appendTo($iconHost);
    }

    function isOccupiedSummaryFilterActive() {
        const statuses = state.statuses || [];
        return (
            state.status === "occupied" ||
            (statuses.includes("occupied") && statuses.includes("reserved"))
        );
    }

    function isSummaryFilterActive(item) {
        if (item.alert) {
            return (
                state.alert === item.alert &&
                !state.status &&
                !(state.statuses || []).length
            );
        }

        if (item.status === "occupied") {
            return isOccupiedSummaryFilterActive() && !state.alert;
        }

        if (item.status === "reserved") {
            return state.status === "reserved" && !(state.statuses || []).length && !state.alert;
        }

        if (!item.status) {
            return !state.status && !(state.statuses || []).length && !state.alert;
        }

        return state.status === item.status && !(state.statuses || []).length && !state.alert;
    }

    function applySummaryFilterClick(item) {
        state.guestColors = [];
        state.alert = item.alert || "";

        if (item.status === "occupied") {
            state.status = "";
            state.statuses = ["occupied", "reserved"];
        } else {
            state.status = item.status;
            state.statuses = [];
        }

        const statusFilter = $("#statusFilter").dxTagBox("instance");
        if (statusFilter) {
            suppressStatusTagChange = true;
            if (item.status === "occupied") {
                statusFilter.option("value", ["occupied", "reserved"]);
            } else {
                statusFilter.option("value", []);
            }
            suppressStatusTagChange = false;
        }

        const colorFilter = $("#guestColorFilter").dxTagBox("instance");
        if (colorFilter) {
            colorFilter.option("value", []);
        }

        persistRoomBoardUiState();
        loadBoard();
    }

    function appendSummaryPill($row, item, summary) {
        const isActive = isSummaryFilterActive(item);
        const $iconHost = $("<span>").addClass("summary-pill-icon");

        appendSummaryPillIcon($iconHost, item);

        $("<button>")
            .attr("type", "button")
            .attr("title", t(item.labelKey))
            .addClass(`summary-action summary-pill ${item.className}${isActive ? " is-active" : ""}`)
            .append(
                $("<span>")
                    .addClass("summary-pill-dot")
                    .append($("<span>").addClass("summary-pill-count").text(summary[item.field] || 0))
            )
            .append($("<span>").addClass("summary-pill-label").text(t(item.labelKey)))
            .append($iconHost)
            .on("click", () => applySummaryFilterClick(item))
            .appendTo($row);
    }

    function renderSummary(summary) {
        const statusItems = [
            { field: "total", labelKey: "summary.total", icon: "home", status: "", alert: "", className: "summary-pill-total" },
            { field: "available", labelKey: "summary.available", icon: "check", status: "available", alert: "", className: "status-available" },
            { field: "occupied", labelKey: "summary.occupied", icon: "user", status: "occupied", alert: "", className: "status-occupied" },
            { field: "reserved", labelKey: "summary.reserved", icon: "clock", status: "reserved", alert: "", className: "status-reserved" },
            { field: "cleaning", labelKey: "summary.cleaning", iconKind: "broom", status: "cleaning", alert: "", className: "status-cleaning" },
            { field: "maintenance", labelKey: "summary.maintenance", icon: "preferences", status: "maintenance", alert: "", className: "status-maintenance" }
        ];
        const alertItems = [
            { field: "departureToday", labelKey: "summary.departureToday", icon: "runner", status: "", alert: "departure-today", className: "summary-alert summary-departure" },
            { field: "overstay", labelKey: "summary.overstay", iconKind: "overstay", status: "", alert: "overstay", className: "summary-alert summary-overstay" },
            { field: "unpaidBalance", labelKey: "summary.unpaidBalance", icon: "money", status: "", alert: "unpaid-balance", className: "summary-alert" },
            { field: "occupiedDirty", labelKey: "summary.occupiedDirty", icon: "warning", status: "", alert: "occupied-dirty", className: "summary-alert summary-occupied-dirty" }
        ];

        const $summary = $("#roomBoardSummary").empty();
        const $row = $("<div>").addClass("summary-actions summary-actions--unified").appendTo($summary);

        statusItems.forEach((item) => appendSummaryPill($row, item, summary));

        $("<span>").addClass("summary-pill-separator").attr("aria-hidden", "true").appendTo($row);

        alertItems.forEach((item) => appendSummaryPill($row, item, summary));

        const total = summary.total || 0;
        const occupied = summary.occupied || 0;
        const occupancyRate = total > 0 ? Math.round((occupied / total) * 100) : 0;

        $("<div>")
            .addClass("summary-occupancy summary-occupancy-pill")
            .attr("title", t("summary.occupancyRate"))
            .append(
                $("<span>")
                    .addClass("summary-pill-dot summary-occupancy-dot")
                    .append($("<span>").addClass("summary-pill-count").text(`${occupancyRate}%`))
            )
            .append($("<span>").addClass("summary-pill-label").text(t("summary.occupancyRate")))
            .append(
                $("<span>")
                    .addClass("summary-pill-icon summary-occupancy-icon")
                    .append($("<i>").addClass("dx-icon dx-icon-chart"))
            )
            .appendTo($row);

        syncRoomBoardStickyChrome();
    }

    function syncContentToolbarVisibility() {
        const showToolbar = state.viewMode === "cards";
        const $toolbar = $(".room-board-content-toolbar");
        const $shell = $(".room-board-shell");
        $shell.removeClass(
            "room-board-shell--view-cards room-board-shell--view-rack room-board-shell--view-grid room-board-shell--view-calendar"
        );
        $shell.addClass(`room-board-shell--view-${state.viewMode || "cards"}`);
        $toolbar.toggle(showToolbar);
        if (!showToolbar) {
            $(".room-board-content-toolbar .room-board-actions").empty();
        }
    }

    function renderActiveView() {
        syncContentToolbarVisibility();

        // Use the DOM `hidden` property (not jQuery `.attr("hidden", false)`), otherwise some browsers
        // keep `hidden="false"` on the element — presence of the attribute still hides per HTML5.
        $(".room-board-panel").each(function () {
            this.hidden = true;
        });
        const activePanel = document.getElementById(`${state.viewMode}Panel`);
        if (activePanel) {
            activePanel.hidden = false;
        }

        const displayBoard = getDisplayBoard() || state.board;
        const displayRooms = getDisplayRooms();

        if (state.viewMode === "cards") {
            window.Zaaer.RoomCardView.render("#cardsPanel", displayRooms, t, {
                onColorsChanged: loadBoard
            });
        }

        if (state.viewMode === "rack") {
            window.Zaaer.RoomBoardRackView.render("#rackPanel", displayRooms, t, {
                onBoardRefresh: loadBoard,
                onOverstayFilter: () => {
                    applySummaryFilterClick({
                        field: "overstay",
                        labelKey: "summary.overstay",
                        status: "",
                        alert: "overstay",
                        className: "summary-alert summary-overstay"
                    });
                }
            });
        }

        if (state.viewMode === "grid") {
            if (!gridInitialized) {
                window.Zaaer.RoomBoardGrid.init("#gridPanel", getFilters, t, {
                    onBoardRefresh: loadBoard,
                    roomFilter: roomMatchesBoardClientFilters
                });
                gridInitialized = true;
            } else {
                window.Zaaer.RoomBoardGrid.refresh();
            }
        }

        if (state.viewMode === "calendar") {
            if (!schedulerInitialized) {
                window.Zaaer.RoomBoardScheduler.init("#calendarPanel", displayBoard, t, {
                    currentDate: state.date,
                    dateRange: getCalendarBoardRange(),
                    onBoardRefresh: loadBoard,
                    onNavigate: onCalendarNavigate
                });
                schedulerInitialized = true;
            } else {
                window.Zaaer.RoomBoardScheduler.update(displayBoard, state.date, getCalendarBoardRange());
            }
        }
    }

    function mapRoomTypeOptions(items) {
        const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;
        if (!labeler || typeof labeler.display !== "function") {
            return items;
        }

        return (items || []).map((item) => ({
            ...item,
            text: labeler.display(item.text, t)
        }));
    }

    function updateLookupFilters() {
        const rooms = state.board ? state.board.rooms : [];
        const lookups = state.board && state.board.lookups ? state.board.lookups : {};

        $("#buildingFilter").dxTagBox("instance").option("dataSource", lookups.buildings || uniqueOptions(rooms, "buildingId", "buildingName"));
        $("#floorFilter").dxTagBox("instance").option("dataSource", lookups.floors || uniqueOptions(rooms, "floorId", "floorName"));
        $("#roomTypeFilter").dxTagBox("instance").option(
            "dataSource",
            mapRoomTypeOptions(lookups.roomTypes || uniqueOptions(rooms, "roomTypeId", "roomTypeName"))
        );
    }

    function getCurrentHotelCode() {
        return state.hotelCode || window.Zaaer.ApiService.getHotelCode() || "";
    }

    function initNumberingSettingsPopup() {
        if (numberingPopupInitialized) {
            return;
        }

        numberingPopup = $("#numberingSettingsPopup").dxPopup({
            title: t("numbering.title"),
            width: Math.min(980, Math.max(360, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "78vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            wrapperAttr: { class: "numbering-settings-popup" },
            showCloseButton: true,
            dragEnabled: true,
            resizeEnabled: true,
            rtlEnabled: isArabic(),
            contentTemplate(contentElement) {
                const $content = $(contentElement).addClass("numbering-settings-content");

                $("<div>")
                    .addClass("numbering-settings-hero")
                    .append(
                        $("<div>").append(
                            $("<strong>").text(t("numbering.heroTitle")),
                            $("<span>").text(t("numbering.heroText"))
                        )
                    )
                    .append(
                        $("<div>")
                            .addClass("numbering-settings-current-hotel")
                            .append($("<span>").text(t("numbering.currentHotel")))
                            .append($("<strong>").text(getCurrentHotelCode() || t("roomBoard.all")))
                    )
                    .appendTo($content);

                const $tabs = $("<div>").appendTo($content);
                $tabs.dxTabPanel({
                    height: "auto",
                    animationEnabled: true,
                    swipeEnabled: false,
                    deferRendering: false,
                    rtlEnabled: isArabic(),
                    items: [
                        {
                            title: t("numbering.documentTypes"),
                            icon: "orderedlist",
                            template: function () {
                                const $pane = $("<div>").addClass("numbering-tab-pane");
                                $("<p>").addClass("numbering-settings-note").text(t("numbering.documentTypesHelp")).appendTo($pane);
                                const po = window.Zaaer.PmsGridOptions;
                                $("<div>").appendTo($pane).dxDataGrid(
                                    po.merge(po.baseline(), {
                                    dataSource: defaultDocumentTypes,
                                    keyExpr: "docCode",
                                    paging: { enabled: false },
                                    editing: {
                                        mode: "cell",
                                        allowUpdating: true,
                                        allowAdding: true,
                                        allowDeleting: false
                                    },
                                    columns: [
                                        { dataField: "docCode", caption: t("numbering.docCode"), allowEditing: true },
                                        { dataField: "label", caption: t("numbering.documentName") },
                                        { dataField: "prefix", caption: t("numbering.prefix") },
                                        { dataField: "padding", caption: t("numbering.padding"), dataType: "number" },
                                        { dataField: "includeHotel", caption: t("numbering.includeHotel"), dataType: "boolean" },
                                        { dataField: "usesZaaerId", caption: t("numbering.usesZaaerId"), dataType: "boolean" },
                                        { dataField: "sample", caption: t("numbering.sample"), allowEditing: false }
                                    ]
                                    })
                                );
                                return $pane;
                            }
                        },
                        {
                            title: t("numbering.hotelOverrides"),
                            icon: "home",
                            template: function () {
                                const $pane = $("<div>").addClass("numbering-tab-pane");
                                $("<p>").addClass("numbering-settings-note").text(t("numbering.hotelOverridesHelp")).appendTo($pane);
                                const po = window.Zaaer.PmsGridOptions;
                                $("<div>").appendTo($pane).dxDataGrid(
                                    po.merge(po.baseline(), {
                                    dataSource: [
                                        { hotelCode: getCurrentHotelCode(), docCode: "invoice", prefix: "INVO", padding: 4, includeHotel: true, preview: "INVO-16-0001" },
                                        { hotelCode: getCurrentHotelCode(), docCode: "payment_receipt", prefix: "REC", padding: 4, includeHotel: false, preview: "REC0001" },
                                        { hotelCode: getCurrentHotelCode(), docCode: "payment_refund", prefix: "PAY", padding: 4, includeHotel: false, preview: "PAY0001" }
                                    ],
                                    paging: { enabled: false },
                                    editing: {
                                        mode: "cell",
                                        allowUpdating: true,
                                        allowAdding: true,
                                        allowDeleting: true
                                    },
                                    columns: [
                                        { dataField: "hotelCode", caption: t("numbering.hotel") },
                                        { dataField: "docCode", caption: t("numbering.docCode") },
                                        { dataField: "prefix", caption: t("numbering.prefix") },
                                        { dataField: "padding", caption: t("numbering.padding"), dataType: "number" },
                                        { dataField: "includeHotel", caption: t("numbering.includeHotel"), dataType: "boolean" },
                                        { dataField: "preview", caption: t("numbering.preview"), allowEditing: false }
                                    ]
                                    })
                                );
                                return $pane;
                            }
                        },
                        {
                            title: t("numbering.seedTenant"),
                            icon: "runner",
                            template: function () {
                                const $pane = $("<div>").addClass("numbering-tab-pane numbering-seed-pane");
                                $("<p>").addClass("numbering-settings-note").text(t("numbering.seedHelp")).appendTo($pane);
                                $("<div>").appendTo($pane).dxForm({
                                    colCount: 2,
                                    labelLocation: "top",
                                    rtlEnabled: isArabic(),
                                    formData: {
                                        tenantId: null,
                                        databaseName: "",
                                        hotelZaaerId: "",
                                        localHotelId: ""
                                    },
                                    items: [
                                        { dataField: "tenantId", label: { text: "Tenants.Id" }, editorType: "dxNumberBox" },
                                        { dataField: "databaseName", label: { text: "Tenants.DatabaseName" }, editorType: "dxTextBox" },
                                        { dataField: "hotelZaaerId", label: { text: "hotel_settings.zaaer_id" }, editorType: "dxTextBox", editorOptions: { readOnly: true, placeholder: t("numbering.readFromHotelSettings") } },
                                        { dataField: "localHotelId", label: { text: "hotel_settings.hotel_id" }, editorType: "dxTextBox", editorOptions: { readOnly: true, placeholder: t("numbering.readFromHotelSettings") } }
                                    ]
                                });

                                $("<div>")
                                    .addClass("numbering-seed-actions")
                                    .append($("<div>").dxButton({
                                        text: t("numbering.validateMapping"),
                                        icon: "check",
                                        type: "default",
                                        onClick() {
                                            DevExpress.ui.notify(t("numbering.apiPending"), "info", 3000);
                                        }
                                    }))
                                    .append($("<div>").dxButton({
                                        text: t("numbering.runSeed"),
                                        icon: "refresh",
                                        onClick() {
                                            DevExpress.ui.notify(t("numbering.apiPending"), "info", 3000);
                                        }
                                    }))
                                    .appendTo($pane);
                                return $pane;
                            }
                        }
                    ]
                });
            }
        }).dxPopup("instance");

        numberingPopupInitialized = true;
    }

    function openNumberingSettings() {
        initNumberingSettingsPopup();
        numberingPopup.show();
    }

    let calendarNavDebounce = null;
    function onCalendarNavigate(nextDate) {
        const d = nextDate instanceof Date ? nextDate : new Date(nextDate);
        if (Number.isNaN(d.getTime())) {
            return;
        }

        state.date = d;
        $("#businessDateText").text(new Intl.DateTimeFormat("en-GB").format(state.date));

        const dateInst = $("#dateFilter").dxDateBox("instance");
        if (dateInst) {
            suppressDateChange = true;
            dateInst.option("value", state.date);
            suppressDateChange = false;
        }

        if (calendarNavDebounce) {
            clearTimeout(calendarNavDebounce);
        }
        calendarNavDebounce = setTimeout(() => {
            loadBoard();
        }, 120);
    }

    async function loadBoard(options) {
        const opts = options && typeof options === "object" ? options : {};
        const isHotelSwitch = !!opts.isHotelSwitch;
        const requestedHotelCode = `${opts.requestedHotelCode ?? state.hotelCode ?? ""}`.trim();
        const previousHotelCode = `${opts.previousHotelCode ?? state.boardReadyHotelCode ?? state.hotelCode ?? ""}`.trim();
        const loadToken = ++boardLoadToken;

        if (isHotelSwitch && requestedHotelCode) {
            state.hotelCode = requestedHotelCode;
        }

        boardLoadInProgress = true;
        setBoardChromeBusy(true, isHotelSwitch);

        try {
            loadPanel.show();
            if (isHotelSwitch) {
                setBoardSwitchOverlay(true, resolveHotelLabel(requestedHotelCode));
            }

            if (state.hotelCode) {
                window.Zaaer.ApiService.setHotelCode(state.hotelCode);
            }

            state.board = await window.Zaaer.RoomBoardService.loadBoard(getFilters());

            if (loadToken !== boardLoadToken) {
                return;
            }

            state.boardReadyHotelCode = state.hotelCode;
            renderSummary(state.board.summary || {});
            updateLookupFilters();
            refreshGuestColorFilterOptions();
            refreshRentalTypeFilterOptions();
            renderActiveView();
            refreshRoomBoardResortNav();
        } catch (error) {
            if (loadToken !== boardLoadToken) {
                return;
            }

            if (isHotelSwitch && previousHotelCode && previousHotelCode !== requestedHotelCode) {
                revertHotelSelection(previousHotelCode);
                DevExpress.ui.notify(t("error.loadRoomBoardHotelSwitch"), "error", 4200);
            } else {
                DevExpress.ui.notify(t("error.loadRoomBoard"), "error", 3500);
            }

            throw error;
        } finally {
            if (loadToken === boardLoadToken) {
                boardLoadInProgress = false;
                setBoardChromeBusy(false, isHotelSwitch);
                setBoardSwitchOverlay(false);
                loadPanel.hide();
            }
        }
    }

    function initRoomBoardNavTree(modeInput) {
        const mode = typeof modeInput === "object"
            ? modeInput
            : { isResort: !!modeInput, isHall: false, isHotel: !modeInput };
        state.isResort = !!mode.isResort;
        state.isHall = !!mode.isHall;
        const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
        const boardLabel =
            shell && typeof shell.boardHomeNavLabel === "function"
                ? shell.boardHomeNavLabel(mode)
                : t(state.isHall ? "property.venue.boardTitle" : state.isResort ? "roomBoard.resortTitle" : "roomBoard.title");
        const resortGroup =
            shell && typeof shell.createResortTicketsNavGroup === "function"
                ? shell.createResortTicketsNavGroup(state.isResort)
                : null;
        const hallGroup =
            shell && typeof shell.createHallOperationsNavGroup === "function"
                ? shell.createHallOperationsNavGroup(state.isHall)
                : null;
        const isLodgingProperty = !!(mode.isHotel || mode.isResort);
        const hotelReportsGroup =
            shell && typeof shell.createHotelReportsNavGroup === "function"
                ? shell.createHotelReportsNavGroup(mode)
                : null;

        const rootItems = [
            {
                id: "nav-board",
                text: boardLabel,
                icon: "home",
                link: "/room-board.html"
            },
            {
                id: "nav-property-settings",
                text: t("property.nav.settings"),
                icon: "box",
                link: "/unit-settings.html"
            },
            {
                id: "nav-booking-engine",
                text: t("bookingEngine.nav.title"),
                icon: "globe",
                link: "/booking-engine-settings.html",
                expanded: true,
                items: [
                    {
                        id: "nav-booking-engine-settings",
                        text: t("bookingEngine.nav.settings"),
                        icon: "preferences",
                        link: "/booking-engine-settings.html"
                    },
                    {
                        id: "nav-booking-engine-open",
                        text: t("bookingEngine.nav.openPage"),
                        icon: "link",
                        action: "open-booking-page"
                    }
                ]
            },
            {
                id: "nav-finance-cash",
                text: t("financeCash.nav.title"),
                icon: "money",
                expanded: false,
                items: [
                    { id: "nav-finance-expenses", text: t("financeCash.nav.expenses"), icon: "money", link: "/expenses.html" },
                    { id: "nav-finance-deposits", text: t("financeCash.nav.deposits"), icon: "box", link: "/deposits.html" }
                ]
            },
            {
                id: "nav-pos",
                text: t("pos.nav.title"),
                icon: "cart",
                expanded: false,
                items: [
                    { id: "nav-pos-terminal", text: t("pos.nav.terminal"), icon: "product", link: "/pos.html" },
                    { id: "nav-pos-settings", text: t("pos.nav.settings"), icon: "preferences", link: "/pos-settings.html" }
                ]
            }
        ];

        if (resortGroup) {
            rootItems.push(resortGroup);
        }
        if (hallGroup) {
            rootItems.push(hallGroup);
        }
        if (hotelReportsGroup) {
            rootItems.push(hotelReportsGroup);
        }

        rootItems.push(
            {
                id: "nav-integrations",
                text: t("integrations.nav.title"),
                icon: "globe",
                expanded: false,
                items: [
                    {
                        id: "nav-integrations-ntmp",
                        text: t("integrations.nav.ntmp"),
                        icon: "image",
                        navIconSrc: "/logo/ntmp.svg",
                        link: "/integration-ntmp-settings.html"
                    },
                    {
                        id: "nav-integrations-shomoos",
                        text: t("integrations.nav.shomoos"),
                        icon: "image",
                        navIconSrc: "/logo/shomoos.svg",
                        link: "/integration-shomoos-settings.html"
                    },
                    {
                        id: "nav-integrations-zatca",
                        text: t("integrations.nav.zatca"),
                        icon: "image",
                        navIconSrc: "/logo/zatca.svg",
                        link: "/integration-zatca-settings.html"
                    },
                    {
                        id: "nav-integrations-balady",
                        text: t("integrations.nav.balady"),
                        icon: "image",
                        navIconSrc: "/logo/balady_logo.svg",
                        link: "/integration-balady-report.html"
                    },
                    { id: "nav-integrations-responses", text: t("integrations.nav.responses"), icon: "orderedlist", link: "/integration-responses.html" }
                ]
            },
            {
                id: "nav-system",
                text: t("rbac.nav.systemSettings"),
                icon: "preferences",
                expanded: false,
                items: [
                    { id: "nav-rbac-users", text: t("rbac.nav.users"), icon: "user", link: "/users.html" },
                    { id: "nav-rbac-roles", text: t("rbac.nav.roles"), icon: "group", link: "/roles.html" },
                    { id: "nav-rbac-permissions", text: t("rbac.nav.permissions"), icon: "key", link: "/permissions.html" },
                    { id: "nav-numbering-admin", text: t("numberingAdmin.navTitle"), icon: "preferences", link: "/numbering-admin.html" }
                ]
            }
        );

        const navItems = [
            {
                id: "nav-root",
                text: t("app.title"),
                expanded: true,
                items: rootItems
            }
        ];

        let filteredNav = navItems;
        if (shell && typeof shell.filterPropertyNavItems === "function") {
            filteredNav = shell.filterPropertyNavItems(navItems, mode);
        } else if (shell && typeof shell.filterResortNavItems === "function") {
            filteredNav = shell.filterResortNavItems(navItems, state.isResort);
        }

        if (window.Zaaer.PmsRbacNav && window.Zaaer.PmsRbacNav.filterNavItems) {
            filteredNav = window.Zaaer.PmsRbacNav.filterNavItems(filteredNav, mode);
        }

        function applyNavTreeItemIcon(e) {
            const src = e.node && e.node.itemData && e.node.itemData.navIconSrc;
            if (!src) {
                return;
            }
            const $icon = $(e.itemElement).find(".dx-icon").first();
            if ($icon.length && !$icon.hasClass("pms-nav-tree-icon-wrap")) {
                $icon
                    .empty()
                    .addClass("pms-nav-tree-icon-wrap")
                    .append(
                        $("<img/>", { src: src, alt: "", class: "pms-nav-tree-icon", width: 18, height: 18 })
                    );
            }
        }

        const expandedItemKeys = shell && typeof shell.resolveExpandedNavKeys === "function"
            ? shell.resolveExpandedNavKeys("nav-board", filteredNav)
            : ["nav-root"];
        if (shell && typeof shell.persistNavExpandedKeys === "function") {
            shell.persistNavExpandedKeys(expandedItemKeys);
        }
        const navAccordionHandler = shell && typeof shell.createNavTreeAccordionHandler === "function"
            ? shell.createNavTreeAccordionHandler()
            : null;

        const $tree = $("#roomBoardNavTree");
        if ($tree.data("dxTreeView")) {
            const instance = $tree.dxTreeView("instance");
            instance.option("items", filteredNav);
            if (shell && typeof shell.applyNavTreeExpandedKeys === "function") {
                shell.applyNavTreeExpandedKeys(instance, expandedItemKeys);
            } else {
                instance.option("expandedItemKeys", expandedItemKeys);
            }
            return;
        }

        $tree.dxTreeView({
            items: filteredNav,
            keyExpr: "id",
            width: "100%",
            animationEnabled: true,
            focusStateEnabled: false,
            selectNodesRecursive: false,
            selectionMode: "single",
            selectByClick: true,
            searchEnabled: true,
            searchEditorOptions: {
                placeholder: t("roomBoard.navSearchPlaceholder"),
                mode: "text",
                stylingMode: "filled"
            },
            expandedItemKeys: expandedItemKeys,
            selectedItemKeys: ["nav-board"],
            onItemRendered: applyNavTreeItemIcon,
            onItemClick(e) {
                const nav = window.Zaaer.PmsRbacNav;
                if (nav && nav.handleNavItemClick) {
                    const result = nav.handleNavItemClick(e, () => state.hotelCode);
                    if (result === "numbering-settings") {
                        openNumberingSettings();
                    }
                    return;
                }

                if (e.itemData && e.itemData.action === "numbering-settings") {
                    openNumberingSettings();
                    return;
                }

                const link = e.itemData && e.itemData.link;
                if (link && link !== "#") {
                    if (shell && typeof shell.handleNavTreeItemNavigate === "function") {
                        shell.handleNavTreeItemNavigate(e, filteredNav, "nav-board");
                        return;
                    }
                    window.location.href = link;
                }
            },
            onItemExpanded: navAccordionHandler,
            onContentReady(e) {
                if (shell && typeof shell.applyNavTreeExpandedKeys === "function") {
                    shell.applyNavTreeExpandedKeys(e.component, expandedItemKeys);
                }
            }
        });
    }

    function refreshRoomBoardResortNav() {
        const shell = window.Zaaer && window.Zaaer.PmsAdminShell;
        if (shell && typeof shell.fetchPropertyMode === "function") {
            return shell.fetchPropertyMode().then((mode) => {
                initRoomBoardNavTree(mode || { isResort: false, isHall: false, isHotel: true });
                applyResortFilterLabels();
                if (mode && mode.isHall && window.Zaaer.HallEventsService) {
                    window.Zaaer.HallEventsService.syncStatuses().catch(() => null);
                }
            });
        }

        initRoomBoardNavTree({ isResort: false, isHall: false, isHotel: true });
        applyResortFilterLabels();
        return $.when();
    }

    function initSidebarToggle() {
        const $shell = $(".room-board-shell");
        const $btn = $("#roomBoardNavToggle");

        function applyCollapsed(collapsed) {
            $shell.toggleClass("room-board-shell--nav-collapsed", collapsed);
            $btn.attr("aria-expanded", collapsed ? "false" : "true");
            try {
                localStorage.setItem(NAV_COLLAPSED_STORAGE_KEY, collapsed ? "1" : "0");
            } catch {
                /* ignore */
            }
        }

        let startCollapsed = false;
        try {
            startCollapsed = localStorage.getItem(NAV_COLLAPSED_STORAGE_KEY) === "1";
        } catch {
            startCollapsed = false;
        }

        if (window.matchMedia("(max-width: 1180px)").matches) {
            startCollapsed = true;
        }

        applyCollapsed(startCollapsed);

        $btn.on("click", () => {
            applyCollapsed(!$shell.hasClass("room-board-shell--nav-collapsed"));
        });
    }

    async function initHotelCodeFilter() {
        let hotels = [];
        try {
            hotels = await window.Zaaer.RoomBoardService.loadHotelCodes();
        } catch {
            hotels = [];
        }

        const stored = window.Zaaer.ApiService.getHotelCode() || "";
        const codes = new Set(hotels.map((h) => h.code));
        let initial = stored && codes.has(stored) ? stored : hotels[0]?.code || "";

        if (initial && initial !== stored) {
            window.Zaaer.ApiService.setHotelCode(initial);
        }

        state.availableHotels = hotels;

        $("#hotelCodeFilterRow").addClass("room-board-filter-hotel-row--hidden");

        applyActiveHotelTitle();
        state.hotelCode = initial || "";

        const chrome = window.Zaaer.PmsTopChrome;
        if (chrome && typeof chrome.initHeaderHotelPicker === "function") {
            return chrome.initHeaderHotelPicker({
                loadHotels: () => $.when(hotels),
                onHotelChanged(v, previousValue) {
                    if (suppressHotelPickerSync) {
                        return;
                    }

                    const next = `${v ?? ""}`.trim();
                    if (!next) {
                        return;
                    }

                    if (boardLoadInProgress) {
                        return;
                    }

                    if (next === state.boardReadyHotelCode && state.board) {
                        return;
                    }

                    void loadBoard({
                        isHotelSwitch: true,
                        requestedHotelCode: next,
                        previousHotelCode: `${previousValue ?? state.boardReadyHotelCode ?? state.hotelCode ?? ""}`.trim()
                    });
                }
            });
        }
    }

    async function initToolbar() {
        $("#searchFilter").dxTextBox({
            label: t("roomBoard.search"),
            labelMode: "floating",
            valueChangeEvent: "keyup",
            width: "100%",
            showClearButton: true,
            onValueChanged(e) {
                const next = e.value == null ? "" : String(e.value);
                const prev = e.previousValue == null ? "" : String(e.previousValue);
                state.search = next;
                if (!next.trim() && prev.trim()) {
                    loadBoard();
                }
            },
            onEnterKey: loadBoard
        });

        $("#searchFilterBtn").dxButton({
            icon: "search",
            type: "default",
            stylingMode: "contained",
            hint: t("roomBoard.search"),
            elementAttr: { class: "room-board-filter-search-btn-widget room-board-filter-search-btn-widget--compact" },
            onClick() {
                const searchBox = $("#searchFilter").dxTextBox("instance");
                if (searchBox) {
                    state.search = searchBox.option("value") || "";
                }
                loadBoard();
            }
        });

        $("#dateFilter").dxDateBox({
            label: t("roomBoard.date"),
            labelMode: "floating",
            type: "date",
            displayFormat: "dd/MM/yyyy",
            value: state.date,
            useMaskBehavior: true,
            openOnFieldClick: true,
            onValueChanged(e) {
                if (suppressDateChange) {
                    return;
                }
                state.date = e.value || new Date();
                $("#businessDateText").text(new Intl.DateTimeFormat("en-GB").format(state.date));
                loadBoard();
            }
        });

        await initHotelCodeFilter();

        const tagBoxDefaults = {
            labelMode: "floating",
            valueExpr: "id",
            displayExpr: "text",
            showClearButton: true,
            showSelectionControls: true,
            applyValueMode: "useButtons",
            multiline: false,
            maxDisplayedTags: 1,
            showMultiTagOnly: false
        };

        $("#buildingFilter").dxTagBox({
            ...tagBoxDefaults,
            label: t("roomBoard.building"),
            onValueChanged(e) {
                state.buildingIds = e.value || [];
                loadBoard();
            }
        });

        $("#floorFilter").dxTagBox({
            ...tagBoxDefaults,
            label: t("roomBoard.floor"),
            onValueChanged(e) {
                state.floorIds = e.value || [];
                loadBoard();
            }
        });

        $("#roomTypeFilter").dxTagBox({
            ...tagBoxDefaults,
            label: roomBoardRoomTypeLabel(),
            dropDownOptions: {
                width: roomBoardRoomTypeDropDownWidth(),
                wrapperAttr: { class: "room-board-room-type-filter-dropdown" }
            },
            elementAttr: { class: "room-board-room-type-filter-tagbox" },
            onValueChanged(e) {
                state.roomTypeIds = e.value || [];
                loadBoard();
            }
        });

        $("#statusFilter").dxTagBox({
            ...tagBoxDefaults,
            label: t("roomBoard.status"),
            dataSource: [
                { id: "available", text: t("status.available") },
                { id: "occupied", text: t("status.occupied") },
                { id: "reserved", text: t("status.reserved") },
                { id: "cleaning", text: t("status.cleaning") },
                { id: "maintenance", text: t("status.maintenance") }
            ],
            onValueChanged(e) {
                if (suppressStatusTagChange) {
                    return;
                }

                state.statuses = e.value || [];
                state.status = "";
                state.alert = "";
                persistRoomBoardUiState();
                loadBoard();
            }
        });

        $("#rentalTypeFilter").dxTagBox({
            ...tagBoxDefaults,
            label: t("roomBoard.rentalTypeFilter"),
            hint: t("roomBoard.rentalTypeFilterHint"),
            dataSource: [],
            disabled: true,
            dropDownOptions: {
                width: 320,
                wrapperAttr: { class: "room-board-rental-filter-dropdown" }
            },
            elementAttr: { class: "room-board-rental-filter-tagbox" },
            itemTemplate: renderRentalTypeFilterItem,
            tagTemplate: renderRentalTypeFilterTag,
            onValueChanged(e) {
                state.rentalTypes = (e.value || [])
                    .map(normalizeRentalTypeFilterKey)
                    .filter(Boolean);
                persistRoomBoardUiState();
                applyBoardClientFiltersToView();
            }
        });

        $("#guestColorFilter").dxTagBox({
            ...tagBoxDefaults,
            displayExpr: "label",
            label: t("roomBoard.guestColorFilter"),
            hint: t("roomBoard.guestColorFilterHint"),
            dataSource: [],
            disabled: true,
            itemTemplate: renderGuestColorFilterItem,
            tagTemplate: renderGuestColorFilterTag,
            onValueChanged(e) {
                state.guestColors = (e.value || [])
                    .map(normalizeGuestColorKey)
                    .filter(Boolean);
                persistRoomBoardUiState();
                applyBoardClientFiltersToView();
            }
        });

        $("#viewModeSwitch").dxButtonGroup({
            keyExpr: "id",
            selectionMode: "single",
            selectedItemKeys: [state.viewMode],
            items: [
                { id: "cards", icon: "card", hint: t("roomBoard.cards") },
                { id: "rack", icon: "hierarchy", hint: t("roomBoard.rack") },
                { id: "grid", icon: "rowfield", hint: t("roomBoard.grid") },
                { id: "calendar", icon: "event", hint: t("roomBoard.calendar") }
            ],
            onSelectionChanged(e) {
                const selected = e.addedItems[0];
                if (selected) {
                    const previousMode = state.viewMode;
                    state.viewMode = selected.id;
                    persistRoomBoardUiState();
                    if (selected.id === "calendar" || previousMode === "calendar") {
                        loadBoard();
                    } else {
                        renderActiveView();
                    }
                }
            }
        });

        $("#refreshButton").dxButton({
            icon: "refresh",
            type: "default",
            hint: t("roomBoard.refresh"),
            stylingMode: "contained",
            elementAttr: {
                class: "room-board-refresh-btn",
                "aria-label": t("roomBoard.refresh")
            },
            onClick: loadBoard
        });
    }

    const MOBILE_FILTERS_MQ = "(max-width: 900px)";

    function initMobileFilterToggle() {
        const main = document.getElementById("roomBoardMain");
        const toggle = document.getElementById("roomBoardFilterToggle");
        const card = document.getElementById("roomBoardFilterCard");
        if (!main || !toggle || !card) {
            return;
        }

        const mq = window.matchMedia(MOBILE_FILTERS_MQ);

        function mobileFiltersMode() {
            return mq.matches;
        }

        function setOpen(open) {
            const on = !!open;
            main.classList.toggle("room-board-main--filters-open", on);
            card.classList.toggle("room-board-filter-card--open", on);
            toggle.setAttribute("aria-expanded", on ? "true" : "false");

            if (mobileFiltersMode()) {
                if (on) {
                    card.removeAttribute("hidden");
                } else {
                    card.setAttribute("hidden", "");
                }
            } else {
                card.removeAttribute("hidden");
                main.classList.remove("room-board-main--filters-open");
                card.classList.remove("room-board-filter-card--open");
            }
        }

        function syncFilterPanelToViewport() {
            if (!mobileFiltersMode()) {
                card.removeAttribute("hidden");
                main.classList.remove("room-board-main--filters-open");
                card.classList.remove("room-board-filter-card--open");
                toggle.setAttribute("aria-expanded", "true");
                return;
            }

            const wasOpen =
                main.classList.contains("room-board-main--filters-open") &&
                card.classList.contains("room-board-filter-card--open");
            setOpen(wasOpen);
        }

        function onToggleClick(e) {
            if (e) {
                e.preventDefault();
                e.stopPropagation();
            }
            if (!mobileFiltersMode()) {
                return;
            }
            setOpen(!main.classList.contains("room-board-main--filters-open"));
        }

        setOpen(false);

        toggle.addEventListener("click", onToggleClick);
        if (typeof mq.addEventListener === "function") {
            mq.addEventListener("change", syncFilterPanelToViewport);
        } else if (typeof mq.addListener === "function") {
            mq.addListener(syncFilterPanelToViewport);
        }

        syncFilterPanelToViewport();
    }

    function initTopChrome() {
        if (window.Zaaer.PmsTopChrome && window.Zaaer.PmsTopChrome.initUserAccountMenu) {
            window.Zaaer.PmsTopChrome.initUserAccountMenu("#userAccountMenu");
        }
    }

    function canAccessRoomBoard() {
        const policy = window.Zaaer && window.Zaaer.PmsRbacPolicy;
        if (policy && typeof policy.canRoomBoardView === "function") {
            return policy.canRoomBoardView();
        }

        const svc = window.Zaaer && window.Zaaer.ApiService;
        return !!(svc && typeof svc.hasPermission === "function" && svc.hasPermission("room_board.view"));
    }

    $(async function () {
        if (!window.Zaaer.ApiService.requireToken()) {
            return;
        }

        if (!canAccessRoomBoard()) {
            const api = window.Zaaer.ApiService;
            const ensure = api && api.ensurePermissionsReady ? api.ensurePermissionsReady() : $.when();
            await ensure;
            const fallback =
                api && api.resolveLandingUrl ? api.resolveLandingUrl("/room-board.html") : "/room-board.html";
            if (fallback && fallback.toLowerCase() !== "/room-board.html") {
                window.location.replace(fallback);
                return;
            }

            DevExpress.ui.notify(t("common.forbidden"), "warning", 4000);
            return;
        }

        window.Zaaer.LocalizationService.init();
        applyStaticText();

        loadPanel = $("#roomBoardLoadPanel").dxLoadPanel({
            shadingColor: "rgba(255,255,255,0.45)",
            position: { of: ".room-board-shell" },
            visible: false
        }).dxLoadPanel("instance");

        initTopChrome();
        initRoomBoardNavTree(false);
        initSidebarToggle();
        initMobileFilterToggle();
        restoreRoomBoardUiState();
        await initToolbar();
        await refreshRoomBoardResortNav();
        applyRestoredUiToWidgets();
        applyActiveHotelTitle();
        await loadBoard();

        syncRoomBoardStickyChrome();
        $(window).on("resize.roomBoardChrome", syncRoomBoardStickyChrome);

        window.addEventListener("zaaer:permissions-refreshed", () => {
            refreshRoomBoardResortNav();
            loadBoard().catch(() => {
                /* loadBoard already notifies */
            });
        });
    });
})(window, jQuery, DevExpress);
