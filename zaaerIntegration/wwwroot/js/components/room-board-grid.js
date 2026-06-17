(function (window, $) {
    "use strict";

    let gridInstance = null;
    let onBoardRefreshCb = null;
    let roomPostFilterCb = null;
    let $gridHost = null;

    const GRID_ACTION_LEGEND = [
        { icon: "arrowup", labelKey: "roomBoard.action.openReservation" },
        { icon: "plus", labelKey: "roomBoard.action.createReservation" },
        { icon: "undo", labelKey: "roomBoard.action.clearCleaning" },
        { icon: "clearformat", labelKey: "roomBoard.action.markCleaning" },
        { icon: "preferences", labelKey: "roomBoard.action.manageMaintenance" },
        { icon: "favorites", labelKey: "roomBoard.action.roomFeatures" },
        { icon: "palette", labelKey: "roomBoard.action.editCardColors" }
    ];

    function isRtl() {
        const svc = window.Zaaer && window.Zaaer.LocalizationService;
        if (svc && typeof svc.currentCulture === "function") {
            return svc.currentCulture() === "ar";
        }

        return document.documentElement.getAttribute("dir") === "rtl";
    }

    function ensureGridShell(containerSelector) {
        const $panel = $(containerSelector);
        if (!$panel.length) {
            return $();
        }

        $panel.addClass("room-board-panel--grid");
        let $shell = $panel.children(".room-board-grid-shell");
        if (!$shell.length) {
            $shell = $("<div>").addClass("room-board-grid-shell").appendTo($panel);
            $("<div>").addClass("room-board-grid-legend").appendTo($shell);
            $("<div>").addClass("room-board-grid-host").appendTo($shell);
        }

        return $shell.find(".room-board-grid-host");
    }

    function renderGridLegend(t) {
        if (!$gridHost || !$gridHost.length) {
            return;
        }

        const $legend = $gridHost.closest(".room-board-grid-shell").find(".room-board-grid-legend");
        $legend.empty();

        $("<span>").addClass("room-board-grid-legend-title").text(t("roomBoard.gridLegend")).appendTo($legend);

        const $items = $("<div>").addClass("room-board-grid-legend-items").appendTo($legend);
        GRID_ACTION_LEGEND.forEach((item) => {
            $("<span>")
                .addClass("room-board-grid-legend-item")
                .append(
                    $("<span>")
                        .addClass(`room-board-grid-legend-icon dx-icon dx-icon-${item.icon}`)
                        .attr("aria-hidden", "true")
                )
                .append($("<span>").text(t(item.labelKey)))
                .appendTo($items);
        });
    }

    function init(containerSelector, getFilters, t, options) {
        const opts = options && typeof options === "object" ? options : {};
        onBoardRefreshCb =
            typeof opts.onBoardRefresh === "function" ? opts.onBoardRefresh : null;
        roomPostFilterCb = typeof opts.roomFilter === "function" ? opts.roomFilter : null;

        $gridHost = ensureGridShell(containerSelector);
        renderGridLegend(t);

        try {
            localStorage.removeItem("zaaer.roomBoard.gridState.v3");
        } catch {
            /* ignore */
        }

        const cardView = window.Zaaer && window.Zaaer.RoomCardView;
        const actionCol =
            cardView && typeof cardView.buildGridActionColumn === "function"
                ? cardView.buildGridActionColumn(t, onBoardRefreshCb)
                : null;

        const dataCols = [
            {
                dataField: "apartmentName",
                caption: t("roomBoard.room"),
                width: 148,
                minWidth: 120,
                fixed: true,
                fixedPosition: isRtl() ? "right" : "left",
                cssClass: "room-board-grid-room-col",
                cellTemplate(container, options) {
                    const room = options.data || {};
                    const label = room.apartmentName || room.apartmentCode || "-";
                    const $cell = $("<div>").addClass("room-board-grid-room-cell").appendTo(container);
                    $("<span>").addClass("room-board-grid-room-code").text(label).appendTo($cell);
                    const $icons = $("<span>").addClass("room-board-grid-room-icons").appendTo($cell);
                    if (cardView && typeof cardView.appendRoomBoardGridIndicators === "function") {
                        cardView.appendRoomBoardGridIndicators($icons, room, t);
                    }
                }
            },
            {
                dataField: "operationalStatus",
                caption: t("roomBoard.status"),
                width: 120,
                minWidth: 96,
                cellTemplate(container, options) {
                    const cardView = window.Zaaer && window.Zaaer.RoomCardView;
                    if (cardView && typeof cardView.renderRoomStatusPill === "function") {
                        cardView.renderRoomStatusPill(container, options.data || {}, t);
                        return;
                    }

                    $("<span>")
                        .addClass(`room-status-pill ${options.data.statusCssClass}`)
                        .text(t(`status.${options.value}`))
                        .appendTo(container);
                }
            },
            {
                dataField: "roomTypeName",
                caption: t("roomBoard.roomType"),
                minWidth: 120,
                calculateDisplayValue(row) {
                    const labeler = window.Zaaer && window.Zaaer.RoomTypeLabels;
                    if (labeler && typeof labeler.display === "function") {
                        return labeler.display(row.roomTypeName, t);
                    }
                    return row.roomTypeName;
                }
            },
            {
                dataField: "customerName",
                caption: t("roomBoard.guest"),
                minWidth: 110
            },
            {
                dataField: "checkInDateShort",
                caption: t("roomBoard.checkIn"),
                width: 100,
                minWidth: 88
            },
            {
                dataField: "checkOutDateShort",
                caption: t("roomBoard.checkOut"),
                width: 100,
                minWidth: 88
            },
            {
                dataField: "buildingName",
                caption: t("roomBoard.building"),
                width: 100,
                visible: false
            },
            {
                dataField: "floorName",
                caption: t("roomBoard.floor"),
                width: 90,
                visible: false
            },
            {
                dataField: "housekeepingStatus",
                caption: t("roomBoard.housekeeping"),
                width: 110,
                minWidth: 96,
                calculateDisplayValue(row) {
                    return row.housekeepingStatus ? t(`housekeeping.${row.housekeepingStatus}`) : "-";
                }
            }
        ];

        const columns = actionCol ? [actionCol, ...dataCols] : dataCols;

        const po = window.Zaaer.PmsGridOptions;
        gridInstance = $gridHost
            .dxDataGrid(
                po.merge(po.baseline(), {
                    dataSource: window.Zaaer.RoomBoardService.createRoomsStore(getFilters, roomPostFilterCb),
                    keyExpr: "apartmentId",
                    remoteOperations: false,
                    rowAlternationEnabled: false,
                    repaintChangesOnly: true,
                    allowColumnResizing: true,
                    hoverStateEnabled: true,
                    rtlEnabled: isRtl(),
                    elementAttr: { class: "pms-grid-compact room-board-grid-widget" },
                    loadPanel: { enabled: true },
                    searchPanel: po.searchPanelOptions({
                        placeholder: t("roomBoard.search")
                    }),
                    stateStoring: {
                        enabled: true,
                        type: "localStorage",
                        storageKey: "zaaer.roomBoard.gridState.v4",
                        customLoad() {
                            try {
                                const raw = localStorage.getItem("zaaer.roomBoard.gridState.v4");
                                if (!raw) {
                                    return null;
                                }

                                const state = JSON.parse(raw);
                                if (state && typeof state.paging === "boolean") {
                                    delete state.paging;
                                }

                                return state;
                            } catch {
                                return null;
                            }
                        }
                    },
                    columns,
                    paging: { pageSize: 30 },
                    pager: {
                        showInfo: true,
                        showNavigationButtons: true,
                        showPageSizeSelector: true,
                        allowedPageSizes: [30, 60, 100]
                    },
                    onRowPrepared(e) {
                        if (e.rowType === "data" && e.data) {
                            e.rowElement.attr("title", t("roomBoard.gridRowHint"));
                        }
                    }
                })
            )
            .dxDataGrid("instance");

        if (cardView && typeof cardView.attachDataGridRoomActions === "function") {
            cardView.attachDataGridRoomActions(gridInstance, t, onBoardRefreshCb);
        }

        return gridInstance;
    }

    function refresh() {
        if (gridInstance) {
            gridInstance.refresh();
        }
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.RoomBoardGrid = {
        init,
        refresh
    };
})(window, jQuery);
