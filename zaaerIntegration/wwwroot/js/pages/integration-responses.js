(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const api = window.Zaaer.ApiService;

    function t(key) {
        return loc.t(key);
    }

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function unwrapList(res) {
        if (Array.isArray(res)) return res;
        if (res && Array.isArray(res.data)) return res.data;
        return [];
    }

    const filterState = {
        fromDate: null,
        toDate: null,
        bookingNo: "",
        service: "all",
        eventType: "",
        status: ""
    };

    function buildQuery() {
        const q = { take: 200, skip: 0 };
        if (filterState.fromDate) q.fromDate = formatLocalDateParam(filterState.fromDate);
        if (filterState.toDate) q.toDate = formatLocalDateParam(filterState.toDate);
        if (filterState.bookingNo) q.bookingNo = filterState.bookingNo;
        if (filterState.service && filterState.service !== "all") q.service = filterState.service;
        if (filterState.eventType) q.eventType = filterState.eventType;
        if (filterState.status) q.status = filterState.status;
        return q;
    }

    function statusCellHtml(status) {
        const ok = String(status || "").toLowerCase() === "success";
        const cls = ok ? "pms-integration-grid-status-success" : "pms-integration-grid-status-error";
        const label = ok ? t("integrations.responses.statusSuccess") : t("integrations.responses.statusError");
        return `<span class="${cls}">${label}</span>`;
    }

    function reservationDetailUrl(reservationRouteId, hotelCode) {
        if (!reservationRouteId) {
            return "";
        }

        const params = new URLSearchParams();
        params.set("id", String(reservationRouteId));
        const hc = (hotelCode && `${hotelCode}`.trim()) || api.getHotelCode();
        if (hc) {
            params.set("hotelCode", hc);
        }

        return `/reservation-detail.html?${params.toString()}`;
    }

    function renderReservationNoCell(container, row) {
        const resNo = (row.resNo || "").trim();
        const routeId = row.reservationRouteId ?? row.ReservationRouteId;
        const hotelCode = row.hotelCode || row.HotelCode;

        if (!resNo) {
            $("<span>").text("—").appendTo(container);
            return;
        }

        if (!routeId) {
            $("<span>").addClass("pms-integration-reservation-ref").text(resNo).appendTo(container);
            return;
        }

        const $link = $("<a>")
            .addClass("pms-integration-reservation-link")
            .attr("href", reservationDetailUrl(routeId, hotelCode))
            .attr("target", "_blank")
            .attr("rel", "noopener noreferrer")
            .attr("title", t("integrations.responses.openReservation"))
            .on("click", (e) => e.stopPropagation());

        $("<span>").addClass("dx-icon dx-icon-link").appendTo($link);
        $("<span>").text(resNo).appendTo($link);
        $link.appendTo(container);
    }

    function openDetail(row) {
        api.get(`/api/v1/pms/integrations/responses/${row.responseId}`).then((res) => {
            const data = res && res.data ? res.data : res;
            const $host = $("#responseDetailPopupHost").empty();
            const $popup = $("<div/>").appendTo($host);
            $popup.dxPopup({
                title: t("integrations.common.details"),
                width: Math.min(900, window.innerWidth - 24),
                height: "auto",
                maxHeight: "70vh",
                visible: true,
                showCloseButton: true,
                hideOnOutsideClick: true,
                shading: true,
                shadingColor: "rgba(15, 23, 42, 0.24)",
                wrapperAttr: { class: "res-extra-popup res-extra-select-popup" },
                contentTemplate() {
                    const $wrap = $("<div/>").css({ padding: "12px", fontSize: "13px" });
                    $wrap.append($("<p/>").text(`${t("integrations.responses.colError")}: ${data.errorMessage || "—"}`));
                    $wrap.append($("<h4/>").text("Request"));
                    $wrap.append($("<pre/>").css({ whiteSpace: "pre-wrap", maxHeight: "200px", overflow: "auto" }).text(data.requestPayload || "—"));
                    $wrap.append($("<h4/>").text("Response"));
                    $wrap.append($("<pre/>").css({ whiteSpace: "pre-wrap", maxHeight: "200px", overflow: "auto" }).text(data.responsePayload || "—"));
                    return $wrap;
                },
                toolbarItems: [
                    {
                        widget: "dxButton",
                        toolbar: "bottom",
                        location: "after",
                        options: {
                            text: t("common.close"),
                            onClick() {
                                $popup.dxPopup("instance").hide();
                            }
                        }
                    }
                ]
            });
        });
    }

    function initFilters() {
        const $f = $("#responsesFilters");
        const today = new Date();
        const weekAgo = new Date(today);
        weekAgo.setDate(weekAgo.getDate() - 7);
        filterState.fromDate = weekAgo;
        filterState.toDate = today;

        $("<div/>").appendTo($f).dxDateBox({
            label: t("integrations.responses.fromDate"),
            value: filterState.fromDate,
            type: "date",
            openOnFieldClick: true,
            onValueChanged(e) {
                filterState.fromDate = e.value;
            }
        });
        $("<div/>").appendTo($f).dxDateBox({
            label: t("integrations.responses.toDate"),
            value: filterState.toDate,
            type: "date",
            openOnFieldClick: true,
            onValueChanged(e) {
                filterState.toDate = e.value;
            }
        });
        $("<div/>").appendTo($f).dxTextBox({
            label: t("integrations.responses.bookingNo"),
            onValueChanged(e) {
                filterState.bookingNo = e.value || "";
            }
        });
        $("<div/>").appendTo($f).dxSelectBox({
            label: t("integrations.responses.service"),
            value: "all",
            items: [
                { value: "all", text: t("integrations.responses.serviceAll") },
                { value: "NTMP", text: "NTMP" },
                { value: "Shomoos", text: "Shomoos" },
                { value: "ZATCA", text: "ZATCA" }
            ],
            valueExpr: "value",
            displayExpr: "text",
            onValueChanged(e) {
                filterState.service = e.value;
            }
        });
        $("<div/>").appendTo($f).dxSelectBox({
            label: t("integrations.responses.status"),
            items: ["", "Success", "Error"],
            onValueChanged(e) {
                filterState.status = e.value || "";
            }
        });
        $("<div/>").appendTo($f).dxButton({
            text: t("integrations.responses.search"),
            type: "default",
            onClick() {
                window.__responsesGrid.refresh();
            }
        });
        $("<div/>").appendTo($f).dxButton({
            text: t("integrations.responses.reset"),
            onClick() {
                filterState.bookingNo = "";
                filterState.service = "all";
                filterState.eventType = "";
                filterState.status = "";
                window.__responsesGrid.refresh();
            }
        });
    }

    function initGrid() {
        const po = window.Zaaer.PmsGridOptions;
        window.__responsesGrid = $("#responsesGrid").dxDataGrid(
            po.merge(po.baseline(), {
            dataSource: new DevExpress.data.CustomStore({
                load() {
                    return api.get("/api/v1/pms/integrations/responses", buildQuery()).then(unwrapList);
                }
            }),
            columns: [
                {
                    dataField: "resNo",
                    caption: t("integrations.responses.colBooking"),
                    minWidth: 128,
                    cssClass: "pms-integration-col-booking",
                    cellTemplate(container, info) {
                        renderReservationNoCell(container, info.data || {});
                    }
                },
                { dataField: "service", caption: t("integrations.responses.colService"), minWidth: 88 },
                { dataField: "eventType", caption: t("integrations.responses.colEvent"), minWidth: 160 },
                {
                    dataField: "status",
                    caption: t("integrations.responses.colStatus"),
                    cellTemplate(container, info) {
                        $(container).html(statusCellHtml(info.value));
                    }
                },
                {
                    dataField: "createdAt",
                    caption: t("integrations.responses.colCreated"),
                    dataType: "datetime",
                    format: "dd/MM/yyyy HH:mm"
                },
                {
                    type: "buttons",
                    width: 56,
                    buttons: [{
                        hint: t("integrations.common.details"),
                        icon: "info",
                        onClick(e) {
                            openDetail(e.row.data);
                        }
                    }]
                }
            ]
            })
        ).dxDataGrid("instance");
    }

    $(function () {
        loc.init();
        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-integrations-responses",
            onRefresh() {
                if (window.__responsesGrid) window.__responsesGrid.refresh();
            }
        });
        initFilters();
        initGrid();
    });
})(window, jQuery);
