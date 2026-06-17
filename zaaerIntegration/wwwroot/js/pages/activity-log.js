(function (window, $) {
    "use strict";

    function getHotelIdFromQuery() {
        const params = new URLSearchParams(window.location.search);
        const raw = params.get("hotelId") || params.get("HotelId");
        if (raw == null || raw === "") {
            return null;
        }

        const n = Number(raw);
        return Number.isFinite(n) ? n : null;
    }

    function formatLocalDateParam(value) {
        const d = value instanceof Date ? value : new Date(value);
        const y = d.getFullYear();
        const m = String(d.getMonth() + 1).padStart(2, "0");
        const day = String(d.getDate()).padStart(2, "0");
        return `${y}-${m}-${day}`;
    }

    function initPage() {
        if (window.Zaaer.PmsTopChrome && typeof window.Zaaer.PmsTopChrome.initHeaderHotelPicker === "function") {
            if (typeof window.Zaaer.PmsTopChrome.ensureHeaderHotelHost === "function") {
                window.Zaaer.PmsTopChrome.ensureHeaderHotelHost();
            }
            window.Zaaer.PmsTopChrome.initHeaderHotelPicker({
                onHotelChanged() {
                    window.location.reload();
                }
            });
        }

        document.title = t("activityLog.pageTitle");
        $("#activityLogPageTitle").text(t("activityLog.pageTitle"));

        const hotelId = getHotelIdFromQuery();
        const filterState = {
            dateFrom: null,
            dateTo: null,
            reservationNo: "",
            eventKey: ""
        };

        const $filters = $("#activityLogFilters");
        const $fromWrap = $("<div>").addClass("activity-log-filter-field").appendTo($filters);
        $("<label>").text(t("activityLog.filterFrom")).appendTo($fromWrap);
        const $from = $("<div>").appendTo($fromWrap);
        $from.dxDateBox({
            type: "date",
            displayFormat: "yyyy-MM-dd",
            openOnFieldClick: true,
            onValueChanged(e) {
                filterState.dateFrom = e.value;
            }
        });

        const $toWrap = $("<div>").addClass("activity-log-filter-field").appendTo($filters);
        $("<label>").text(t("activityLog.filterTo")).appendTo($toWrap);
        const $to = $("<div>").appendTo($toWrap);
        $to.dxDateBox({
            type: "date",
            displayFormat: "yyyy-MM-dd",
            openOnFieldClick: true,
            onValueChanged(e) {
                filterState.dateTo = e.value;
            }
        });

        const $noWrap = $("<div>").addClass("activity-log-filter-field").appendTo($filters);
        $("<label>").text(t("activityLog.filterReservationNo")).appendTo($noWrap);
        const $no = $("<div>").appendTo($noWrap);
        $no.dxTextBox({
            onValueChanged(e) {
                filterState.reservationNo = e.value || "";
            }
        });

        const $searchWrap = $("<div>").addClass("activity-log-filter-field").appendTo($filters);
        $("<div>").appendTo($searchWrap).dxButton({
            text: t("activityLog.search"),
            type: "default",
            stylingMode: "contained",
            icon: "search",
            onClick() {
                runSearch();
            }
        });

        function runSearch() {
            const svc = window.Zaaer && window.Zaaer.ReservationDetailService;
            if (!svc || typeof svc.searchActivityLogs !== "function") {
                DevExpress.ui.notify(t("activityLog.loadFailed"), "error", 3200);
                return;
            }

            const query = { skip: 0, take: 100 };
            if (hotelId != null) {
                query.hotelId = hotelId;
            }

            if (filterState.dateFrom) {
                query.dateFrom = formatLocalDateParam(filterState.dateFrom);
            }

            if (filterState.dateTo) {
                query.dateTo = `${formatLocalDateParam(filterState.dateTo)}T23:59:59`;
            }

            if (filterState.reservationNo && String(filterState.reservationNo).trim()) {
                query.reservationNo = String(filterState.reservationNo).trim();
            }

            const $results = $("#activityLogResults").empty();
            $("<div>").addClass("activity-log-loading").text(t("common.loading")).appendTo($results);

            svc.searchActivityLogs(query)
                .then((rows) => {
                    if (window.PmsActivityLogRender && typeof window.PmsActivityLogRender.renderActivityLogTimeline === "function") {
                        window.PmsActivityLogRender.renderActivityLogTimeline($results, rows);
                        return;
                    }

                    $results.empty();
                    $("<p>").addClass("activity-log-empty").text(t("activityLog.empty")).appendTo($results);
                })
                .catch((err) => {
                    $results.empty();
                    $("<p>")
                        .addClass("activity-log-empty")
                        .text((err && err.message) || t("activityLog.loadFailed"))
                        .appendTo($results);
                });
        }

        if (hotelId != null) {
            runSearch();
        }
    }

    $(function () {
        if (window.Zaaer && window.Zaaer.Localization && typeof window.Zaaer.Localization.init === "function") {
            window.Zaaer.Localization.init().then(initPage);
        } else {
            initPage();
        }
    });
})(window, jQuery);
