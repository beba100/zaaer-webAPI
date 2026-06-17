(function (window, $, DevExpress) {
    "use strict";

    /** Match reservation-detail.js consumption key */
    const NEW_RESERVATION_CTX_KEY = "zaaer.pms.newReservationContext";
    /** DevExtreme icon for reserved / awaiting arrival (distinct from money / unpaid). */
    const RESERVED_ARRIVAL_ICON = "clock";
    const CARDS_EXPAND_STORAGE_KEY = "zaaer.roomBoard.cardsExpand.v1";
    const CARDS_FLOOR_GROUP_STORAGE_KEY = "zaaer.roomBoard.cardsFloorGroup.v1";
    /** Reset — no custom tint on occupied card. */
    const OCCUPIED_CARD_COLOR_DEFAULT = {
        id: "",
        textKey: "roomBoard.cardColors.default",
        back: "",
        text: ""
    };

    /** Medium-calm tints: clear on a dense room board without harsh saturation. */
    const OCCUPIED_CARD_COLOR_PRESETS_CALM = [
        { id: "blue-calm", textKey: "roomBoard.cardColors.presetBlue", back: "#93c5fd", text: "#1e3a8a" },
        { id: "teal-calm", textKey: "roomBoard.cardColors.presetTeal", back: "#5eead4", text: "#115e59" },
        { id: "green-calm", textKey: "roomBoard.cardColors.presetGreen", back: "#86efac", text: "#166534" },
        { id: "sage-calm", textKey: "roomBoard.cardColors.presetSage", back: "#6ee7b7", text: "#065f46" },
        { id: "sky-calm", textKey: "roomBoard.cardColors.presetSky", back: "#7dd3fc", text: "#075985" },
        { id: "indigo-calm", textKey: "roomBoard.cardColors.presetIndigo", back: "#a5b4fc", text: "#3730a3" },
        { id: "lavender-calm", textKey: "roomBoard.cardColors.presetPurple", back: "#c4b5fd", text: "#5b21b6" },
        { id: "sand-calm", textKey: "roomBoard.cardColors.presetSand", back: "#fcd34d", text: "#92400e" },
        { id: "yellow-calm", textKey: "roomBoard.cardColors.presetYellow", back: "#fde68a", text: "#78350f" },
        { id: "peach-calm", textKey: "roomBoard.cardColors.presetOrange", back: "#fdba74", text: "#9a3412" },
        { id: "rose-calm", textKey: "roomBoard.cardColors.presetRose", back: "#fda4af", text: "#9f1239" },
        { id: "slate-calm", textKey: "roomBoard.cardColors.presetGray", back: "#cbd5e1", text: "#334155" }
    ];

    const OCCUPIED_CARD_COLOR_PRESETS_SOFT = [
        { id: "yellow-soft", textKey: "roomBoard.cardColors.presetYellow", back: "#fef9c3", text: "#713f12" },
        { id: "blue-soft", textKey: "roomBoard.cardColors.presetBlue", back: "#dbeafe", text: "#1e3a8a" },
        { id: "green-soft", textKey: "roomBoard.cardColors.presetGreen", back: "#dcfce7", text: "#166534" },
        { id: "red-soft", textKey: "roomBoard.cardColors.presetRed", back: "#fee2e2", text: "#991b1b" },
        { id: "gray-soft", textKey: "roomBoard.cardColors.presetGray", back: "#f1f5f9", text: "#334155" },
        { id: "purple-soft", textKey: "roomBoard.cardColors.presetPurple", back: "#f3e8ff", text: "#6b21a8" },
        { id: "teal-soft", textKey: "roomBoard.cardColors.presetTeal", back: "#ccfbf1", text: "#115e59" },
        { id: "sky-soft", textKey: "roomBoard.cardColors.presetSky", back: "#e0f2fe", text: "#075985" },
        { id: "orange-soft", textKey: "roomBoard.cardColors.presetOrange", back: "#ffedd5", text: "#9a3412" },
        { id: "lime-soft", textKey: "roomBoard.cardColors.presetLime", back: "#ecfccb", text: "#365314" },
        { id: "sage-soft", textKey: "roomBoard.cardColors.presetSage", back: "#d1fae5", text: "#065f46" },
        { id: "sand-soft", textKey: "roomBoard.cardColors.presetSand", back: "#fef3c7", text: "#92400e" },
        { id: "indigo-soft", textKey: "roomBoard.cardColors.presetIndigo", back: "#e0e7ff", text: "#3730a3" }
    ];

    const OCCUPIED_CARD_PALETTE_DEFAULT_GROUP = "calm";

    function getOccupiedCardPaletteGroups() {
        return [
            {
                id: "calm",
                textKey: "roomBoard.cardColors.groupCalm",
                presets: OCCUPIED_CARD_COLOR_PRESETS_CALM
            },
            {
                id: "soft",
                textKey: "roomBoard.cardColors.groupSoft",
                presets: OCCUPIED_CARD_COLOR_PRESETS_SOFT
            }
        ];
    }

    let allCardsExpanded = false;
    let cardsGroupByFloor = false;
    let lastCardsRenderCtx = null;
    const individuallyExpandedRooms = new Set();
    const individuallyCollapsedRooms = new Set();

    function persistCardsExpandState() {
        try {
            localStorage.setItem(
                CARDS_EXPAND_STORAGE_KEY,
                JSON.stringify({
                    allCardsExpanded: !!allCardsExpanded,
                    expanded: Array.from(individuallyExpandedRooms),
                    collapsed: Array.from(individuallyCollapsedRooms)
                })
            );
        } catch {
            /* storage unavailable */
        }
    }

    function restoreCardsExpandState() {
        try {
            const raw = localStorage.getItem(CARDS_EXPAND_STORAGE_KEY);
            if (!raw) {
                return;
            }

            const saved = JSON.parse(raw);
            allCardsExpanded = !!saved.allCardsExpanded;
            individuallyExpandedRooms.clear();
            individuallyCollapsedRooms.clear();

            if (Array.isArray(saved.expanded)) {
                saved.expanded.forEach((key) => {
                    const k = `${key ?? ""}`.trim();
                    if (k) {
                        individuallyExpandedRooms.add(k);
                    }
                });
            }

            if (Array.isArray(saved.collapsed)) {
                saved.collapsed.forEach((key) => {
                    const k = `${key ?? ""}`.trim();
                    if (k) {
                        individuallyCollapsedRooms.add(k);
                    }
                });
            }
        } catch {
            /* ignore corrupt storage */
        }
    }

    function persistCardsFloorGroupState() {
        try {
            localStorage.setItem(
                CARDS_FLOOR_GROUP_STORAGE_KEY,
                JSON.stringify({ groupByFloor: !!cardsGroupByFloor })
            );
        } catch {
            /* storage unavailable */
        }
    }

    function restoreCardsFloorGroupState() {
        try {
            const raw = localStorage.getItem(CARDS_FLOOR_GROUP_STORAGE_KEY);
            if (!raw) {
                return;
            }

            const saved = JSON.parse(raw);
            cardsGroupByFloor = !!saved.groupByFloor;
        } catch {
            /* ignore corrupt storage */
        }
    }

    restoreCardsFloorGroupState();

    function rerenderLastCards() {
        if (!lastCardsRenderCtx) {
            return;
        }

        render(
            lastCardsRenderCtx.containerSelector,
            lastCardsRenderCtx.rooms,
            lastCardsRenderCtx.t,
            lastCardsRenderCtx.options
        );
    }

    function cardRoomSortKey(room) {
        const code = `${room.apartmentCode || room.apartmentName || ""}`.trim();
        const num = parseInt(code.replace(/\D/g, ""), 10);
        return Number.isFinite(num) ? num : code;
    }

    function deriveCardFloorKey(room) {
        const floorName = `${room.floorName || ""}`.trim();
        if (floorName) {
            return floorName;
        }

        const code = `${room.apartmentCode || ""}`.trim();
        const match = code.match(/^(\d{1,3})/);
        if (match) {
            const prefix = match[1];
            if (prefix.length >= 2) {
                const hundreds = prefix.length >= 3 ? prefix.charAt(0) : prefix;
                return `${hundreds}00`;
            }
        }

        return "—";
    }

    function groupRoomsForCardFloors(rooms) {
        const buildingMap = new Map();

        (rooms || []).forEach((room) => {
            const buildingKey = `${room.buildingName || ""}`.trim() || "__default__";
            if (!buildingMap.has(buildingKey)) {
                buildingMap.set(buildingKey, new Map());
            }

            const floorKey = deriveCardFloorKey(room);
            const floors = buildingMap.get(buildingKey);
            if (!floors.has(floorKey)) {
                floors.set(floorKey, []);
            }

            floors.get(floorKey).push(room);
        });

        const buildings = [];
        buildingMap.forEach((floors, buildingKey) => {
            const floorList = [];
            floors.forEach((floorRooms, floorKey) => {
                floorRooms.sort((a, b) => {
                    const ka = cardRoomSortKey(a);
                    const kb = cardRoomSortKey(b);
                    if (typeof ka === "number" && typeof kb === "number") {
                        return ka - kb;
                    }

                    return `${ka}`.localeCompare(`${kb}`, undefined, { numeric: true });
                });

                floorList.push({
                    floorKey,
                    rooms: floorRooms,
                    count: floorRooms.length
                });
            });

            floorList.sort((a, b) =>
                `${a.floorKey}`.localeCompare(`${b.floorKey}`, undefined, { numeric: true })
            );

            buildings.push({
                buildingKey,
                floors: floorList
            });
        });

        return buildings;
    }

    function appendCardFloorHeader($section, floor, t) {
        const $header = $("<div>").addClass("room-card-floor-header").appendTo($section);
        $("<span>")
            .addClass("room-card-floor-title")
            .text(`${t("roomBoard.rack.floor")} ${floor.floorKey}`)
            .appendTo($header);
        $("<span>")
            .addClass("room-card-floor-count")
            .text(`(${floor.count || 0})`)
            .appendTo($header);
    }

    restoreCardsExpandState();

    function normalizeStatusKey(value) {
        return `${value ?? ""}`.trim().toLowerCase().replace(/_/g, "-");
    }

    function formatBalance(value) {
        const amount = Number(value || 0);

        if (amount === 0) {
            return "";
        }

        return `(${amount.toLocaleString("en-US", {
            minimumFractionDigits: 0,
            maximumFractionDigits: 2
        })})`;
    }

    function persistNewReservationContext(room) {
        const hc = window.Zaaer.ApiService.getHotelCode();
        const payload = {
            apartmentId: room.internalApartmentId != null ? room.internalApartmentId : room.apartmentId,
            apartmentZaaerId: room.apartmentId,
            apartmentCode: room.apartmentCode,
            apartmentName: room.apartmentName,
            roomTypeName: room.roomTypeName,
            buildingName: room.buildingName,
            floorName: room.floorName,
            hotelId: room.hotelId != null ? Number(room.hotelId) : null,
            hotelCode: hc || null
        };
        try {
            window.sessionStorage.setItem(NEW_RESERVATION_CTX_KEY, JSON.stringify(payload));
        } catch {
            /* ignore quota / private mode */
        }
    }

    function openReservationUrl(room, reservationId) {
        const params = new URLSearchParams();
        params.set("id", String(reservationId));
        const hc = window.Zaaer.ApiService.getHotelCode();
        if (hc) {
            params.set("hotelCode", hc);
        }
        return `/reservation-detail.html?${params.toString()}`;
    }

    function newReservationUrl(room) {
        persistNewReservationContext(room);
        return "/reservation-detail.html?newReservation=1";
    }

    function shortDateText(value) {
        const s = `${value ?? ""}`.trim();
        if (!s || s === "--/--") {
            return "";
        }
        return s;
    }

    function appendRoomCardDateChip($dates, kind, dateText) {
        if (!$dates || !$dates.length || !dateText) {
            return;
        }

        const $chip = $("<div>").addClass(`room-card-date-chip room-card-date-chip--${kind}`);
        $("<span>").addClass("room-card-date-chip-value").attr("dir", "ltr").text(dateText).appendTo($chip);
        $chip.appendTo($dates);
    }

    function isOccupiedRoom(room) {
        const x = `${room && room.operationalStatus ? room.operationalStatus : ""}`.toLowerCase();
        return x === "occupied" || x === "rented";
    }

    function applyColorIfSet($el, prop, value) {
        const s = `${value || ""}`.trim();
        if (s) {
            $el.css(prop, s);
        }
    }

    function parseHexColor(value) {
        const s = `${value || ""}`.trim();
        if (!s) {
            return null;
        }

        const m = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(s);
        if (!m) {
            return null;
        }

        let hex = m[1];
        if (hex.length === 3) {
            hex = hex.split("").map((c) => c + c).join("");
        }

        const r = parseInt(hex.slice(0, 2), 16);
        const g = parseInt(hex.slice(2, 4), 16);
        const b = parseInt(hex.slice(4, 6), 16);
        return { r, g, b };
    }

    function relativeLuminance({ r, g, b }) {
        // WCAG relative luminance
        const toLinear = (v) => {
            const s = v / 255;
            return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
        };
        const R = toLinear(r);
        const G = toLinear(g);
        const B = toLinear(b);
        return 0.2126 * R + 0.7152 * G + 0.0722 * B;
    }

    function autoTextColorForBackground(backColor) {
        const rgb = parseHexColor(backColor);
        if (!rgb) {
            return "";
        }
        // Threshold tuned for UI pills; slightly favor dark text on bright colors.
        return relativeLuminance(rgb) > 0.55 ? "#0b1220" : "#ffffff";
    }

    function applyOccupiedCardVisual($card, $colorZone, room) {
        const $indicator = $card.find(".room-card-status-indicator");
        const $stayMeta = $colorZone.find(".room-card-stay-meta");
        const $guest = $colorZone.find(".room-card-guest");
        if (!roomHasCustomColors(room)) {
            $indicator.css("background-color", "");
            $colorZone.css({ "background-color": "", color: "" });
            $colorZone.removeClass("room-card-color-zone--tinted");
            $stayMeta.removeClass("room-card-stay-meta--tinted").css({
                "background-color": "",
                color: ""
            });
            $guest.removeClass("room-card-guest--tinted").css({
                "background-color": "",
                color: ""
            });
            $card.css("border-color", "");
            return;
        }

        const back = `${room.occupiedGuestBackColor || ""}`.trim();
        const explicitText = `${room.occupiedTextColor || ""}`.trim();
        const textColor = explicitText || autoTextColorForBackground(back);
        $indicator.css("background-color", "");
        $colorZone.removeClass("room-card-color-zone--tinted").css({
            "background-color": "",
            color: ""
        });
        $stayMeta.removeClass("room-card-stay-meta--tinted").css({
            "background-color": "",
            color: ""
        });
        $guest
            .addClass("room-card-guest--tinted")
            .css({
                "background-color": back || "",
                color: textColor || ""
            });
    }

    function roomHasCustomColors(room) {
        return !!(
            room &&
            (room.occupiedGuestBackColor || room.occupiedTextColor)
        );
    }

    function ensureHeaderActions($header) {
        let $actions = $header.children(".room-card-header-actions");
        if (!$actions.length) {
            $actions = $("<div>").addClass("room-card-header-actions").appendTo($header);
        }
        return $actions;
    }

    function roomKey(room) {
        return String(
            room.internalApartmentId ??
                room.apartmentId ??
                room.apartmentCode ??
                room.apartmentName ??
                ""
        );
    }

    function isAlwaysCollapsedCard(room) {
        const op = normalizeStatusKey(room.operationalStatus);
        const hk = normalizeStatusKey(room.housekeepingStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);

        if (op === "maintenance" || op === "cleaning") {
            return true;
        }

        if (op === "available" || apt === "vacant") {
            return true;
        }

        return false;
    }

    function canToggleCardExpanded(room) {
        return !isAlwaysCollapsedCard(room);
    }

    function isRoomExpanded(room) {
        if (isAlwaysCollapsedCard(room)) {
            return false;
        }

        const key = roomKey(room);
        if (!key) {
            return allCardsExpanded;
        }

        return allCardsExpanded
            ? !individuallyCollapsedRooms.has(key)
            : individuallyExpandedRooms.has(key);
    }

    function setRoomExpanded(room, expanded) {
        const key = roomKey(room);
        if (!key) {
            return;
        }

        if (allCardsExpanded) {
            if (expanded) {
                individuallyCollapsedRooms.delete(key);
            } else {
                individuallyCollapsedRooms.add(key);
            }
            persistCardsExpandState();
            return;
        }

        if (expanded) {
            individuallyExpandedRooms.add(key);
        } else {
            individuallyExpandedRooms.delete(key);
        }

        persistCardsExpandState();
    }

    function syncOccupiedCardColorForExpand($card, $colorZone, room, expanded) {
        if (!$colorZone || !$colorZone.length) {
            return;
        }

        if (expanded) {
            applyOccupiedCardVisual($card, $colorZone, room);
            return;
        }

        $colorZone.css({ "background-color": "", color: "" });
        $colorZone.removeClass("room-card-color-zone--tinted");
        $colorZone.find(".room-card-stay-meta").removeClass("room-card-stay-meta--tinted").css({
            "background-color": "",
            color: ""
        });
        $colorZone.find(".room-card-guest").removeClass("room-card-guest--tinted").css({
            "background-color": "",
            color: ""
        });
    }

    function applyCardExpandedState($card, room, expanded) {
        $card
            .toggleClass("is-expanded", expanded)
            .toggleClass("is-collapsed", !expanded)
            .attr("aria-expanded", expanded ? "true" : "false");
        $card.find(".room-card-code--toggle").attr("aria-expanded", expanded ? "true" : "false");
        syncOccupiedCardColorForExpand($card, $card.find(".room-card-color-zone"), room, expanded);
    }

    function wireRoomCodeToggle($code, $card, room, t) {
        function toggleExpanded(ev) {
            if (ev.type === "keydown" && ev.key !== "Enter" && ev.key !== " ") {
                return;
            }
            if (ev.type === "keydown") {
                ev.preventDefault();
            }
            ev.stopPropagation();

            const nextExpanded = !isRoomExpanded(room);
            setRoomExpanded(room, nextExpanded);
            applyCardExpandedState($card, room, nextExpanded);
        }

        const roomLabel = $code.attr("title") || $code.text() || "";
        $code
            .addClass("room-card-code--toggle")
            .attr({
                role: "button",
                tabindex: "0",
                "aria-expanded": isRoomExpanded(room) ? "true" : "false",
                "aria-label": `${roomLabel} — ${t("roomBoard.expandRoom")}`
            })
            .on("click", toggleExpanded)
            .on("keydown", toggleExpanded);
    }

    function applyAllCardExpandedState($container, expanded, t) {
        $container.find(".room-card").each(function () {
            const $card = $(this);
            const room = $card.data("roomBoardRoom");
            if (!room) {
                return;
            }

            if (isAlwaysCollapsedCard(room)) {
                applyCardExpandedState($card, room, false);
                return;
            }

            setRoomExpanded(room, expanded);
            applyCardExpandedState($card, room, isRoomExpanded(room));
        });
    }

    function renderRoomBoardActions(t, $container) {
        const $actions = $(".room-board-content-toolbar .room-board-actions");
        if (!$actions.length) {
            return;
        }

        $actions.empty().addClass("room-board-card-collapse-actions");

        $("<div>")
            .dxButton({
                icon: "chevrondown",
                hint: t("roomBoard.collapseAllRooms"),
                stylingMode: "outlined",
                type: "normal",
                elementAttr: { class: "room-board-collapse-all-btn" },
                onClick() {
                    allCardsExpanded = false;
                    individuallyExpandedRooms.clear();
                    individuallyCollapsedRooms.clear();
                    applyAllCardExpandedState($container, false, t);
                    persistCardsExpandState();
                }
            })
            .appendTo($actions);

        $("<div>")
            .dxButton({
                icon: "chevronup",
                hint: t("roomBoard.expandAllRooms"),
                stylingMode: "outlined",
                type: "normal",
                elementAttr: { class: "room-board-expand-all-btn" },
                onClick() {
                    allCardsExpanded = true;
                    individuallyExpandedRooms.clear();
                    individuallyCollapsedRooms.clear();
                    applyAllCardExpandedState($container, true, t);
                    persistCardsExpandState();
                }
            })
            .appendTo($actions);

        $("<div>")
            .dxButton({
                icon: "hierarchy",
                hint: cardsGroupByFloor
                    ? t("roomBoard.cardsFlatView")
                    : t("roomBoard.cardsGroupByFloor"),
                stylingMode: cardsGroupByFloor ? "contained" : "outlined",
                type: cardsGroupByFloor ? "default" : "normal",
                elementAttr: { class: "room-board-floor-group-btn" },
                onClick() {
                    cardsGroupByFloor = !cardsGroupByFloor;
                    persistCardsFloorGroupState();
                    rerenderLastCards();
                }
            })
            .appendTo($actions);
    }

    function appendBroomSymbol($parent) {
        $("<span>")
            .addClass("room-card-status-symbol room-card-symbol-broom")
            .appendTo($parent);
    }

    function appendOverstaySymbol($parent, extraClass) {
        const classes = ["room-card-symbol-overstay"];
        if (extraClass) {
            classes.push(extraClass);
        }

        return $("<span>")
            .addClass(classes.join(" "))
            .attr("aria-hidden", "true")
            .appendTo($parent);
    }

    /** Status icons aligned with top summary chips (check / red arrow / clock / broom / wrench). */
    function appendRoomOperationalStatusIcon($host, operationalStatus) {
        if (!$host || !$host.length) {
            return;
        }

        const key = normalizeStatusKey(operationalStatus);
        if (key === "cleaning") {
            appendBroomSymbol($host);
            return;
        }

        if (key === "occupied") {
            $("<span>")
                .addClass("room-status-pill-arrow")
                .attr("aria-hidden", "true")
                .append(
                    $("<span>")
                        .addClass("room-status-pill-arrow-glyph")
                        .attr("aria-hidden", "true")
                )
                .appendTo($host);
            return;
        }

        const iconByStatus = {
            available: "check",
            reserved: RESERVED_ARRIVAL_ICON,
            maintenance: "preferences"
        };
        const icon = iconByStatus[key];
        if (icon) {
            $("<i>").addClass(`dx-icon dx-icon-${icon}`).attr("aria-hidden", "true").appendTo($host);
        }
    }

    function renderRoomStatusPill(container, room, t) {
        const $container = $(container);
        if (!$container.length || !room) {
            return;
        }

        const statusClass = room.statusCssClass || "status-available";
        const statusKey = normalizeStatusKey(room.operationalStatus) || statusClass.replace(/^status-/, "");
        const labelKey = `status.${room.operationalStatus || statusKey}`;

        const $pill = $("<span>")
            .addClass(`room-status-pill ${statusClass}`)
            .attr("title", t(labelKey))
            .appendTo($container);

        const $icon = $("<span>").addClass("room-status-pill-icon").appendTo($pill);
        appendRoomOperationalStatusIcon($icon, statusKey);
        $("<span>").addClass("room-status-pill-label").text(t(labelKey)).appendTo($pill);
    }

    function shouldShowBroomInHeader(room) {
        const op = normalizeStatusKey(room.operationalStatus);
        const hk = normalizeStatusKey(room.housekeepingStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);
        const isVacant = op === "available" || apt === "vacant";
        return isVacant && hk === "dirty";
    }

    function shouldShowBroomBesideOccupiedBalance(room) {
        const op = normalizeStatusKey(room.operationalStatus);
        const hk = normalizeStatusKey(room.housekeepingStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);
        const isRentedLike = op === "occupied" || apt === "rented";
        return isRentedLike && (hk === "dirty" || hk === "cleaning");
    }

    function shouldShowBroomInFooter(room) {
        if (isAlwaysCollapsedCard(room)) {
            return false;
        }

        const op = normalizeStatusKey(room.operationalStatus);
        const hk = normalizeStatusKey(room.housekeepingStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);

        if (op === "maintenance" || op === "cleaning") {
            return false;
        }

        if (shouldShowBroomInHeader(room)) {
            return false;
        }

        if (shouldShowBroomBesideOccupiedBalance(room)) {
            return false;
        }

        const isRentedLike = op === "occupied" || apt === "rented";
        return isRentedLike && (hk === "dirty" || hk === "cleaning");
    }

    function roomHasUnpaidBalance(room) {
        return Number(room && room.balanceAmount) > 0;
    }

    function isWaitingForArrivalRoom(room) {
        return normalizeStatusKey(room && room.operationalStatus) === "reserved";
    }

    function isRoomUnderMaintenance(room) {
        return normalizeStatusKey(room && room.operationalStatus) === "maintenance";
    }

    function appendHeaderBalanceAmount($parent, room) {
        const balanceAmount = Number(room.balanceAmount || 0);
        const balanceText = formatBalance(balanceAmount);
        if (!balanceText) {
            return null;
        }

        return $("<span>")
            .addClass(`room-card-header-balance room-card-balance ${balanceAmount > 0 ? "is-positive" : "is-negative"}`)
            .text(balanceText)
            .attr("title", balanceText)
            .appendTo($parent);
    }

    function appendOpenReservationArrow($parent, room, reservationIdToOpen, t) {
        return $("<div>")
            .appendTo($parent)
            .dxButton({
                icon: "arrowup",
                hint: t("roomBoard.action.openReservation"),
                stylingMode: "text",
                type: "default",
                elementAttr: { class: "room-card-header-cal-btn room-card-header-open-btn" },
                onClick() {
                    window.location.href = openReservationUrl(room, reservationIdToOpen);
                }
            });
    }

    function appendCornerReservationBadge($header, room, kind, reservationIdToOpen, t) {
        const icon = kind === "reserved" ? RESERVED_ARRIVAL_ICON : "money";
        const hint =
            kind === "reserved" ? t("status.reserved") : t("summary.unpaidBalance");
        const styleClass =
            kind === "reserved"
                ? "room-card-reserved-corner-badge"
                : "room-card-unpaid-corner-badge";

        const classes = `room-card-corner-badge ${styleClass} dx-icon dx-icon-${icon}`;
        const canOpen =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;

        if (canOpen) {
            return $("<button>")
                .attr("type", "button")
                .addClass(classes)
                .attr("title", hint)
                .attr("aria-label", hint)
                .prependTo($header)
                .on("click", (ev) => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    window.location.href = openReservationUrl(room, reservationIdToOpen);
                });
        }

        return $("<span>")
            .addClass(classes)
            .attr("title", hint)
            .attr("aria-hidden", "true")
            .prependTo($header);
    }

    function formatOverstayBanner(room, t) {
        const days = Math.max(1, Number(room && room.overstayDays) || 1);
        if (days === 1) {
            return t("roomBoard.overstayBannerOne");
        }

        return t("roomBoard.overstayBanner").replace("{0}", String(days));
    }

    function appendOverstayCornerBadge($header, room, reservationIdToOpen, t) {
        const classes = "room-card-overstay-badge room-card-corner-badge";
        const hint = formatOverstayBanner(room, t);
        const canOpen =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;

        if (canOpen) {
            const $badge = $("<button>")
                .attr("type", "button")
                .addClass(classes)
                .attr("title", hint)
                .attr("aria-label", hint)
                .prependTo($header)
                .on("click", (ev) => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    window.location.href = openReservationUrl(room, reservationIdToOpen);
                });
            appendOverstaySymbol($badge);
            return $badge;
        }

        const $badge = $("<span>")
            .addClass(classes)
            .attr("title", hint)
            .attr("aria-hidden", "true")
            .prependTo($header);
        appendOverstaySymbol($badge);
        return $badge;
    }

    function appendDepartureCornerBadge($header, room, reservationIdToOpen, t) {
        const classes = "room-card-departure-badge room-card-corner-badge dx-icon dx-icon-runner";
        const hint = t("summary.departureToday");
        const canOpen =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;

        if (canOpen) {
            return $("<button>")
                .attr("type", "button")
                .addClass(classes)
                .attr("title", hint)
                .attr("aria-label", hint)
                .prependTo($header)
                .on("click", (ev) => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    window.location.href = openReservationUrl(room, reservationIdToOpen);
                });
        }

        return $("<span>")
            .addClass(classes)
            .attr("title", hint)
            .attr("aria-hidden", "true")
            .prependTo($header);
    }

    /**
     * Lead row: corner-badge cases → balance only (no red arrow).
     * Default → balance then red arrow (swapped positions).
     */
    function appendHeaderLead($header, room, context, t) {
        const op = normalizeStatusKey(room.operationalStatus);
        const waiting = isWaitingForArrivalRoom(room);
        const unpaid = roomHasUnpaidBalance(room);
        const overstay = !!room.isOverstay;
        const departure = !!room.isDepartureToday;
        const hasCornerStatusBadge = waiting || (unpaid && !departure && !overstay);
        const balanceText = formatBalance(room.balanceAmount);
        const { hasReservationToOpen, reservationIdToOpen } = context;

        const needsLead =
            balanceText ||
            overstay ||
            departure ||
            hasCornerStatusBadge ||
            op === "occupied" ||
            op === "reserved" ||
            (hasReservationToOpen && reservationIdToOpen);

        if (!needsLead) {
            return false;
        }

        const $actions = ensureHeaderActions($header);
        const $lead = $("<span>").addClass("room-card-header-lead").prependTo($actions);

        if (overstay || departure || hasCornerStatusBadge) {
            appendHeaderBalanceAmount($lead, room);
            if (op === "occupied" && shouldShowBroomBesideOccupiedBalance(room)) {
                appendBroomSymbol($lead);
            }
            return true;
        }

        $lead.addClass("room-card-header-lead--arrow-fallback");
        if (balanceText) {
            appendHeaderBalanceAmount($lead, room);
        }
        if (hasReservationToOpen && reservationIdToOpen) {
            appendOpenReservationArrow($lead, room, reservationIdToOpen, t);
        }
        if (op === "occupied" && shouldShowBroomBesideOccupiedBalance(room)) {
            appendBroomSymbol($lead);
        }

        return true;
    }

    function headerShowsBalance(room, context) {
        const op = normalizeStatusKey(room.operationalStatus);
        const waiting = isWaitingForArrivalRoom(room);
        const unpaid = roomHasUnpaidBalance(room);
        const overstay = !!room.isOverstay;
        const departure = !!room.isDepartureToday;
        const { hasReservationToOpen, reservationIdToOpen } = context;

        return (
            waiting ||
            unpaid ||
            overstay ||
            departure ||
            op === "occupied" ||
            op === "reserved" ||
            !!(hasReservationToOpen && reservationIdToOpen)
        );
    }

    function appendFooterOnlySymbol($quick, room) {
        const op = normalizeStatusKey(room.operationalStatus);

        if (op === "maintenance") {
            $("<span>")
                .addClass("room-card-main-icon dx-icon dx-icon-preferences")
                .appendTo($quick);
            return;
        }

        if (shouldShowBroomInFooter(room)) {
            appendBroomSymbol($quick);
        }
    }

    function shouldShowOccupiedDirtyFooter(room) {
        const op = normalizeStatusKey(room.operationalStatus);
        const hk = normalizeStatusKey(room.housekeepingStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);
        const isRentedLike = op === "occupied" || apt === "rented";
        return isRentedLike && (hk === "dirty" || hk === "cleaning");
    }

    function appendHeaderOperationalSymbol($header, room, t) {
        const op = normalizeStatusKey(room.operationalStatus);
        const $actions = ensureHeaderActions($header);

        if (op === "maintenance") {
            const maint = window.Zaaer && window.Zaaer.RoomMaintenanceBoardPopup;
            const hint =
                maint && typeof maint.formatMaintenanceTooltip === "function"
                    ? maint.formatMaintenanceTooltip(room, t)
                    : t("status.maintenance");
            $("<span>")
                .addClass(
                    "room-card-main-icon room-card-header-operational-icon room-card-maintenance-operational-icon dx-icon dx-icon-preferences"
                )
                .attr("title", hint)
                .attr("aria-label", hint)
                .appendTo($actions);
            return;
        }

        if (op === "cleaning") {
            appendBroomSymbol($actions);
        }
    }

    function appendHeaderCreateReservationButton($header, room, hasReservationToOpen, reservationIdToOpen, t) {
        $("<div>")
            .appendTo(ensureHeaderActions($header))
            .dxButton({
                icon: hasReservationToOpen ? "folder" : "plus",
                hint: hasReservationToOpen
                    ? t("roomBoard.action.openReservation")
                    : t("roomBoard.action.createReservation"),
                stylingMode: "contained",
                type: hasReservationToOpen ? "normal" : "success",
                elementAttr: {
                    class: hasReservationToOpen
                        ? "room-card-open-btn room-card-header-quick-btn"
                        : "room-card-open-btn room-card-create-btn room-card-header-quick-btn"
                },
                onClick() {
                    if (hasReservationToOpen) {
                        window.location.href = openReservationUrl(room, reservationIdToOpen);
                        return;
                    }
                    window.location.href = newReservationUrl(room);
                }
            });
    }

    function rbac() {
        return window.Zaaer && window.Zaaer.PmsRbacPolicy;
    }

    function isWaitingForArrival(room) {
        return isWaitingForArrivalRoom(room);
    }

    function renderContextMenuItemTemplate(itemData, _itemIndex, itemElement) {
        const $el = $(itemElement).empty();
        const $content = $("<div/>").addClass("dx-item-content").appendTo($el);

        if (itemData.menuIcon === "broom") {
            $("<span/>")
                .addClass("room-board-ctx-icon room-board-ctx-icon--broom")
                .attr("aria-hidden", "true")
                .appendTo($content);
        } else if (itemData.menuIcon === "plus-green") {
            $("<span/>")
                .addClass("room-board-ctx-icon room-board-ctx-icon--plus-green dx-icon dx-icon-plus")
                .attr("aria-hidden", "true")
                .appendTo($content);
        } else if (itemData.menuIcon === "open-arrow") {
            $("<span/>")
                .addClass("room-board-ctx-icon room-board-ctx-icon--open-arrow dx-icon dx-icon-arrowup")
                .attr("aria-hidden", "true")
                .appendTo($content);
        } else if (itemData.icon) {
            $("<span/>")
                .addClass(`dx-icon dx-icon-${itemData.icon}`)
                .attr("aria-hidden", "true")
                .appendTo($content);
        }

        $("<span/>").addClass("dx-menu-item-text").text(itemData.text || "").appendTo($content);
    }

    function buildContextMenuItems(room, t) {
        const policy = rbac();
        const canViewRes =
            !policy || typeof policy.canReservationView !== "function" || policy.canReservationView();
        const canCreateRes =
            !policy || typeof policy.canReservationCreate !== "function" || policy.canReservationCreate();
        const canUpdateStatus =
            !policy ||
            typeof policy.canRoomBoardUpdateStatus !== "function" ||
            policy.canRoomBoardUpdateStatus();

        const reservationIdToOpen =
            (room.currentStay && room.currentStay.reservationId) ||
            (room.nextStay && room.nextStay.reservationId);
        const hasReservationToOpen =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;

        const hk = normalizeStatusKey(room.housekeepingStatus);
        const hideCreateReservation = hk === "dirty" || isRoomUnderMaintenance(room);

        const items = [];
        if (hasReservationToOpen && canViewRes) {
            items.push({
                id: "open-reservation",
                text: t("roomBoard.action.openReservation"),
                menuIcon: "open-arrow"
            });
        } else if (!hideCreateReservation && canCreateRes) {
            items.push({ id: "create-reservation", text: t("roomBoard.action.createReservation"), icon: "plus" });
        }

        if (isOccupiedRoom(room) && canUpdateStatus) {
            items.push({
                id: "edit-card-color",
                text: t("roomBoard.action.editCardColors"),
                icon: "palette"
            });
        }

        const apartmentStatus = normalizeStatusKey(room.apartmentStatus);
        const operationalStatus = normalizeStatusKey(room.operationalStatus);
        const needsCleaningFinish = hk === "dirty" || hk === "cleaning";
        const waitingArrival = isWaitingForArrival(room);

        if (canUpdateStatus) {
            if (needsCleaningFinish && !waitingArrival) {
                const underCleaning = operationalStatus === "cleaning" || hk === "cleaning";
                items.push({
                    id: "clear-cleaning",
                    text: t("roomBoard.action.clearCleaning"),
                    menuIcon: underCleaning ? "plus-green" : undefined,
                    icon: underCleaning ? undefined : "undo"
                });
            } else if (!needsCleaningFinish) {
                items.push({
                    id: "mark-cleaning",
                    text: t("roomBoard.action.markCleaning"),
                    menuIcon: "broom"
                });
            }
        }

        if (apartmentStatus !== "rented" && canUpdateStatus) {
            items.push({
                id: "manage-maintenance",
                text: t("roomBoard.action.manageMaintenance"),
                icon: "preferences"
            });
        }

        if (!policy || typeof policy.has !== "function" || policy.has("resort_tickets.view")) {
            items.push({
                id: "resort-tickets",
                text: t("roomBoard.resortTickets.title"),
                icon: "print"
            });
        }

        items.push({ id: "room-features", text: t("roomBoard.action.roomFeatures"), icon: "favorites" });

        return items;
    }

    function openRoomCardColorPopup(room, t, onColorsChanged) {
        const $host = $("<div>").appendTo("body");

        function applyColorPreset(preset) {
            const p = preset || OCCUPIED_CARD_COLOR_DEFAULT;
            const draft = {
                occupiedGuestBackColor: p.back || "",
                occupiedTextColor: p.text || ""
            };
            return window.Zaaer.RoomBoardService.saveRoomCardColor(room.apartmentId, draft).then(() => {
                DevExpress.ui.notify(t("roomBoard.cardColors.saved"), "success", 1600);
                $host.dxPopup("instance").hide();
                if (typeof onColorsChanged === "function") {
                    onColorsChanged();
                }
            });
        }

        const paletteGroups = getOccupiedCardPaletteGroups();
        const initialPaletteGroup = OCCUPIED_CARD_PALETTE_DEFAULT_GROUP;

        $host.dxPopup({
            width: Math.min(420, Math.max(280, window.innerWidth - 24)),
            height: "auto",
            maxHeight: "50vh",
            shading: true,
            shadingColor: "rgba(15, 23, 42, 0.24)",
            title: `${room.apartmentName || room.apartmentCode || ""}`.trim() || "-",
            visible: true,
            showCloseButton: true,
            hideOnOutsideClick: true,
            wrapperAttr: { class: "room-card-color-popup res-extra-popup" },
            contentTemplate(contentElement) {
                const $content = $(contentElement).empty().addClass("room-card-color-popup-body");

                const $defaultRow = $("<div>")
                    .addClass("room-card-color-default-row")
                    .appendTo($content);
                $("<span>")
                    .addClass("room-card-color-default-label")
                    .text(t("roomBoard.cardColors.defaultHint"))
                    .appendTo($defaultRow);
                const $defaultBtn = $("<button type='button'>")
                    .addClass("room-card-color-default-btn")
                    .attr("aria-label", t("roomBoard.cardColors.default"))
                    .appendTo($defaultRow);
                $("<span>")
                    .addClass("room-card-color-dot room-card-color-dot--default")
                    .appendTo($defaultBtn);
                $("<span>")
                    .addClass("room-card-color-default-btn-text")
                    .text(t("roomBoard.cardColors.default"))
                    .appendTo($defaultBtn);
                $defaultBtn.on("click", () => {
                    applyColorPreset(OCCUPIED_CARD_COLOR_DEFAULT).catch(() =>
                        DevExpress.ui.notify(t("roomBoard.cardColors.saveFailed"), "error", 3000)
                    );
                });

                const $selectHost = $("<div>")
                    .addClass("room-card-color-family-select")
                    .appendTo($content);
                const $palette = $("<div>").addClass("room-card-color-palette").appendTo($content);

                function appendColorDot($parent, preset, variantKind) {
                    const back = `${preset.back || ""}`.trim();
                    const isDefault = !back;
                    if (isDefault) {
                        return;
                    }

                    const isSoft = variantKind === "soft";
                    const isCalm = variantKind === "calm";
                    const baseLabel = t(preset.textKey);
                    const label =
                        variantKind === "calm"
                            ? `${baseLabel} (${t("roomBoard.cardColors.variantCalm")})`
                            : variantKind === "soft"
                              ? `${baseLabel} (${t("roomBoard.cardColors.variantSoft")})`
                              : baseLabel;

                    const $dot = $("<button type='button'>")
                        .addClass("room-card-color-dot")
                        .toggleClass("room-card-color-dot--calm", isCalm)
                        .toggleClass("room-card-color-dot--soft", isSoft)
                        .attr("aria-label", label)
                        .css("background-color", back);

                    $dot.on("click", () => {
                        applyColorPreset(preset).catch(() =>
                            DevExpress.ui.notify(t("roomBoard.cardColors.saveFailed"), "error", 3000)
                        );
                    });

                    $parent.append($dot);
                }

                function renderPaletteSwatches(groupId) {
                    const group =
                        paletteGroups.find((g) => g.id === groupId) ||
                        paletteGroups.find((g) => g.id === OCCUPIED_CARD_PALETTE_DEFAULT_GROUP);
                    $palette.empty();

                    if (!group || !group.presets || !group.presets.length) {
                        return;
                    }

                    const $row = $("<div>").addClass("room-card-color-dots").appendTo($palette);
                    const groupKind = group.id === "soft" ? "soft" : "calm";

                    group.presets.forEach((preset) => {
                        appendColorDot($row, preset, groupKind);
                    });
                }

                $selectHost.dxSelectBox({
                    dataSource: paletteGroups,
                    valueExpr: "id",
                    displayExpr: (item) => (item && item.textKey ? t(item.textKey) : ""),
                    value: initialPaletteGroup,
                    label: t("roomBoard.cardColors.chooseFamily"),
                    labelMode: "outside",
                    stylingMode: "outlined",
                    searchEnabled: false,
                    onValueChanged(e) {
                        renderPaletteSwatches(e.value);
                    }
                });

                renderPaletteSwatches(initialPaletteGroup);
            },
            onHidden() {
                $host.remove();
            }
        });
    }

    function quickStateNotifyError(xhr, t) {
        const code =
            xhr && xhr.responseJSON && xhr.responseJSON.code ? String(xhr.responseJSON.code) : "";
        const key = code ? `roomBoard.quickState.${code}` : "roomBoard.quickState.error";
        const msg = t(key);
        DevExpress.ui.notify(msg !== key ? msg : t("roomBoard.quickState.error"), "error", 3200);
    }

    function applyQuickStateAndReload(room, mode, t, onBoardRefresh) {
        window.Zaaer.RoomBoardService.applyApartmentQuickState(room.apartmentId, { mode })
            .then(() => {
                DevExpress.ui.notify(t("roomBoard.quickState.saved"), "success", 1400);
                if (typeof onBoardRefresh === "function") {
                    onBoardRefresh();
                }
            })
            .catch((xhrOrErr) => quickStateNotifyError(xhrOrErr, t));
    }

    function notifyForbidden(t) {
        DevExpress.ui.notify(t("common.forbidden"), "warning", 3200);
    }

    function handleContextMenuAction(room, item, t, onBoardRefresh) {
        const policy = rbac();
        const action = item && item.id;
        if (action === "open-reservation") {
            if (policy && typeof policy.canReservationView === "function" && !policy.canReservationView()) {
                notifyForbidden(t);
                return;
            }
            const rid =
                (room.currentStay && room.currentStay.reservationId) ||
                (room.nextStay && room.nextStay.reservationId);
            if (rid) {
                window.location.href = openReservationUrl(room, rid);
                return;
            }
            DevExpress.ui.notify(t("roomBoard.openReservationMissing"), "warning", 2200);
            return;
        }
        if (action === "create-reservation") {
            if (policy && typeof policy.canReservationCreate === "function" && !policy.canReservationCreate()) {
                notifyForbidden(t);
                return;
            }
            if (normalizeStatusKey(room.housekeepingStatus) === "dirty") {
                DevExpress.ui.notify(t("roomBoard.action.createReservationBlockedDirty"), "warning", 2800);
                return;
            }
            if (room.apartmentId === undefined || room.apartmentId === null) {
                DevExpress.ui.notify(t("error.openNewReservation"), "warning", 2600);
                return;
            }
            window.location.href = newReservationUrl(room);
            return;
        }
        if (
            action === "edit-card-color" ||
            action === "mark-cleaning" ||
            action === "clear-cleaning" ||
            action === "manage-maintenance"
        ) {
            if (
                policy &&
                typeof policy.canRoomBoardUpdateStatus === "function" &&
                !policy.canRoomBoardUpdateStatus()
            ) {
                notifyForbidden(t);
                return;
            }
        }
        if (action === "edit-card-color") {
            openRoomCardColorPopup(room, t, onBoardRefresh);
            return;
        }
        if (action === "mark-cleaning") {
            applyQuickStateAndReload(room, "setCleaning", t, onBoardRefresh);
            return;
        }
        if (action === "clear-cleaning") {
            applyQuickStateAndReload(room, "clearCleaning", t, onBoardRefresh);
            return;
        }
        if (action === "manage-maintenance") {
            if (!window.Zaaer.RoomMaintenanceBoardPopup || typeof window.Zaaer.RoomMaintenanceBoardPopup.open !== "function") {
                DevExpress.ui.notify(t("roomBoard.maintenance.missingModule"), "error", 3000);
                return;
            }
            window.Zaaer.RoomMaintenanceBoardPopup.open(room, t, onBoardRefresh);
            return;
        }
        if (action === "resort-tickets") {
            if (!window.Zaaer.RoomResortTicketPopup || typeof window.Zaaer.RoomResortTicketPopup.open !== "function") {
                DevExpress.ui.notify(t("roomBoard.resortTickets.missingModule"), "error", 3000);
                return;
            }
            window.Zaaer.RoomResortTicketPopup.open(room, t, onBoardRefresh);
            return;
        }
        if (action === "room-features") {
            if (
                !window.Zaaer.RoomBoardRoomFeaturesPopup ||
                typeof window.Zaaer.RoomBoardRoomFeaturesPopup.open !== "function"
            ) {
                DevExpress.ui.notify(t("roomBoard.roomFeatures.missingModule"), "error", 3000);
                return;
            }
            window.Zaaer.RoomBoardRoomFeaturesPopup.open(room, t, onBoardRefresh);
            return;
        }
        const label = item && item.text ? item.text : action;
        DevExpress.ui.notify(`${label} — ${room.apartmentName || room.apartmentCode}`, "info", 1800);
    }

    function ensureContextMenuHost($container, t, onBoardRefresh) {
        let $menu = $container.data("roomBoardCtxMenuEl");
        if ($menu && $menu.length) {
            $(document).off(".roomBoardCtxDismiss");
            try {
                $menu.dxContextMenu("dispose");
            } catch {
                /* already disposed */
            }
            $menu.remove();
        }

        $menu = $("<div>").appendTo(document.body);
        $container.data("roomBoardCtxMenuEl", $menu);

        let ctxRoom = null;
        const dismissNs = ".roomBoardCtxDismiss";

        function detachDismissOnOutside() {
            $(document).off(dismissNs);
        }

        function attachDismissOnOutside() {
            detachDismissOnOutside();
            window.setTimeout(() => {
                $(document).on(`mousedown${dismissNs}`, function (ev) {
                    const inst = $menu.dxContextMenu("instance");
                    if (!inst) {
                        detachDismissOnOutside();
                        return;
                    }
                    if (!$(ev.target).closest(".dx-context-menu").length) {
                        try {
                            inst.hide();
                        } catch {
                            /* disposed */
                        }
                        detachDismissOnOutside();
                    }
                });
                $(document).on(`keydown${dismissNs}`, function (ev) {
                    if (ev.key !== "Escape") {
                        return;
                    }
                    const inst = $menu.dxContextMenu("instance");
                    if (!inst) {
                        detachDismissOnOutside();
                        return;
                    }
                    try {
                        inst.hide();
                    } catch {
                        /* disposed */
                    }
                    detachDismissOnOutside();
                });
            }, 0);
        }

        $menu.dxContextMenu({
            width: 248,
            cssClass: "room-board-context-menu",
            items: [],
            showEvent: "",
            hideOnOutsideClick: true,
            focusStateEnabled: false,
            itemTemplate: renderContextMenuItemTemplate,
            onShown() {
                attachDismissOnOutside();
            },
            onHidden() {
                detachDismissOnOutside();
            },
            onItemClick(e) {
                const item = e.itemData || {};
                const room = ctxRoom;
                if (!room || !item.id) {
                    return;
                }
                handleContextMenuAction(room, item, t, onBoardRefresh);
            }
        });

        const menuInst = $menu.dxContextMenu("instance");

        $container.off("contextmenu.roomBoardCtx");
        $container.on("contextmenu.roomBoardCtx", ".room-card", function (ev) {
            ev.preventDefault();
            try {
                menuInst.hide();
            } catch {
                /* not open */
            }
            detachDismissOnOutside();
            ctxRoom = $(this).data("roomBoardRoom");
            if (!ctxRoom) {
                return;
            }
            menuInst.option("items", buildContextMenuItems(ctxRoom, t));
            menuInst.option("position", {
                of: ev,
                my: "left top",
                at: "right top",
                collision: "flip fit"
            });
            menuInst.show();
        });
    }

    function appendRoomCard($grid, room, t) {
        const op = normalizeStatusKey(room.operationalStatus);
        const waitingArrival = isWaitingForArrivalRoom(room);
        const unpaidOnCard = roomHasUnpaidBalance(room);

        const $card = $("<article>")
            .addClass(`room-card ${room.statusCssClass || "status-available"}`)
            .toggleClass("is-overstay", !!room.isOverstay)
            .toggleClass("is-departure-today", !!room.isDepartureToday)
            .toggleClass("is-waiting-arrival", waitingArrival)
            .toggleClass("has-unpaid-balance", unpaidOnCard && !waitingArrival)
            .appendTo($grid);

        $card.data("roomBoardRoom", room);

        $("<div>")
            .addClass("room-card-status-indicator")
            .appendTo($card);

        const $header = $("<div>").addClass("room-card-header").appendTo($card);

        const reservationIdToOpen =
            (room.currentStay && room.currentStay.reservationId) ||
            (room.nextStay && room.nextStay.reservationId);

        let hasHeaderCornerBadge = false;

        if (room.isOverstay) {
            appendOverstayCornerBadge($header, room, reservationIdToOpen, t);
            hasHeaderCornerBadge = true;
        } else if (room.isDepartureToday) {
            appendDepartureCornerBadge($header, room, reservationIdToOpen, t);
            hasHeaderCornerBadge = true;
        } else if (waitingArrival) {
            appendCornerReservationBadge(
                $header,
                room,
                "reserved",
                reservationIdToOpen,
                t
            );
            hasHeaderCornerBadge = true;
        } else if (unpaidOnCard) {
            appendCornerReservationBadge(
                $header,
                room,
                "unpaid",
                reservationIdToOpen,
                t
            );
            hasHeaderCornerBadge = true;
        }

        if (hasHeaderCornerBadge) {
            $card.addClass("has-header-corner-badge");
        }

        const hasReservationToOpenEarly =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;
        if (hasReservationToOpenEarly && !hasHeaderCornerBadge) {
            $card.addClass("has-open-reservation-arrow");
        }

        const roomTitle = room.apartmentName || room.apartmentCode || "-";
        const $headerMain = $("<div>").addClass("room-card-header-main").appendTo($header);

        const $roomCode = $("<div>")
            .addClass("room-card-code")
            .attr("title", roomTitle)
            .text(roomTitle)
            .appendTo($headerMain);

        const hasReservationToOpen =
            reservationIdToOpen !== undefined &&
            reservationIdToOpen !== null &&
            Number(reservationIdToOpen) > 0;
        const hkNorm = normalizeStatusKey(room.housekeepingStatus);
        const isVacant = op === "available";
        const blockNewReservationWhileDirty = hkNorm === "dirty";
        const showQuickPlus = isVacant && !blockNewReservationWhileDirty;
        const hasOccupiedColors = isOccupiedRoom(room) && roomHasCustomColors(room);
        const expanded = isRoomExpanded(room);
        applyCardExpandedState($card, room, expanded);

        if (hasOccupiedColors) {
            $card.addClass("has-room-card-colors");
        }

        const headerContext = { hasReservationToOpen, reservationIdToOpen };
        appendHeaderLead($header, room, headerContext, t);

        if (shouldShowBroomInHeader(room)) {
            appendBroomSymbol(ensureHeaderActions($header));
        }

        appendHeaderOperationalSymbol($header, room, t);

        if (showQuickPlus) {
            appendHeaderCreateReservationButton(
                $header,
                room,
                hasReservationToOpen,
                reservationIdToOpen,
                t
            );
        }

        if (canToggleCardExpanded(room)) {
            wireRoomCodeToggle($roomCode, $card, room, t);
        } else {
            $card.addClass("room-card--compact-only");
        }

        const needsFooterQuickRow = shouldShowOccupiedDirtyFooter(room);
        if (needsFooterQuickRow) {
            $card.addClass("has-quick-footer");
        }

        const roomTypeLabel = (() => {
            const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;
            const raw = room.roomTypeName || t("roomBoard.roomTypeNotSet");
            if (labeler && typeof labeler.display === "function" && room.roomTypeName) {
                return labeler.display(room.roomTypeName, t);
            }
            return raw;
        })();

        $("<div>")
            .addClass("room-card-type")
            .attr("title", roomTypeLabel)
            .text(roomTypeLabel)
            .appendTo($card);

        const $colorZone = $("<div>").addClass("room-card-color-zone").appendTo($card);

        const balanceAmount = Number(room.balanceAmount || 0);
        const balanceText = formatBalance(balanceAmount);
        if (balanceText && !headerShowsBalance(room, headerContext)) {
            $("<div>")
                .addClass("room-card-balance-row")
                .append(
                    $("<span>")
                        .addClass(`room-card-balance ${balanceAmount > 0 ? "is-positive" : "is-negative"}`)
                        .text(balanceText)
                )
                .appendTo($colorZone);
        }

        const $compact = $("<div>").addClass("room-card-compact").appendTo($colorZone);

        const $stayMeta = $("<div>").addClass("room-card-stay-meta").appendTo($compact);

        $("<div>")
            .addClass("room-card-guest")
            .text(room.customerName || "")
            .appendTo($stayMeta);
        const checkInLabel = shortDateText(room.checkInDateShort);
        const checkOutLabel = shortDateText(room.checkOutDateShort);
        const $dates = $("<div>").addClass("room-card-dates");
        appendRoomCardDateChip($dates, "in", checkInLabel);
        appendRoomCardDateChip($dates, "out", checkOutLabel);
        if ($dates.children().length) {
            $dates.appendTo($stayMeta);
        }

        if (isRoomUnderMaintenance(room)) {
            const maint = window.Zaaer && window.Zaaer.RoomMaintenanceBoardPopup;
            const label =
                maint && typeof maint.formatMaintenanceDisplayLabel === "function"
                    ? maint.formatMaintenanceDisplayLabel(room, t)
                    : "";
            if (label) {
                $card.addClass("has-maintenance-summary");
                $("<div>")
                    .addClass("room-card-maintenance-summary")
                    .text(label)
                    .appendTo($colorZone);
            }
        }

        syncOccupiedCardColorForExpand($card, $colorZone, room, expanded);

        if (needsFooterQuickRow) {
            const $quick = $("<div>")
                .addClass("room-card-quick-row")
                .appendTo($card);
            appendFooterOnlySymbol($quick, room);
        }
    }

    function render(containerSelector, rooms, t, options) {
        const opts = options && typeof options === "object" ? options : {};
        lastCardsRenderCtx = { containerSelector, rooms, t, options: opts };
        const $container = $(containerSelector);
        $container.empty();

        if (!rooms || rooms.length === 0) {
            $(".room-board-content-toolbar .room-board-actions").empty();
            $("<div>")
                .addClass("room-board-empty")
                .text(t("roomBoard.noRooms"))
                .appendTo($container);
            return;
        }

        renderRoomBoardActions(t, $container);

        if (cardsGroupByFloor) {
            const $layout = $("<div>").addClass("room-card-floor-layout").appendTo($container);
            groupRoomsForCardFloors(rooms).forEach((building) => {
                if (building.buildingKey !== "__default__") {
                    $("<div>")
                        .addClass("room-card-building-title")
                        .text(building.buildingKey)
                        .appendTo($layout);
                }

                building.floors.forEach((floor) => {
                    const $section = $("<section>")
                        .addClass("room-card-floor-section")
                        .appendTo($layout);
                    appendCardFloorHeader($section, floor, t);
                    const $grid = $("<div>").addClass("room-card-grid").appendTo($section);
                    floor.rooms.forEach((room) => appendRoomCard($grid, room, t));
                });
            });
        } else {
            const $grid = $("<div>").addClass("room-card-grid").appendTo($container);
            rooms.forEach((room) => appendRoomCard($grid, room, t));
        }

        const onBoardRefresh =
            typeof opts.onBoardRefresh === "function"
                ? opts.onBoardRefresh
                : typeof opts.onColorsChanged === "function"
                  ? opts.onColorsChanged
                  : null;

        ensureContextMenuHost($container, t, onBoardRefresh);
    }

    function appendRoomBoardGridAlertIcon($parent, kind, title) {
        if (!$parent || !$parent.length) {
            return;
        }

        const $wrap = $("<span>")
            .addClass(`room-board-grid-alert room-board-grid-alert--${kind}`)
            .attr("title", title || "")
            .attr("aria-hidden", title ? "false" : "true")
            .appendTo($parent);

        if (kind === "broom") {
            appendBroomSymbol($wrap);
            return;
        }

        if (kind === "overstay") {
            appendOverstaySymbol($wrap);
            return;
        }

        const iconByKind = {
            departure: "runner"
        };
        const icon = iconByKind[kind];
        if (icon) {
            $("<i>").addClass(`room-board-grid-icon dx-icon dx-icon-${icon}`).appendTo($wrap);
        }
    }

    function appendRoomBoardGridStatusChip($parent, room, t) {
        if (!$parent || !$parent.length || !room) {
            return;
        }

        const statusClass = room.statusCssClass || "status-available";
        const statusKey = normalizeStatusKey(room.operationalStatus) || statusClass.replace(/^status-/, "");
        const labelKey = `status.${room.operationalStatus || statusKey}`;

        const $chip = $("<span>")
            .addClass(`room-board-grid-status-chip ${statusClass}`)
            .attr("title", t(labelKey))
            .attr("aria-label", t(labelKey))
            .appendTo($parent);

        const $iconHost = $("<span>").addClass("room-board-grid-status-chip-icon").appendTo($chip);
        appendRoomOperationalStatusIcon($iconHost, statusKey);
    }

    function appendRoomBoardGridIndicators($parent, room, t) {
        if (!$parent || !$parent.length || !room) {
            return;
        }

        appendRoomBoardGridStatusChip($parent, room, t);

        if (room.isOverstay) {
            appendRoomBoardGridAlertIcon($parent, "overstay", formatOverstayBanner(room, t));
        } else if (room.isDepartureToday) {
            appendRoomBoardGridAlertIcon($parent, "departure", t("summary.departureToday"));
        }

        const op = normalizeStatusKey(room.operationalStatus);
        if (op !== "cleaning") {
            if (shouldShowBroomInHeader(room) || shouldShowBroomBesideOccupiedBalance(room)) {
                const hk = normalizeStatusKey(room.housekeepingStatus);
                const hkLabel =
                    hk === "cleaning" ? t("housekeeping.cleaning") : t("housekeeping.dirty");
                appendRoomBoardGridAlertIcon($parent, "broom", hkLabel);
            }
        }
    }

    function gridActionFixedPosition() {
        const rtl =
            (window.Zaaer &&
                window.Zaaer.LocalizationService &&
                typeof window.Zaaer.LocalizationService.currentCulture === "function" &&
                window.Zaaer.LocalizationService.currentCulture() === "ar") ||
            document.documentElement.getAttribute("dir") === "rtl";
        return rtl ? "left" : "right";
    }

    function reservationIdForRoom(room) {
        const rid =
            (room.currentStay && room.currentStay.reservationId) ||
            (room.nextStay && room.nextStay.reservationId);
        return rid !== undefined && rid !== null && Number(rid) > 0 ? Number(rid) : null;
    }

    function buildGridActionColumn(t, onBoardRefresh) {
        const policy = rbac();

        function runAction(room, itemId) {
            if (!room) {
                return;
            }

            handleContextMenuAction(room, { id: itemId }, t, onBoardRefresh);
        }

        return {
            type: "buttons",
            name: "roomBoardGridActions",
            caption: t("roomBoard.gridActions"),
            width: 138,
            minWidth: 138,
            fixed: true,
            fixedPosition: gridActionFixedPosition(),
            alignment: "center",
            cssClass: "room-board-grid-actions-col",
            allowSorting: false,
            allowFiltering: false,
            allowHeaderFiltering: false,
            buttons: [
                {
                    hint: t("roomBoard.action.openReservation"),
                    icon: "folder",
                    visible(e) {
                        const room = e.row && e.row.data;
                        if (!room || !reservationIdForRoom(room)) {
                            return false;
                        }

                        return (
                            !policy ||
                            typeof policy.canReservationView !== "function" ||
                            policy.canReservationView()
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "open-reservation");
                    }
                },
                {
                    hint: t("roomBoard.action.createReservation"),
                    icon: "plus",
                    visible(e) {
                        const room = e.row && e.row.data;
                        if (!room || reservationIdForRoom(room)) {
                            return false;
                        }

                        if (normalizeStatusKey(room.housekeepingStatus) === "dirty") {
                            return false;
                        }

                        return (
                            !policy ||
                            typeof policy.canReservationCreate !== "function" ||
                            policy.canReservationCreate()
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "create-reservation");
                    }
                },
                {
                    hint: t("roomBoard.action.clearCleaning"),
                    icon: "undo",
                    visible(e) {
                        const room = e.row && e.row.data;
                        if (!room) {
                            return false;
                        }

                        const hk = normalizeStatusKey(room.housekeepingStatus);
                        const needsCleaningFinish = hk === "dirty" || hk === "cleaning";
                        if (!needsCleaningFinish || isWaitingForArrival(room)) {
                            return false;
                        }

                        return (
                            !policy ||
                            typeof policy.canRoomBoardUpdateStatus !== "function" ||
                            policy.canRoomBoardUpdateStatus()
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "clear-cleaning");
                    }
                },
                {
                    hint: t("roomBoard.action.markCleaning"),
                    icon: "clearformat",
                    visible(e) {
                        const room = e.row && e.row.data;
                        if (!room) {
                            return false;
                        }

                        const hk = normalizeStatusKey(room.housekeepingStatus);
                        const needsCleaningFinish = hk === "dirty" || hk === "cleaning";
                        if (needsCleaningFinish) {
                            return false;
                        }

                        return (
                            !policy ||
                            typeof policy.canRoomBoardUpdateStatus !== "function" ||
                            policy.canRoomBoardUpdateStatus()
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "mark-cleaning");
                    }
                },
                {
                    hint: t("roomBoard.action.manageMaintenance"),
                    icon: "preferences",
                    visible(e) {
                        const room = e.row && e.row.data;
                        if (!room) {
                            return false;
                        }

                        if (normalizeStatusKey(room.apartmentStatus) === "rented") {
                            return false;
                        }

                        return (
                            !policy ||
                            typeof policy.canRoomBoardUpdateStatus !== "function" ||
                            policy.canRoomBoardUpdateStatus()
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "manage-maintenance");
                    }
                },
                {
                    hint: t("roomBoard.action.roomFeatures"),
                    icon: "favorites",
                    onClick(e) {
                        runAction(e.row && e.row.data, "room-features");
                    }
                },
                {
                    hint: t("roomBoard.action.editCardColors"),
                    icon: "palette",
                    visible(e) {
                        const room = e.row && e.row.data;
                        return (
                            !!room &&
                            isOccupiedRoom(room) &&
                            (!policy ||
                                typeof policy.canRoomBoardUpdateStatus !== "function" ||
                                policy.canRoomBoardUpdateStatus())
                        );
                    },
                    onClick(e) {
                        runAction(e.row && e.row.data, "edit-card-color");
                    }
                }
            ]
        };
    }

    function attachDataGridRoomActions(gridInstance, t, onBoardRefresh) {
        if (!gridInstance) {
            return;
        }

        gridInstance.option("onContextMenuPreparing", (e) => {
            if (e.target !== "content" || !e.row || e.row.rowType !== "data") {
                return;
            }

            const room = e.row.data;
            const items = buildContextMenuItems(room, t);
            if (!items.length) {
                e.cancel = true;
                return;
            }

            e.items = items.map((item) => ({
                text: item.text,
                icon: item.icon,
                onItemClick: () => handleContextMenuAction(room, item, t, onBoardRefresh)
            }));
        });

        gridInstance.option("onRowDblClick", (e) => {
            const room = e.data;
            if (!room) {
                return;
            }

            const rid = reservationIdForRoom(room);
            if (rid) {
                const policy = rbac();
                if (policy && typeof policy.canReservationView === "function" && !policy.canReservationView()) {
                    notifyForbidden(t);
                    return;
                }

                window.location.href = openReservationUrl(room, rid);
                return;
            }

            if (
                normalizeStatusKey(room.operationalStatus) !== "available" ||
                isRoomUnderMaintenance(room)
            ) {
                return;
            }

            handleContextMenuAction(room, { id: "create-reservation" }, t, onBoardRefresh);
        });
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomCardView = {
        render,
        buildGridActionColumn,
        attachDataGridRoomActions,
        handleContextMenuAction,
        buildContextMenuItems,
        renderContextMenuItemTemplate,
        appendRoomBoardGridIndicators,
        renderRoomStatusPill,
        appendRoomOperationalStatusIcon,
        appendOverstaySymbol,
        openReservationUrl,
        newReservationUrl,
        reservationIdForRoom,
        formatOverstayBanner,
        normalizeStatusKey
    };
})(window, jQuery, DevExpress);
