(function (window, $, DevExpress) {

    "use strict";



    let schedulerInstance = null;

    let $hostRoot = null;

    let roomByApartmentId = {};

    let onBoardRefreshCb = null;

    let translateFn = (key) => key;

    let schedulerDateRange = null;

    let ctxMenuInst = null;

    let ctxRoom = null;

    let rawAppointments = [];

    let rawResources = [];

    let calendarClientFilter = { search: "", roomTypeIds: [] };

    let calendarViewKey = "month";

    let apptHoverTooltipInst = null;

    let appointmentDataById = {};

    const CALENDAR_VIEW_STORAGE_KEY = "zaaer.roomBoard.calendarView.v1";

    const CALENDAR_FILTER_STORAGE_KEY = "zaaer.roomBoard.calendarFilter.v1";

    const TIMELINE_COMPACT_ROW_PX = 22;

    let timelineLayoutLock = false;

    let timelineWorkSpaceCellHeightSet = false;



    const LEGEND_STATUSES = [

        { cssClass: "status-reserved", labelKey: "status.reserved" },

        { cssClass: "status-occupied", labelKey: "status.occupied" },

        { cssClass: "status-available", labelKey: "status.available" },

        { cssClass: "status-cleaning", labelKey: "status.cleaning" },

        { cssClass: "status-maintenance", labelKey: "status.maintenance" }

    ];



    function isRtl() {

        const svc = window.Zaaer && window.Zaaer.LocalizationService;

        if (svc && typeof svc.currentCulture === "function") {

            return svc.currentCulture() === "ar";

        }



        return document.documentElement.getAttribute("dir") === "rtl";

    }



    function normalizeSchedulerId(value) {

        if (value === undefined || value === null || value === "") {

            return null;

        }

        const n = Number(value);

        return Number.isNaN(n) ? null : n;

    }



    function readRoomBoardId(room, boardKey, internalKey) {

        if (!room) {

            return null;

        }

        return normalizeSchedulerId(room[boardKey] ?? room[internalKey]);

    }



    function readCalendarApartmentId(item) {

        if (!item) {

            return null;

        }

        return normalizeSchedulerId(item.apartmentId ?? item.ApartmentId);

    }



    function filterAppointmentsForResources(appointments, resources) {

        const resourceIds = new Set();

        (resources || []).forEach((resource) => {

            const id = normalizeSchedulerId(resource.id);

            if (id != null) {

                resourceIds.add(id);

            }

        });

        return (appointments || []).filter((appointment) => {

            const apartmentId = normalizeSchedulerId(appointment.apartmentId);

            return apartmentId != null && resourceIds.has(apartmentId);

        });

    }



    function enrichAppointmentForDisplay(data) {

        if (!data) {

            return null;

        }

        const text = typeof data.text === "string" ? data.text.trim() : "";

        let reservationNo = data.reservationNo || "";

        let guestName = data.guestName || "";

        if (text) {

            if (text.includes(" - ")) {

                const parts = text.split(" - ");

                if (!reservationNo) {

                    reservationNo = parts[0] || "";

                }

                if (!guestName) {

                    guestName = parts.slice(1).join(" - ").trim();

                }

            } else if (!reservationNo) {

                reservationNo = text;

            }

        }

        let stayEndDate = data.stayEndDate;

        if (!stayEndDate && data.endDate) {

            const d = new Date(data.endDate);

            if (!Number.isNaN(d.getTime())) {

                d.setDate(d.getDate() - 1);

                stayEndDate = d;

            }

        }

        const aptId = normalizeSchedulerId(data.apartmentId);

        const room = aptId != null ? roomByApartmentId[aptId] : null;

        const roomCode =

            data.roomCode ||

            (room && (room.apartmentCode || room.apartmentName)) ||

            "";



        return {

            ...data,

            reservationNo: reservationNo || "—",

            guestName: guestName || "—",

            roomCode: roomCode ? String(roomCode).trim() : "",

            stayStartDate: data.stayStartDate || data.startDate,

            stayEndDate: stayEndDate || data.endDate,

            statusLabel: data.statusLabel || data.type || "",

            rentalType: data.rentalType || ""

        };

    }



    function formatShortDate(value) {

        if (!value) {

            return "—";

        }



        const d = value instanceof Date ? value : new Date(value);

        if (Number.isNaN(d.getTime())) {

            return "—";

        }



        return new Intl.DateTimeFormat("en-GB", {

            day: "2-digit",

            month: "2-digit",

            year: "numeric"

        }).format(d);

    }



    function startOfDay(value) {

        const d = value instanceof Date ? new Date(value.getTime()) : new Date(value);

        if (Number.isNaN(d.getTime())) {

            return new Date();

        }



        d.setHours(0, 0, 0, 0);

        return d;

    }



    function monthSpanInclusive(from, to) {

        const a = startOfDay(from);

        const b = startOfDay(to);

        const months = (b.getFullYear() - a.getFullYear()) * 12 + (b.getMonth() - a.getMonth()) + 1;

        return Math.max(1, months);

    }



    function resolveSchedulerDateRange(opts, board) {

        const anchor = startOfDay((opts && opts.currentDate) || new Date());

        let min = startOfDay((opts && opts.dateRange && opts.dateRange.from) || anchor);

        let max = startOfDay((opts && opts.dateRange && opts.dateRange.to) || anchor);



        if (!opts || !opts.dateRange) {

            min = new Date(anchor);

            min.setDate(min.getDate() - 14);

            max = new Date(anchor);

            max.setMonth(max.getMonth() + 18);

        }



        ((board && board.calendarItems) || []).forEach((item) => {

            if (!item || !item.endDate) {

                return;

            }



            const end = startOfDay(item.endDate);

            if (end > max) {

                max = new Date(end);

            }

        });



        max.setDate(max.getDate() + 21);

        if (max < min) {

            max = new Date(min);

            max.setMonth(max.getMonth() + 3);

        }



        return {

            min,

            max,

            // Keep the view light (one month); users navigate with arrows.
            monthCount: 1

        };

    }



    /** Scheduler end is exclusive; extend one day so checkout day is included on the timeline. */

    function toSchedulerEndDate(value) {

        const d = startOfDay(value);

        d.setDate(d.getDate() + 1);

        return d;

    }



    function toSchedulerAppointment(item) {

        const boardApartmentId = resolveBoardApartmentId(readCalendarApartmentId(item));

        if (boardApartmentId == null) {

            return null;

        }



        const stayStart = startOfDay(item.startDate);

        const stayEnd = startOfDay(item.endDate);



        const appt = {

            id: item.id,

            apartmentId: boardApartmentId,

            text: item.text,

            startDate: stayStart,

            endDate: toSchedulerEndDate(stayEnd),

            stayStartDate: stayStart,

            stayEndDate: stayEnd,

            type: item.type,

            statusCssClass: item.statusCssClass || "",

            reservationId: item.reservationId,

            unitId: item.unitId,

            reservationNo: item.reservationNo || "",

            guestName: item.guestName || "",

            statusLabel: item.statusLabel || item.type || "",

            rentalType: item.rentalType || ""

        };

        const enriched = enrichAppointmentForDisplay(appt);

        if (enriched && enriched.id != null) {

            appointmentDataById[enriched.id] = enriched;

        }

        return enriched;

    }



    function rebuildAppointmentDataMap(appointments) {

        appointmentDataById = {};

        (appointments || []).forEach((appt) => {

            if (appt && appt.id != null) {

                appointmentDataById[appt.id] = appt;

            }

        });

    }



    function resolveAppointmentData($target) {

        if (!$target || !$target.length) {

            return null;

        }



        let data = null;

        if (schedulerInstance && typeof schedulerInstance.getTargetedAppointmentData === "function") {

            try {

                data = schedulerInstance.getTargetedAppointmentData($target);

            } catch {

                /* ignore */

            }

        }



        if (!data) {

            data = $target.data("rbAppt");

        }

        if (!data) {

            data = $target.find(".room-scheduler-appointment").data("rbAppt");

        }

        if (!data) {

            const id =

                $target.attr("data-rb-appt-id") || $target.find("[data-rb-appt-id]").first().attr("data-rb-appt-id");

            if (id != null && id !== "") {

                data = appointmentDataById[id];

            }

        }



        if (data && data.id != null && appointmentDataById[data.id]) {

            data = { ...appointmentDataById[data.id], ...data };

        }



        return enrichAppointmentForDisplay(data);

    }



    function toRoomResource(room, t) {

        const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;

        const code = room.apartmentCode || room.apartmentName || String(room.apartmentId);

        const typeRaw = room.roomTypeName || "";

        const typeLabel =

            labeler && typeof labeler.display === "function" ? labeler.display(typeRaw, t) : typeRaw;



        const boardId = readRoomBoardId(room, "apartmentId", "ApartmentId");

        return {

            id: boardId,

            text: code,

            roomTypeName: typeLabel,

            roomTypeId: room.roomTypeId,

            operationalStatus: room.operationalStatus || ""

        };

    }



    function buildRoomResourceMap(rooms) {

        roomByApartmentId = {};

        (rooms || []).forEach((room) => {

            const boardId = readRoomBoardId(room, "apartmentId", "ApartmentId");

            if (boardId == null) {

                return;

            }

            roomByApartmentId[boardId] = room;

            const internalId = readRoomBoardId(room, "internalApartmentId", "InternalApartmentId");

            if (internalId != null && internalId !== boardId) {

                roomByApartmentId[internalId] = room;

            }

        });

    }



    function resolveBoardApartmentId(apartmentId) {

        const numericId = normalizeSchedulerId(apartmentId);

        if (numericId == null) {

            return null;

        }



        const room = roomByApartmentId[numericId];

        if (room) {

            return readRoomBoardId(room, "apartmentId", "ApartmentId");

        }



        return null;

    }



    function loadCalendarViewKey() {

        try {

            const v = localStorage.getItem(CALENDAR_VIEW_STORAGE_KEY);

            if (v === "week" || v === "month") {

                return v;

            }

        } catch {

            /* storage unavailable */

        }

        return "month";

    }



    function restoreCalendarClientFilter() {

        try {

            const raw = localStorage.getItem(CALENDAR_FILTER_STORAGE_KEY);

            if (!raw) {

                return;

            }

            const saved = JSON.parse(raw);

            calendarClientFilter.search = typeof saved.search === "string" ? saved.search : "";

            calendarClientFilter.roomTypeIds = Array.isArray(saved.roomTypeIds) ? saved.roomTypeIds.slice() : [];

        } catch {

            /* ignore */

        }

    }



    function persistCalendarClientFilter() {

        try {

            localStorage.setItem(

                CALENDAR_FILTER_STORAGE_KEY,

                JSON.stringify({

                    search: calendarClientFilter.search || "",

                    roomTypeIds: calendarClientFilter.roomTypeIds || []

                })

            );

        } catch {

            /* storage unavailable */

        }

    }



    function uniqueRoomTypesFromRooms(rooms) {

        const map = new Map();

        (rooms || []).forEach((room) => {

            if (room && room.roomTypeId != null && !map.has(room.roomTypeId)) {

                map.set(room.roomTypeId, {

                    id: room.roomTypeId,

                    text: room.roomTypeName || String(room.roomTypeId)

                });

            }

        });

        return Array.from(map.values());

    }



    function mapCalendarRoomTypeOptions(items, t) {

        const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;

        return (items || []).map((item) => ({

            ...item,

            text:

                labeler && typeof labeler.display === "function"

                    ? labeler.display(item.text, t)

                    : item.text

        }));

    }



    function appointmentSearchHaystack(appointment) {

        const room = roomByApartmentId[appointment.apartmentId];

        return [

            appointment.guestName,

            appointment.reservationNo,

            appointment.text,

            room && room.apartmentCode,

            room && room.apartmentName,

            room && room.roomTypeName

        ]

            .filter(Boolean)

            .join(" ")

            .toLowerCase();

    }



    function applyCalendarClientFilters() {

        if (!schedulerInstance) {

            return;

        }



        const search = (calendarClientFilter.search || "").trim().toLowerCase();

        const typeIds = new Set((calendarClientFilter.roomTypeIds || []).map(String));

        let resources = rawResources.slice();



        if (typeIds.size) {

            resources = resources.filter((r) => {

                const room = roomByApartmentId[normalizeSchedulerId(r.id)];

                return room && typeIds.has(String(room.roomTypeId));

            });

        }



        let appointments = filterAppointmentsForResources(rawAppointments, resources);



        if (search) {

            appointments = appointments.filter((a) => appointmentSearchHaystack(a).includes(search));

            resources = resources.filter((r) => {

                const room = roomByApartmentId[r.id];

                const roomHay = `${room?.apartmentCode || ""} ${room?.apartmentName || ""} ${room?.roomTypeName || ""} ${r.roomTypeName || ""}`.toLowerCase();

                if (roomHay.includes(search)) {

                    return true;

                }

                const rid = normalizeSchedulerId(r.id);

                return appointments.some((a) => normalizeSchedulerId(a.apartmentId) === rid);

            });

            appointments = filterAppointmentsForResources(appointments, resources);

        } else if (typeIds.size) {

            appointments = filterAppointmentsForResources(appointments, resources);

        }



        const resourceLabel =

            schedulerInstance.option("resources") &&

            schedulerInstance.option("resources")[0] &&

            schedulerInstance.option("resources")[0].label;



        schedulerInstance.option({

            dataSource: appointments,

            resources: [

                {

                    fieldExpr: "apartmentId",

                    label: resourceLabel,

                    dataSource: resources,

                    useColorAsDefault: false

                }

            ]

        });

    }



    function setCalendarView(key) {

        calendarViewKey = key === "week" ? "week" : "month";

        try {

            localStorage.setItem(CALENDAR_VIEW_STORAGE_KEY, calendarViewKey);

        } catch {

            /* storage unavailable */

        }



        if (!schedulerInstance) {

            return;

        }



        schedulerInstance.option("currentView", calendarViewKey);

        scheduleSchedulerChromeRefresh(translateFn);



        const $shell = $hostRoot && $hostRoot.closest(".room-board-calendar-shell");

        const $toggle = $shell && $shell.find("#roomBoardCalendarViewToggle");

        const toggleInst = $toggle && $toggle.length ? $toggle.dxButtonGroup("instance") : null;

        if (toggleInst) {

            toggleInst.option("selectedItemKeys", [calendarViewKey]);

        }

    }



    function isArabicUi() {

        const svc = window.Zaaer && window.Zaaer.LocalizationService;

        if (svc && typeof svc.currentCulture === "function") {

            return svc.currentCulture() === "ar";

        }

        return isRtl();

    }



    function patchSchedulerViewHeaderLabel(t) {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }



        const weekLabel = t("roomBoard.viewWeek");

        const monthLabel = t("roomBoard.viewMonth");

        const isAr = isArabicUi();



        $hostRoot.find(".dx-scheduler-header-panel-cell").each(function () {

            const $cell = $(this);

            const raw = ($cell.text() || "").trim().toLowerCase();

            if (!isAr) {

                return;

            }

            if (raw === "week" || raw === "timelineweek") {

                $cell.text(weekLabel);

            } else if (raw === "month" || raw === "timelinemonth") {

                $cell.text(monthLabel);

            }

        });



        if (!isAr) {

            return;

        }



        $hostRoot.find(".dx-scheduler-view-switcher .dx-texteditor-input").each(function () {

            const $input = $(this);

            const raw = ($input.val() || "").trim().toLowerCase();

            if (raw === "week" || raw === "timelineweek") {

                $input.val(weekLabel);

            } else if (raw === "month" || raw === "timelinemonth") {

                $input.val(monthLabel);

            }

        });

    }



    function getApptHoverTooltip() {

        if (apptHoverTooltipInst) {

            return apptHoverTooltipInst;

        }



        let $host = $("#roomBoardApptHoverTip");

        if (!$host.length) {

            $host = $("<div>").attr("id", "roomBoardApptHoverTip").appendTo("body");

        }



        apptHoverTooltipInst = $host

            .dxTooltip({

                position: { my: "bottom center", at: "top center", collision: "fit flip" },

                animation: {

                    show: { type: "fade", duration: 120 },

                    hide: { type: "fade", duration: 80 }

                },

                hideOnOutsideClick: false,

                wrapperAttr: { class: "room-scheduler-hover-tooltip-wrap" }

            })

            .dxTooltip("instance");



        return apptHoverTooltipInst;

    }



    function wireAppointmentHoverTooltips() {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }



        const tip = getApptHoverTooltip();

        let hideTimer = null;



        $hostRoot.off(".rbApptHoverTip");

        $hostRoot.on("mouseenter.rbApptHoverTip", ".dx-scheduler-appointment", function (ev) {

            if (hideTimer) {

                clearTimeout(hideTimer);

                hideTimer = null;

            }



            const $target = $(ev.currentTarget);

            const data = resolveAppointmentData($target);

            if (!data) {

                return;

            }



            const snapshot = enrichAppointmentForDisplay(data);

            tip.option({

                target: ev.currentTarget,

                contentTemplate: function (container) {

                    const $container = container ? $(container) : $();

                    $container.empty().append(buildTooltipElement(snapshot));

                }

            });

            tip.show();

        });



        $hostRoot.on("mouseleave.rbApptHoverTip", ".dx-scheduler-appointment", function () {

            hideTimer = setTimeout(function () {

                tip.hide();

            }, 100);

        });

    }



    let chromeRefreshTimer = null;

    function purgeSchedulerOrphanAppointments() {

        if (!schedulerInstance) {

            return;

        }



        const resourceConfig = schedulerInstance.option("resources");

        const resourceList = resourceConfig && resourceConfig[0] ? resourceConfig[0].dataSource : [];

        const current = schedulerInstance.option("dataSource") || [];

        const filtered = filterAppointmentsForResources(current, resourceList);

        if (filtered.length !== current.length) {

            schedulerInstance.option("dataSource", filtered);

        }

    }



    function wireAppointmentLabelPinOnScroll() {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }

        const $scroll = $hostRoot.find(".dx-scheduler-date-table-scrollable .dx-scrollable-container");

        if (!$scroll.length) {

            return;

        }

        const rtl = isRtl();

        function updatePins() {

            const scrollNode = $scroll.get(0);

            if (!scrollNode) {

                return;

            }

            const vr = scrollNode.getBoundingClientRect();

            const pad = 6;

            $hostRoot.find(".dx-scheduler-appointment").each(function () {

                const $line = $(this).find(".room-scheduler-appointment__label-sticky");

                if (!$line.length) {

                    return;

                }

                const ar = this.getBoundingClientRect();

                if (ar.bottom < vr.top || ar.top > vr.bottom || ar.right < vr.left || ar.left > vr.right) {

                    $line.css("transform", "");

                    return;

                }

                const lineW = $line.outerWidth() || 0;

                if (rtl) {

                    const pinRight = vr.right - pad;

                    if (ar.right > pinRight && ar.left < pinRight - lineW) {

                        const shift = ar.right - pinRight;

                        $line.css("transform", "translateX(" + -shift + "px)");

                    } else {

                        $line.css("transform", "");

                    }

                } else {

                    const pinLeft = vr.left + pad;

                    if (ar.left < pinLeft && ar.right > pinLeft + lineW) {

                        const shift = pinLeft - ar.left;

                        $line.css("transform", "translateX(" + shift + "px)");

                    } else {

                        $line.css("transform", "");

                    }

                }

            });

        }

        $scroll.off("scroll.rbApptLabelPin");

        $(window).off("resize.rbApptLabelPin");

        $scroll.on("scroll.rbApptLabelPin", updatePins);

        $(window).on("resize.rbApptLabelPin", updatePins);

        updatePins();

    }



    function applySchedulerWorkSpaceCellHeightOnce() {

        if (timelineWorkSpaceCellHeightSet || !schedulerInstance || typeof schedulerInstance.getWorkSpace !== "function") {

            return;

        }

        try {

            const ws = schedulerInstance.getWorkSpace();

            if (!ws || typeof ws.option !== "function") {

                return;

            }

            const current = ws.option("cellHeight");

            if (current === TIMELINE_COMPACT_ROW_PX) {

                timelineWorkSpaceCellHeightSet = true;

                return;

            }

            ws.option("cellHeight", TIMELINE_COMPACT_ROW_PX);

            timelineWorkSpaceCellHeightSet = true;

        } catch {

            /* workspace not ready */

        }

    }



    function syncCompactTimelineRowHeights() {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }

        const $timeline = $hostRoot.find(".dx-scheduler-timeline");

        if (!$timeline.length) {

            return;

        }

        const px = TIMELINE_COMPACT_ROW_PX + "px";

        const rowCss = {

            height: px,

            minHeight: px,

            maxHeight: px,

            flex: "0 0 " + px,

            flexGrow: 0

        };

        const sizeClass = "dx-scheduler-cell-sizes-vertical";

        const sizedSelector =

            ".dx-scheduler-date-table-row, .dx-scheduler-group-header, .dx-scheduler-date-table-cell";



        $timeline.find(".dx-scheduler-date-table-row").css(rowCss);

        $timeline.find(".dx-scheduler-group-header").css(rowCss);

        $timeline.find(".dx-scheduler-date-table-cell").css({

            height: px,

            minHeight: px,

            maxHeight: px

        });

        $timeline.find(".dx-scheduler-date-table .dx-scheduler-date-table-cell").css({

            flex: "0 0 auto",

            minHeight: px,

            height: px

        });

        $timeline.find(".dx-scheduler-group-header-content").css({

            height: px,

            minHeight: px

        });

        $timeline.find(sizedSelector).each(function () {

            this.classList.add(sizeClass);

        });



        const scrollPad = "20px";

        $timeline

            .find(

                ".dx-scheduler-date-table-scrollable .dx-scrollable-content, .dx-scheduler-sidebar-scrollable .dx-scrollable-content"

            )

            .css("paddingBottom", scrollPad);

    }



    function wireTimelineVerticalScrollSync() {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }

        const $dateScroll = $hostRoot.find(".dx-scheduler-date-table-scrollable .dx-scrollable-container");

        const $sideScroll = $hostRoot.find(".dx-scheduler-sidebar-scrollable .dx-scrollable-container");

        if (!$dateScroll.length || !$sideScroll.length) {

            return;

        }

        let syncing = false;

        function mirrorScroll($from, $to) {

            if (syncing) {

                return;

            }

            syncing = true;

            $to.scrollTop($from.scrollTop());

            syncing = false;

        }

        $dateScroll.off("scroll.rbTimelineVSync");

        $sideScroll.off("scroll.rbTimelineVSync");

        $dateScroll.on("scroll.rbTimelineVSync", function () {

            mirrorScroll($dateScroll, $sideScroll);

        });

        $sideScroll.on("scroll.rbTimelineVSync", function () {

            mirrorScroll($sideScroll, $dateScroll);

        });

    }



    function refreshTimelineLayout() {

        if (timelineLayoutLock) {

            return;

        }

        timelineLayoutLock = true;

        try {

            applySchedulerWorkSpaceCellHeightOnce();

            syncCompactTimelineRowHeights();

            wireTimelineVerticalScrollSync();

            wireAppointmentLabelPinOnScroll();

        } finally {

            timelineLayoutLock = false;

        }

    }



    function scheduleSchedulerChromeRefresh(t) {

        if (chromeRefreshTimer) {

            clearTimeout(chromeRefreshTimer);

        }

        chromeRefreshTimer = setTimeout(function () {

            refreshTimelineLayout();

            patchSchedulerViewHeaderLabel(t);

            purgeSchedulerOrphanAppointments();

        }, 50);

    }



    function initCalendarToolbar(board, t) {

        const $shell = $hostRoot && $hostRoot.closest(".room-board-calendar-shell");

        if (!$shell || !$shell.length) {

            return;

        }



        const lookups = board && board.lookups ? board.lookups : {};

        const rooms = (board && board.rooms) || [];

        const roomTypes = lookups.roomTypes || uniqueRoomTypesFromRooms(rooms);



        const $search = $shell.find("#roomBoardCalendarSearch");

        if ($search.length && !$search.data("dxTextBox")) {

            $search.dxTextBox({

                mode: "search",

                label: t("roomBoard.calendarSearch"),

                labelMode: "floating",

                showClearButton: true,

                value: calendarClientFilter.search,

                onValueChanged(e) {

                    calendarClientFilter.search = e.value || "";

                    persistCalendarClientFilter();

                    applyCalendarClientFilters();

                }

            });

        } else if ($search.length) {

            const searchInst = $search.dxTextBox("instance");

            if (searchInst) {

                searchInst.option({

                    label: t("roomBoard.calendarSearch"),

                    value: calendarClientFilter.search

                });

            }

        }



        const $roomType = $shell.find("#roomBoardCalendarRoomType");

        const roomTypeData = mapCalendarRoomTypeOptions(roomTypes, t);

        if ($roomType.length && !$roomType.data("dxTagBox")) {

            $roomType.dxTagBox({

                label: t("roomBoard.roomType"),

                labelMode: "floating",

                dataSource: roomTypeData,

                valueExpr: "id",

                displayExpr: "text",

                showClearButton: true,

                showSelectionControls: true,

                applyValueMode: "useButtons",

                multiline: false,

                maxDisplayedTags: 1,

                value: calendarClientFilter.roomTypeIds,

                onValueChanged(e) {

                    calendarClientFilter.roomTypeIds = e.value || [];

                    persistCalendarClientFilter();

                    applyCalendarClientFilters();

                }

            });

        } else if ($roomType.length) {

            const tagInst = $roomType.dxTagBox("instance");

            if (tagInst) {

                tagInst.option({

                    label: t("roomBoard.roomType"),

                    dataSource: roomTypeData,

                    value: calendarClientFilter.roomTypeIds

                });

            }

        }



        const $viewToggle = $shell.find("#roomBoardCalendarViewToggle");

        const viewItems = [

            { key: "week", text: t("roomBoard.viewWeek") },

            { key: "month", text: t("roomBoard.viewMonth") }

        ];

        if ($viewToggle.length && !$viewToggle.data("dxButtonGroup")) {

            $viewToggle.dxButtonGroup({

                items: viewItems,

                keyExpr: "key",

                selectedItemKeys: [calendarViewKey],

                stylingMode: "contained",

                onSelectionChanged(e) {

                    const added = e.addedItems && e.addedItems[0];

                    if (added && added.key) {

                        setCalendarView(added.key);

                    }

                }

            });

        } else if ($viewToggle.length) {

            const viewInst = $viewToggle.dxButtonGroup("instance");

            if (viewInst) {

                viewInst.option({

                    items: viewItems,

                    selectedItemKeys: [calendarViewKey]

                });

            }

        }

        const $today = $shell.find("#roomBoardCalendarTodayBtn");

        if ($today.length && !$today.data("dxButton")) {

            $today.dxButton({

                text: t("roomBoard.calendarToday"),

                type: "default",

                stylingMode: "contained",

                onClick() {

                    if (!schedulerInstance) {

                        return;

                    }

                    schedulerInstance.option("currentDate", new Date());

                }

            });

        } else if ($today.length) {

            const todayInst = $today.dxButton("instance");

            if (todayInst) {

                todayInst.option("text", t("roomBoard.calendarToday"));

            }

        }

    }



    function openReservationUrl(reservationId) {

        const params = new URLSearchParams();

        params.set("id", String(reservationId));

        const hc = window.Zaaer.ApiService && window.Zaaer.ApiService.getHotelCode

            ? window.Zaaer.ApiService.getHotelCode()

            : "";

        if (hc) {

            params.set("hotelCode", hc);

        }



        return `/reservation-detail.html?${params.toString()}`;

    }



    function ensureCalendarShell(containerSelector) {

        const $panel = $(containerSelector);

        if (!$panel.length) {

            return $();

        }



        $panel.addClass("room-board-panel--calendar");

        let $shell = $panel.children(".room-board-calendar-shell");

        if (!$shell.length) {

            $shell = $("<div>").addClass("room-board-calendar-shell").appendTo($panel);

            $("<div>").addClass("room-board-calendar-legend").appendTo($shell);

            $("<div>").addClass("room-board-calendar-scheduler-host").appendTo($shell);

        }



        if (!$shell.children(".room-board-calendar-toolbar").length) {

            const $toolbar = $("<div>").addClass("room-board-calendar-toolbar").prependTo($shell);

            $("<div>")

                .addClass("room-board-calendar-toolbar-filters")

                .append($("<div>").attr("id", "roomBoardCalendarSearch").addClass("room-board-calendar-search"))

                .append($("<div>").attr("id", "roomBoardCalendarRoomType").addClass("room-board-calendar-room-type"))

                .appendTo($toolbar);

            $("<div>").attr("id", "roomBoardCalendarViewToggle").addClass("room-board-calendar-view-toggle").appendTo($toolbar);

            $("<div>").attr("id", "roomBoardCalendarTodayBtn").addClass("room-board-calendar-today-btn").appendTo($toolbar);

        }



        return $shell.find(".room-board-calendar-scheduler-host");

    }



    function renderLegend(t) {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }



        const $legend = $hostRoot.closest(".room-board-calendar-shell").find(".room-board-calendar-legend");

        $legend.empty();



        $("<span>").addClass("room-board-calendar-legend-title").text(t("roomBoard.calendarLegend")).appendTo($legend);



        const $items = $("<div>").addClass("room-board-calendar-legend-items").appendTo($legend);

        LEGEND_STATUSES.forEach((item) => {

            $("<span>")

                .addClass(`room-board-calendar-legend-item ${item.cssClass}`)

                .append($("<i>").addClass("room-board-calendar-legend-swatch"))

                .append($("<span>").text(t(item.labelKey)))

                .appendTo($items);

        });

    }



    function buildAppointmentElement(data) {

        const statusClass = data.statusCssClass || (data.type ? `status-${data.type}` : "");

        const $app = $("<div>").addClass(`room-scheduler-appointment ${statusClass}`.trim());



        const code = data.reservationNo || (data.text ? `${data.text}`.split(" - ")[0] : "");

        const guest =

            data.guestName ||

            (data.text && data.text.includes(" - ") ? data.text.split(" - ").slice(1).join(" - ") : "");



        const arrival = data.stayStartDate || data.startDate;

        const departure =

            data.stayEndDate ||

            (() => {

                const d = new Date(data.endDate);

                d.setDate(d.getDate() - 1);

                return d;

            })();



        const titleText = guest || code || "—";
        const $line = $("<span>")
            .addClass("room-scheduler-appointment__line room-scheduler-appointment__label-sticky")
            .appendTo($app);

        const roomCode = data.roomCode ? String(data.roomCode).trim() : "";

        if (roomCode) {
            $("<span>").addClass("room-scheduler-appointment__room").text(roomCode).appendTo($line);
            $("<span>").addClass("room-scheduler-appointment__sep").text(" · ").appendTo($line);
        }

        const $title = $("<span>").addClass("room-scheduler-appointment__title").text(titleText).appendTo($line);
        if (guest && code) {
            $("<span>").addClass("room-scheduler-appointment__res-no").text(` (${code})`).appendTo($title);
        }

        $("<span>").addClass("room-scheduler-appointment__sep").text(" · ").appendTo($line);

        const $dates = $("<span>").addClass("room-scheduler-appointment__dates").appendTo($line);
        $("<span>").addClass("room-scheduler-appointment__date-in").text(formatShortDate(arrival)).appendTo($dates);
        $("<span>")
            .addClass("room-scheduler-appointment__date-arrow")
            .attr("aria-hidden", "true")
            .text("→")
            .appendTo($dates);
        $("<span>").addClass("room-scheduler-appointment__date-out").text(formatShortDate(departure)).appendTo($dates);



        return $app;

    }



    function buildTooltipElement(data) {

        const t = translateFn;

        const statusKey = data.statusLabel ? `status.${data.statusLabel}` : "";

        const statusText = statusKey && t(statusKey) !== statusKey ? t(statusKey) : data.statusLabel || "—";

        const rentalKey = data.rentalType ? `rental.${data.rentalType}` : "";

        const rentalText =

            rentalKey && t(rentalKey) !== rentalKey ? t(rentalKey) : data.rentalType || "—";



        const departure =

            data.stayEndDate ||

            (() => {

                const d = new Date(data.endDate);

                d.setDate(d.getDate() - 1);

                return d;

            })();



        const $tip = $("<div>").addClass("room-scheduler-tooltip");

        const rows = [

            [t("roomBoard.calendarTooltip.reservation"), data.reservationNo || "—"],

            [t("roomBoard.calendarTooltip.guest"), data.guestName || "—"],

            [t("roomBoard.calendarTooltip.arrival"), formatShortDate(data.stayStartDate || data.startDate)],

            [t("roomBoard.calendarTooltip.departure"), formatShortDate(departure)],

            [t("roomBoard.calendarTooltip.rental"), rentalText],

            [t("roomBoard.calendarTooltip.status"), statusText]

        ];



        rows.forEach(([label, value]) => {

            $("<div>")

                .addClass("room-scheduler-tooltip-row")

                .append($("<span>").addClass("room-scheduler-tooltip-k").text(label))

                .append($("<span>").addClass("room-scheduler-tooltip-v").text(value == null ? "—" : String(value)))

                .appendTo($tip);

        });



        return $tip;

    }



    function resolveApartmentIdFromTarget($target) {

        const raw =

            $target.closest("[data-apartment-id]").attr("data-apartment-id") ||

            $target.find("[data-apartment-id]").first().attr("data-apartment-id");

        if (raw === undefined || raw === null || raw === "") {

            return null;

        }



        const n = Number(raw);

        return Number.isFinite(n) ? n : raw;

    }



    function roomForContextMenu(apartmentId, appointmentData) {

        const base = roomByApartmentId[apartmentId];

        if (!base) {

            return null;

        }



        const room = { ...base };

        if (appointmentData && appointmentData.reservationId) {

            room.currentStay = {

                ...(room.currentStay || {}),

                reservationId: appointmentData.reservationId

            };

        }



        return room;

    }



    function extractAppointmentData($appointmentEl) {

        const stored = $appointmentEl.data("rbAppt");

        if (stored) {

            return stored;

        }



        if (!schedulerInstance || typeof schedulerInstance.getTargetedAppointmentData !== "function") {

            return null;

        }



        try {

            return schedulerInstance.getTargetedAppointmentData($appointmentEl);

        } catch {

            return null;

        }

    }



    function showSchedulerContextMenu(room, ev) {

        const cardView = window.Zaaer && window.Zaaer.RoomCardView;

        if (!cardView || !room || typeof cardView.buildContextMenuItems !== "function" || !ctxMenuInst) {

            return;

        }



        const t = translateFn;

        ctxRoom = room;

        ctxMenuInst.option("items", cardView.buildContextMenuItems(room, t));

        ctxMenuInst.option("position", {

            of: ev,

            my: "left top",

            at: "right top",

            collision: "flip fit"

        });

        ctxMenuInst.show();

    }



    function ensureSchedulerContextMenu(t) {

        const cardView = window.Zaaer && window.Zaaer.RoomCardView;

        if (!cardView || typeof cardView.buildContextMenuItems !== "function") {

            return;

        }



        let $menu = $("#roomBoardSchedulerCtxMenu");

        if (!$menu.length) {

            $menu = $("<div id='roomBoardSchedulerCtxMenu'>").appendTo("body");

            const itemTemplate =

                typeof cardView.renderContextMenuItemTemplate === "function"

                    ? cardView.renderContextMenuItemTemplate

                    : null;



            $menu.dxContextMenu({

                width: 248,

                cssClass: "room-board-context-menu",

                items: [],

                showEvent: "",

                hideOnOutsideClick: true,

                focusStateEnabled: false,

                itemTemplate: itemTemplate || undefined,

                onItemClick(e) {

                    const item = e.itemData || {};

                    const room = ctxRoom;

                    if (!room || !item.id) {

                        return;

                    }



                    if (typeof cardView.handleContextMenuAction === "function") {

                        cardView.handleContextMenuAction(room, item, t, onBoardRefreshCb);

                    }

                }

            });

            ctxMenuInst = $menu.dxContextMenu("instance");

        } else if (!ctxMenuInst) {

            ctxMenuInst = $menu.dxContextMenu("instance");

        }

    }



    function wireSchedulerContextMenu(t) {

        if (!$hostRoot || !$hostRoot.length) {

            return;

        }



        ensureSchedulerContextMenu(t);



        $hostRoot.off(".rbSchedCtx");

        $hostRoot.on("contextmenu.rbSchedCtx", ".dx-scheduler-appointment", function (ev) {

            ev.preventDefault();

            if (ctxMenuInst) {

                try {

                    ctxMenuInst.hide();

                } catch {

                    /* not open */

                }

            }



            const appointmentData = extractAppointmentData($(this));

            const apartmentId = appointmentData ? appointmentData.apartmentId : null;

            const room = roomForContextMenu(apartmentId, appointmentData);

            if (!room) {

                return;

            }



            showSchedulerContextMenu(room, ev);

        });



        $hostRoot.on("contextmenu.rbSchedCtx", ".dx-scheduler-group-header", function (ev) {

            ev.preventDefault();

            if (ctxMenuInst) {

                try {

                    ctxMenuInst.hide();

                } catch {

                    /* not open */

                }

            }



            const apartmentId = resolveApartmentIdFromTarget($(this));

            const room = roomForContextMenu(apartmentId, null);

            if (!room) {

                return;

            }



            showSchedulerContextMenu(room, ev);

        });

    }



    function init(containerSelector, board, t, options) {

        const opts = options && typeof options === "object" ? options : {};

        const currentDate = opts.currentDate || opts.date || new Date();

        onBoardRefreshCb = typeof opts.onBoardRefresh === "function" ? opts.onBoardRefresh : null;

        translateFn = typeof t === "function" ? t : translateFn;

        restoreCalendarClientFilter();

        calendarViewKey = loadCalendarViewKey();

        schedulerDateRange = resolveSchedulerDateRange(opts, board);



        const $schedulerHost = ensureCalendarShell(containerSelector);

        $hostRoot = $schedulerHost;

        renderLegend(t);



        const rooms = (board && board.rooms) || [];

        buildRoomResourceMap(rooms);



        const resources = rooms

            .map((room) => toRoomResource(room, t))

            .filter((resource) => resource.id != null);

        const appointments = filterAppointmentsForResources(

            ((board && board.calendarItems) || []).map(toSchedulerAppointment).filter(Boolean),

            resources

        );

        rawResources = resources;

        rawAppointments = appointments;

        rebuildAppointmentDataMap(appointments);



        if (schedulerInstance) {

            try {

                schedulerInstance.dispose();

            } catch {

                /* already disposed */

            }

            schedulerInstance = null;

        }

        timelineLayoutLock = false;

        timelineWorkSpaceCellHeightSet = false;



        let navigateDebounce = null;

        schedulerInstance = $schedulerHost

            .dxScheduler({

                dataSource: appointments,

                resources: [

                    {

                        fieldExpr: "apartmentId",

                        label: t("roomBoard.room"),

                        dataSource: resources,

                        useColorAsDefault: false

                    }

                ],

                groups: ["apartmentId"],

                groupOrientation: "vertical",

                views: [

                    {

                        type: "timelineWeek",

                        name: "week",

                        intervalCount: 1,

                        groupOrientation: "vertical"

                    },

                    {

                        type: "timelineMonth",

                        name: "month",

                        intervalCount: schedulerDateRange.monthCount,

                        groupOrientation: "vertical"

                    }

                ],

                currentView: calendarViewKey,
                currentDate: currentDate,

                min: schedulerDateRange.min,

                max: schedulerDateRange.max,

                firstDayOfWeek: 6,

                height: "calc(100vh - var(--room-board-chrome-top, 148px) - 132px)",

                width: "100%",

                adaptivityEnabled: true,

                crossScrollingEnabled: true,

                showAllDayPanel: false,

                showCurrentTimeIndicator: true,

                shadeUntilCurrentTime: false,

                maxAppointmentsPerCell: "unlimited",

                appointmentTooltipEnabled: false,

                cellDuration: 1440,

                startDayHour: 0,

                endDayHour: 24,

                rtlEnabled: isRtl(),

                editing: {

                    allowAdding: false,

                    allowDeleting: false,

                    allowDragging: false,

                    allowResizing: false,

                    allowUpdating: false

                },

                appointmentTemplate(_model, _index, element) {

                    const data =

                        (_model && _model.appointmentData) ||

                        (_model && _model.targetedAppointmentData) ||

                        _model ||

                        {};

                    const $root = $(element).empty();

                    const $app = buildAppointmentElement(data);

                    $root.append($app);

                    $root.data("rbAppt", data);

                    $app.data("rbAppt", data);

                    if (data.id != null) {

                        $root.attr("data-rb-appt-id", String(data.id));

                    }

                    $root.attr("title", "");

                },

                resourceCellTemplate(cellData) {

                    const resource = cellData.data || cellData;

                    const code = resource.text || "—";

                    const type = resource.roomTypeName || "";

                    const line = type ? `${code} · ${type}` : code;

                    const $cell = $("<div>")

                        .addClass("room-scheduler-resource room-scheduler-resource--compact")

                        .attr("data-apartment-id", resource.id)

                        .attr("title", line);



                    $("<span>").addClass("room-scheduler-resource__line").text(line).appendTo($cell);



                    return $cell;

                },

                onAppointmentClick(e) {

                    const data = e.appointmentData;

                    if (data && data.reservationId) {

                        window.location.href = openReservationUrl(data.reservationId);

                    }

                },

                onAppointmentDblClick(e) {

                    e.cancel = true;

                },
                onOptionChanged(e) {
                    if (!e) {
                        return;
                    }

                    if (e.name === "currentView") {

                        scheduleSchedulerChromeRefresh(t);

                        return;

                    }

                    if (e.name !== "currentDate" || !e.value) {
                        return;
                    }

                    if (typeof opts.onNavigate !== "function") {
                        return;
                    }

                    if (navigateDebounce) {
                        clearTimeout(navigateDebounce);
                    }

                    navigateDebounce = setTimeout(() => {
                        opts.onNavigate(e.value);
                    }, 80);
                },

                onCellPrepared(e) {

                    if (!e.cellElement || (e.cellType !== "data" && e.cellType !== "group")) {

                        return;

                    }

                    const groups = e.cellData && e.cellData.groups;

                    const apartmentId = groups && groups.apartmentId;

                    const room = apartmentId != null ? roomByApartmentId[apartmentId] : null;

                    if (!room) {

                        return;

                    }

                    const statusKey = (room.statusCssClass || "status-available").replace(/^status-/, "");

                    const statusClass = `room-scheduler-data-cell--${statusKey || "available"}`;

                    $(e.cellElement)
                        .addClass("room-scheduler-data-cell")
                        .addClass(statusClass)
                        .removeClass(
                            "room-scheduler-data-cell--available room-scheduler-data-cell--reserved room-scheduler-data-cell--occupied room-scheduler-data-cell--cleaning room-scheduler-data-cell--maintenance"
                        )
                        .addClass(statusClass);

                },

                onInitialized(e) {

                    const inst = e && e.component;

                    if (!inst || typeof inst.getWorkSpace !== "function") {

                        return;

                    }

                    try {

                        const ws = inst.getWorkSpace();

                        if (ws && typeof ws.option === "function") {

                            ws.option("cellHeight", TIMELINE_COMPACT_ROW_PX);

                            timelineWorkSpaceCellHeightSet = true;

                        }

                    } catch {

                        /* workspace not ready yet */

                    }

                },

                onContentReady() {

                    wireSchedulerContextMenu(t);

                    scheduleSchedulerChromeRefresh(t);

                }

            })

            .dxScheduler("instance");



        initCalendarToolbar(board, t);

        applyCalendarClientFilters();



        return schedulerInstance;

    }



    function update(board, currentDate, dateRange) {

        if (!schedulerInstance) {

            return;

        }



        const rooms = (board && board.rooms) || [];

        buildRoomResourceMap(rooms);

        const resources = rooms

            .map((room) => toRoomResource(room, translateFn))

            .filter((resource) => resource.id != null);

        const appointments = filterAppointmentsForResources(

            ((board && board.calendarItems) || []).map(toSchedulerAppointment).filter(Boolean),

            resources

        );

        rawResources = resources;

        rawAppointments = appointments;

        rebuildAppointmentDataMap(appointments);



        schedulerDateRange = resolveSchedulerDateRange(

            { currentDate: currentDate || schedulerInstance.option("currentDate"), dateRange },

            board

        );



        const resourceLabel =
            schedulerInstance.option("resources") &&
            schedulerInstance.option("resources")[0] &&
            schedulerInstance.option("resources")[0].label;

        schedulerInstance.option({
            currentDate: currentDate || schedulerInstance.option("currentDate"),
            min: schedulerDateRange.min,
            max: schedulerDateRange.max,
            "views[1].intervalCount": schedulerDateRange.monthCount,
            resources: [
                {
                    fieldExpr: "apartmentId",
                    label: resourceLabel,
                    dataSource: resources,
                    useColorAsDefault: false
                }
            ]
        });

        initCalendarToolbar(board, translateFn);

        applyCalendarClientFilters();

        scheduleSchedulerChromeRefresh(translateFn);

    }



    window.Zaaer = window.Zaaer || {};

    window.Zaaer.RoomBoardScheduler = {

        init,

        update

    };

})(window, jQuery, DevExpress);

