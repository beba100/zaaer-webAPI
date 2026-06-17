(function (window, $, DevExpress) {
    "use strict";

    const RACK_COLLAPSE_STORAGE_KEY = "zaaer.roomBoard.rackCollapse.v1";
    const collapsedSections = new Set();

    function cardView() {
        return window.Zaaer && window.Zaaer.RoomCardView;
    }

    function normalizeStatusKey(value) {
        const cv = cardView();
        if (cv && typeof cv.normalizeStatusKey === "function") {
            return cv.normalizeStatusKey(value);
        }

        return `${value ?? ""}`.trim().toLowerCase().replace(/_/g, "-");
    }

    function reservationIdForRoom(room) {
        const cv = cardView();
        if (cv && typeof cv.reservationIdForRoom === "function") {
            return cv.reservationIdForRoom(room);
        }

        const rid =
            (room.currentStay && room.currentStay.reservationId) ||
            (room.nextStay && room.nextStay.reservationId);
        return rid !== undefined && rid !== null && Number(rid) > 0 ? Number(rid) : null;
    }

    function openReservationUrl(room, reservationId) {
        const cv = cardView();
        if (cv && typeof cv.openReservationUrl === "function") {
            return cv.openReservationUrl(room, reservationId);
        }

        return `/reservation-detail.html?id=${encodeURIComponent(reservationId)}`;
    }

    function newReservationUrl(room) {
        const cv = cardView();
        if (cv && typeof cv.newReservationUrl === "function") {
            return cv.newReservationUrl(room);
        }

        return "/reservation-detail.html?newReservation=1";
    }

    function formatOverstayChip(room, t) {
        const days = Math.max(1, Number(room && room.overstayDays) || 1);
        if (days === 1) {
            return t("roomBoard.rack.chipOverstayOne");
        }

        return t("roomBoard.rack.chipOverstay").replace("{0}", String(days));
    }

    function isVacantGuestLabel(room, guestText, t) {
        const vacantLabel = t("roomBoard.rack.vacant");
        if (`${guestText || ""}`.trim() === vacantLabel) {
            return true;
        }

        const op = normalizeStatusKey(room && room.operationalStatus);
        return op === "available" && !reservationIdForRoom(room);
    }

    function appendFloorStatPill($stats, kind, count, label) {
        return $("<span>")
            .addClass(`room-rack-floor-stat room-rack-floor-stat--${kind}`)
            .append($("<span>").addClass("room-rack-floor-stat-count").text(String(count || 0)))
            .append($("<span>").addClass("room-rack-floor-stat-label").text(label));
    }

    function persistRackCollapseState() {
        try {
            localStorage.setItem(
                RACK_COLLAPSE_STORAGE_KEY,
                JSON.stringify({ collapsed: Array.from(collapsedSections) })
            );
        } catch {
            /* storage unavailable */
        }
    }

    function restoreRackCollapseState() {
        try {
            const raw = localStorage.getItem(RACK_COLLAPSE_STORAGE_KEY);
            if (!raw) {
                return;
            }

            const saved = JSON.parse(raw);
            collapsedSections.clear();
            if (Array.isArray(saved.collapsed)) {
                saved.collapsed.forEach((key) => {
                    const k = `${key ?? ""}`.trim();
                    if (k) {
                        collapsedSections.add(k);
                    }
                });
            }
        } catch {
            /* ignore corrupt storage */
        }
    }

    restoreRackCollapseState();

    function roomSortKey(room) {
        const code = `${room.apartmentCode || room.apartmentName || ""}`.trim();
        const num = parseInt(code.replace(/\D/g, ""), 10);
        return Number.isFinite(num) ? num : code;
    }

    function deriveFloorKey(room) {
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

    function groupRoomsForRack(rooms, t) {
        const buildingMap = new Map();

        (rooms || []).forEach((room) => {
            const buildingKey = `${room.buildingName || ""}`.trim() || "__default__";
            if (!buildingMap.has(buildingKey)) {
                buildingMap.set(buildingKey, new Map());
            }

            const floorKey = deriveFloorKey(room);
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
                    const ka = roomSortKey(a);
                    const kb = roomSortKey(b);
                    if (typeof ka === "number" && typeof kb === "number") {
                        return ka - kb;
                    }

                    return `${ka}`.localeCompare(`${kb}`, undefined, { numeric: true });
                });

                const occupied = floorRooms.filter((r) => normalizeStatusKey(r.operationalStatus) === "occupied").length;
                const overstay = floorRooms.filter((r) => !!r.isOverstay).length;

                floorList.push({
                    key: `${buildingKey}::${floorKey}`,
                    floorKey,
                    buildingKey,
                    rooms: floorRooms,
                    stats: {
                        total: floorRooms.length,
                        occupied,
                        overstay
                    }
                });
            });

            floorList.sort((a, b) => `${a.floorKey}`.localeCompare(`${b.floorKey}`, undefined, { numeric: true }));

            buildings.push({
                buildingKey,
                buildingLabel: buildingKey === "__default__" ? "" : buildingKey,
                floors: floorList
            });
        });

        return buildings;
    }

    function railStatusClass(room) {
        if (room.isOverstay) {
            return "rack-rail--overstay";
        }

        if (room.isDepartureToday) {
            return "rack-rail--departure";
        }

        const op = normalizeStatusKey(room.operationalStatus);
        if (op === "occupied") {
            return "rack-rail--occupied";
        }

        if (op === "reserved") {
            return "rack-rail--reserved";
        }

        if (op === "cleaning") {
            return "rack-rail--cleaning";
        }

        if (op === "maintenance") {
            return "rack-rail--maintenance";
        }

        return "rack-rail--available";
    }

    function roomTypeLabel(room, t) {
        const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;
        const raw = room.roomTypeName || t("roomBoard.roomTypeNotSet");
        if (labeler && typeof labeler.display === "function" && room.roomTypeName) {
            return labeler.display(room.roomTypeName, t);
        }

        return raw;
    }

    function formatDates(room) {
        const ci = `${room.checkInDateShort || ""}`.trim();
        const co = `${room.checkOutDateShort || ""}`.trim();
        if (!ci && !co) {
            return "";
        }

        if (ci && co) {
            return `${ci} → ${co}`;
        }

        return ci || co;
    }

    function appendChip($host, kind, text, title) {
        return $("<span>")
            .addClass(`room-rack-chip room-rack-chip--${kind}`)
            .attr("title", title || text)
            .text(text)
            .appendTo($host);
    }

    function appendRowChips($chips, room, t) {
        if (room.isOverstay) {
            appendChip($chips, "overstay", formatOverstayChip(room, t), t("summary.overstay"));
        } else if (room.isDepartureToday) {
            appendChip($chips, "departure", t("roomBoard.rack.chipDeparture"), t("summary.departureToday"));
        }

        if (room.hasUnpaidBalance) {
            const bal = Number(room.balanceAmount || 0);
            const label = bal > 0 ? t("roomBoard.rack.chipUnpaid") : t("roomBoard.rack.chipUnpaid");
            appendChip($chips, "unpaid", label, t("summary.unpaidBalance"));
        }

        const hk = normalizeStatusKey(room.housekeepingStatus);
        const op = normalizeStatusKey(room.operationalStatus);
        const apt = normalizeStatusKey(room.apartmentStatus);
        const isRentedLike = op === "occupied" || apt === "rented";
        if (isRentedLike && (hk === "dirty" || hk === "cleaning")) {
            appendChip(
                $chips,
                "hk",
                t("roomBoard.rack.chipHk"),
                hk === "cleaning" ? t("housekeeping.cleaning") : t("housekeeping.dirty")
            );
        }
    }

    function appendRowActions($actions, room, t) {
        const rid = reservationIdForRoom(room);
        const op = normalizeStatusKey(room.operationalStatus);

        if (rid) {
            $("<button>")
                .attr("type", "button")
                .addClass("room-rack-action room-rack-action--open")
                .attr("title", t("roomBoard.action.openReservation"))
                .append($("<i>").addClass("dx-icon dx-icon-chevronnext"))
                .on("click", (ev) => {
                    ev.preventDefault();
                    ev.stopPropagation();
                    window.location.href = openReservationUrl(room, rid);
                })
                .appendTo($actions);
            return;
        }

        if (op === "available" && normalizeStatusKey(room.operationalStatus) !== "maintenance") {
            const hk = normalizeStatusKey(room.housekeepingStatus);
            if (hk !== "dirty") {
                $("<button>")
                    .attr("type", "button")
                    .addClass("room-rack-action room-rack-action--create")
                    .attr("title", t("roomBoard.action.createReservation"))
                    .append($("<i>").addClass("dx-icon dx-icon-plus"))
                    .on("click", (ev) => {
                        ev.preventDefault();
                        ev.stopPropagation();
                        window.location.href = newReservationUrl(room);
                    })
                    .appendTo($actions);
            }
        }
    }

    function appendRackRow($body, room, t) {
        const rid = reservationIdForRoom(room);
        const guest = `${room.customerName || ""}`.trim() || t("roomBoard.rack.vacant");
        const dates = formatDates(room);

        const $row = $("<div>")
            .addClass("room-rack-row")
            .toggleClass("room-rack-row--overstay", !!room.isOverstay)
            .toggleClass("room-rack-row--departure", !!room.isDepartureToday)
            .appendTo($body);

        $row.data("roomBoardRoom", room);

        $("<span>").addClass(`room-rack-rail ${railStatusClass(room)}`).appendTo($row);

        $("<span>")
            .addClass("room-rack-code")
            .text(room.apartmentCode || room.apartmentName || "—")
            .appendTo($row);

        $("<span>")
            .addClass("room-rack-type")
            .attr("title", roomTypeLabel(room, t))
            .text(roomTypeLabel(room, t))
            .appendTo($row);

        const $guestCell = $("<span>").addClass("room-rack-guest-cell").appendTo($row);
        if (isVacantGuestLabel(room, guest, t)) {
            $("<span>")
                .addClass("room-rack-guest-pill room-rack-guest-pill--vacant")
                .attr("title", guest)
                .text(guest)
                .appendTo($guestCell);
        } else {
            $("<span>")
                .addClass("room-rack-guest")
                .attr("title", guest)
                .text(guest)
                .appendTo($guestCell);
        }

        $("<span>")
            .addClass("room-rack-dates")
            .attr("dir", "ltr")
            .text(dates || "—")
            .appendTo($row);

        const $chips = $("<span>").addClass("room-rack-chips").appendTo($row);
        appendRowChips($chips, room, t);

        const $actions = $("<span>").addClass("room-rack-actions").appendTo($row);
        appendRowActions($actions, room, t);

        function activateRow() {
            if (rid) {
                window.location.href = openReservationUrl(room, rid);
                return;
            }

            if (normalizeStatusKey(room.operationalStatus) === "available") {
                window.location.href = newReservationUrl(room);
            }
        }

        $row.attr("tabindex", "0").on("click", function (ev) {
            if ($(ev.target).closest(".room-rack-action").length) {
                return;
            }

            activateRow();
        });

        $row.on("keydown", function (ev) {
            if (ev.key === "Enter" || ev.key === " ") {
                ev.preventDefault();
                activateRow();
            }
        });
    }

    function appendFloorSection($host, floor, t) {
        const isCollapsed = collapsedSections.has(floor.key);
        const $section = $("<section>")
            .addClass("room-rack-floor")
            .toggleClass("is-collapsed", isCollapsed)
            .appendTo($host);

        const $header = $("<button>")
            .attr("type", "button")
            .addClass("room-rack-floor-header")
            .appendTo($section);

        $("<span>")
            .addClass("room-rack-floor-chevron dx-icon")
            .toggleClass("dx-icon-chevronup", !isCollapsed)
            .toggleClass("dx-icon-chevrondown", isCollapsed)
            .appendTo($header);

        $("<span>")
            .addClass("room-rack-floor-name")
            .text(`${t("roomBoard.rack.floor")} ${floor.floorKey}`)
            .appendTo($header);

        const $stats = $("<span>").addClass("room-rack-floor-stats").appendTo($header);
        appendFloorStatPill($stats, "total", floor.stats.total, t("roomBoard.rack.statRooms"));
        appendFloorStatPill($stats, "occupied", floor.stats.occupied, t("roomBoard.rack.statOccupied"));
        appendFloorStatPill($stats, "overstay", floor.stats.overstay, t("roomBoard.rack.statOverstay"));

        const $body = $("<div>").addClass("room-rack-floor-body").appendTo($section);
        if (isCollapsed) {
            $body.hide();
        }

        floor.rooms.forEach((room) => appendRackRow($body, room, t));

        $header.on("click", () => {
            const next = !collapsedSections.has(floor.key);
            if (next) {
                collapsedSections.add(floor.key);
                $section.addClass("is-collapsed");
                $body.slideUp(140);
                $header.find(".room-rack-floor-chevron")
                    .removeClass("dx-icon-chevronup")
                    .addClass("dx-icon-chevrondown");
            } else {
                collapsedSections.delete(floor.key);
                $section.removeClass("is-collapsed");
                $body.slideDown(140);
                $header.find(".room-rack-floor-chevron")
                    .removeClass("dx-icon-chevrondown")
                    .addClass("dx-icon-chevronup");
            }

            persistRackCollapseState();
        });
    }

    function appendExceptionBar($host, overstayCount, t, onOverstayFilter) {
        if (!overstayCount || overstayCount <= 0) {
            return;
        }

        const label = t("roomBoard.rack.exceptionBar").replace("{0}", String(overstayCount));
        const $bar = $("<div>").addClass("room-rack-exception-bar").appendTo($host);

        const cv = cardView();
        if (cv && typeof cv.appendOverstaySymbol === "function") {
            cv.appendOverstaySymbol($bar);
        }

        $("<span>").addClass("room-rack-exception-text").text(label).appendTo($bar);

        if (typeof onOverstayFilter === "function") {
            $("<button>")
                .attr("type", "button")
                .addClass("room-rack-exception-btn")
                .text(t("roomBoard.rack.exceptionShowAll"))
                .on("click", (ev) => {
                    ev.preventDefault();
                    onOverstayFilter();
                })
                .appendTo($bar);
        }
    }

    function ensureRackContextMenu($container, $rackHost, t, onBoardRefresh) {
        let $menu = $container.data("roomBoardRackCtxMenuEl");
        if ($menu && $menu.length) {
            $(document).off(".roomBoardRackCtxDismiss");
            try {
                $menu.dxContextMenu("dispose");
            } catch {
                /* already disposed */
            }
            $menu.remove();
        }

        $menu = $("<div>").appendTo(document.body);
        $container.data("roomBoardRackCtxMenuEl", $menu);

        const cv = cardView();
        let ctxRoom = null;
        const dismissNs = ".roomBoardRackCtxDismiss";

        function detachDismiss() {
            $(document).off(dismissNs);
        }

        $menu.dxContextMenu({
            width: 248,
            cssClass: "room-board-context-menu",
            items: [],
            showEvent: "",
            hideOnOutsideClick: true,
            focusStateEnabled: false,
            itemTemplate: cv && cv.renderContextMenuItemTemplate ? cv.renderContextMenuItemTemplate : null,
            onItemClick(e) {
                const item = e.itemData || {};
                if (!ctxRoom || !item.id || !cv || typeof cv.handleContextMenuAction !== "function") {
                    return;
                }

                cv.handleContextMenuAction(ctxRoom, item, t, onBoardRefresh);
            }
        });

        const menuInst = $menu.dxContextMenu("instance");

        $rackHost.off("contextmenu.roomBoardRack");
        $rackHost.on("contextmenu.roomBoardRack", ".room-rack-row", function (ev) {
            ev.preventDefault();
            ctxRoom = $(this).data("roomBoardRoom");
            if (!ctxRoom || !cv || typeof cv.buildContextMenuItems !== "function") {
                return;
            }

            menuInst.option("items", cv.buildContextMenuItems(ctxRoom, t));
            menuInst.option("position", {
                my: "left top",
                at: "left bottom",
                of: ev,
                offset: "4 4"
            });
            menuInst.show();
        });
    }

    function render(selector, rooms, t, opts) {
        const options = opts || {};
        const $container = $(selector);
        if (!$container.length) {
            return;
        }

        $container.empty().addClass("room-rack-root");

        const list = Array.isArray(rooms) ? rooms : [];
        if (!list.length) {
            $("<p>")
                .addClass("room-rack-empty")
                .text(t("roomBoard.noRooms"))
                .appendTo($container);
            return;
        }

        const overstayCount = list.filter((r) => !!r.isOverstay).length;
        appendExceptionBar($container, overstayCount, t, options.onOverstayFilter);

        const grouped = groupRoomsForRack(list, t);
        const $rack = $("<div>").addClass("room-rack-board").appendTo($container);

        grouped.forEach((building) => {
            if (building.buildingLabel) {
                $("<div>")
                    .addClass("room-rack-building-title")
                    .text(building.buildingLabel)
                    .appendTo($rack);
            }

            building.floors.forEach((floor) => appendFloorSection($rack, floor, t));
        });

        ensureRackContextMenu($container, $rack, t, options.onBoardRefresh);
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomBoardRackView = {
        render,
        groupRoomsForRack
    };
})(window, jQuery, DevExpress);
