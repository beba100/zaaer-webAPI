(function (window, $) {
    "use strict";

    const loc = window.Zaaer.LocalizationService;
    const service = window.Zaaer.ResortTicketService;
    const api = window.Zaaer.ApiService;
    const po = window.Zaaer.PmsGridOptions;

    let loadPanel;
    let collectionGrid;
    let disbursementGrid;
    let pendingGrid;
    let filterFrom;
    let filterTo;
    let filterFromBox;
    let filterToBox;
    let $kpiHost;

    function t(key) {
        return loc.t(key);
    }

    function isAr() {
        return loc.currentCulture && loc.currentCulture() === "ar";
    }

    function canFinance() {
        return api.hasPermission("resort_tickets.finance");
    }

    function today() {
        const d = new Date();
        d.setHours(0, 0, 0, 0);
        return d;
    }

    function formatMoney(value) {
        const n = Number(value) || 0;
        return `${n.toFixed(2)} ${t("resortTickets.currency")}`;
    }

    function filterParams() {
        return {
            fromDate: filterFrom,
            toDate: filterTo
        };
    }

    function withLoad(promise) {
        loadPanel.show();
        return $.when(promise).always(() => loadPanel.hide());
    }

    function receiptColumns(includeInvoice) {
        const cols = [
            { dataField: "receiptNo", caption: t("resortTickets.receipts.receiptNo"), width: 120 },
            {
                dataField: "receiptDate",
                caption: t("resortTickets.receipts.receiptDate"),
                dataType: "datetime",
                width: 150
            },
            { dataField: "orderNo", caption: t("roomBoard.resortTickets.orderNo"), width: 120 },
            {
                dataField: "serviceDate",
                caption: t("roomBoard.resortTickets.serviceDate"),
                dataType: "date",
                width: 120
            },
            {
                dataField: "amountPaid",
                caption: t("resortTickets.receipts.amount"),
                dataType: "number",
                format: "#,##0.00",
                width: 110
            },
            { dataField: "paymentMethod", caption: t("resortTickets.paymentMethod"), width: 110 },
            { dataField: "receiptStatus", caption: t("resortTickets.receipts.status"), width: 90 }
        ];

        if (includeInvoice) {
            cols.push({
                dataField: "hasInvoice",
                caption: t("resortTickets.receipts.hasInvoice"),
                dataType: "boolean",
                width: 100
            });
            cols.push({
                dataField: "invoiceNo",
                caption: t("resortTickets.invoices.invoiceNo"),
                width: 120
            });
        }

        return cols;
    }

    function buildGrid($host, keyExpr) {
        return $host
            .dxDataGrid(
                po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                    dataSource: [],
                    keyExpr,
                    height: "calc(100vh - 420px)",
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "pms-grid-compact" },
                    columns: receiptColumns(true)
                })
            )
            .dxDataGrid("instance");
    }

    function buildPendingGrid($host) {
        return $host
            .dxDataGrid(
                po.merge(po.adminBaseline ? po.adminBaseline() : {}, {
                    dataSource: [],
                    keyExpr: "ticketOrderId",
                    height: "calc(100vh - 420px)",
                    headerFilter: { visible: true, search: { enabled: true } },
                    searchPanel: { visible: true, width: 280 },
                    elementAttr: { class: "pms-grid-compact" },
                    columns: [
                        { dataField: "orderNo", caption: t("roomBoard.resortTickets.orderNo"), width: 120 },
                        {
                            dataField: "serviceDate",
                            caption: t("roomBoard.resortTickets.serviceDate"),
                            dataType: "date",
                            width: 120
                        },
                        { dataField: "receiptNo", caption: t("resortTickets.finance.receiptNo"), width: 120 },
                        {
                            dataField: "ticketCount",
                            caption: t("roomBoard.resortTickets.count"),
                            width: 80
                        },
                        {
                            dataField: "totalAmount",
                            caption: t("roomBoard.resortTickets.total"),
                            dataType: "number",
                            format: "#,##0.00",
                            width: 110
                        }
                    ]
                })
            )
            .dxDataGrid("instance");
    }

    function renderKpi(summary) {
        if (!$kpiHost) {
            return;
        }

        $kpiHost.empty();
        const data = summary || {};
        const cards = [
            {
                label: t("resortTickets.receipts.kpi.pendingOrders"),
                value: String(data.pendingInvoiceOrderCount || 0),
                sub: formatMoney(data.pendingInvoiceOrderTotal || 0),
                tone: "warn"
            },
            {
                label: t("resortTickets.receipts.kpi.collections"),
                value: String(data.collectionReceiptCount || 0),
                sub: formatMoney(data.collectionReceiptsTotal || 0),
                tone: "ok"
            },
            {
                label: t("resortTickets.receipts.kpi.disbursements"),
                value: String(data.disbursementReceiptCount || 0),
                sub: formatMoney(data.disbursementReceiptsTotal || 0),
                tone: "neutral"
            },
            {
                label: t("resortTickets.receipts.kpi.invoiced"),
                value: String(data.invoicedCount || 0),
                sub: formatMoney(data.invoicedTotal || 0),
                tone: "primary"
            },
            {
                label: t("resortTickets.receipts.kpi.variance"),
                value: formatMoney(data.pendingVsCollectionVariance || 0),
                sub: data.isBalanced
                    ? t("resortTickets.receipts.kpi.balanced")
                    : t("resortTickets.receipts.kpi.unbalanced"),
                tone: data.isBalanced ? "ok" : "danger"
            }
        ];

        cards.forEach((card) => {
            const $card = $("<div/>")
                .addClass("resort-ticket-receipts-kpi-card")
                .addClass(`resort-ticket-receipts-kpi-card--${card.tone}`)
                .appendTo($kpiHost);
            $("<div/>").addClass("resort-ticket-receipts-kpi-card__label").text(card.label).appendTo($card);
            $("<div/>").addClass("resort-ticket-receipts-kpi-card__value").text(card.value).appendTo($card);
            $("<div/>").addClass("resort-ticket-receipts-kpi-card__sub").text(card.sub).appendTo($card);
        });
    }

    function refreshAll() {
        const params = filterParams();
        return withLoad(
            $.when(
                service.getFinanceReconciliation(params),
                service.listTicketReceipts(Object.assign({ receiptKind: "collection" }, params)),
                service.listTicketReceipts(Object.assign({ receiptKind: "disbursement" }, params)),
                service.listPendingInvoiceOrders(params)
            ).then(function (summary, collections, disbursements, pending) {
                renderKpi(summary);
                if (collectionGrid) {
                    collectionGrid.option("dataSource", collections || []);
                }
                if (disbursementGrid) {
                    disbursementGrid.option("dataSource", disbursements || []);
                }
                if (pendingGrid) {
                    pendingGrid.option("dataSource", pending || []);
                }
            })
        );
    }

    function applyFilter() {
        if (filterFromBox) {
            filterFrom = filterFromBox.option("value") || today();
        }
        if (filterToBox) {
            filterTo = filterToBox.option("value") || today();
        }
        return refreshAll();
    }

    function resetFilter() {
        filterFrom = today();
        filterTo = today();
        if (filterFromBox) {
            filterFromBox.option("value", filterFrom);
        }
        if (filterToBox) {
            filterToBox.option("value", filterTo);
        }
        return refreshAll();
    }

    function initFilter() {
        filterFrom = today();
        filterTo = today();

        const $wrap = $("#resortTicketReceiptsFilter");
        $("<div/>")
            .addClass("resort-ticket-receipts-filter__field")
            .appendTo($wrap)
            .dxDateBox({
                label: t("common.fromDate"),
                labelMode: "static",
                type: "date",
                openOnFieldClick: true,
                value: filterFrom,
                onInitialized(e) {
                    filterFromBox = e.component;
                }
            });
        $("<div/>")
            .addClass("resort-ticket-receipts-filter__field")
            .appendTo($wrap)
            .dxDateBox({
                label: t("common.toDate"),
                labelMode: "static",
                type: "date",
                openOnFieldClick: true,
                value: filterTo,
                onInitialized(e) {
                    filterToBox = e.component;
                }
            });
        $("<div/>")
            .appendTo($wrap)
            .dxButton({
                text: t("resortTickets.ordersFilter.apply"),
                icon: "filter",
                type: "default",
                onClick: applyFilter
            });
        $("<div/>")
            .appendTo($wrap)
            .dxButton({
                text: t("resortTickets.ordersFilter.reset"),
                icon: "revert",
                stylingMode: "outlined",
                onClick: resetFilter
            });
    }

    function initTabs() {
        $("#resortTicketReceiptsTabs").dxTabPanel({
            deferRendering: false,
            rtlEnabled: isAr(),
            animationEnabled: true,
            items: [
                {
                    title: t("resortTickets.receipts.tab.collections"),
                    template() {
                        const $host = $("<div class='resort-ticket-receipts-grid'/>");
                        collectionGrid = buildGrid($host, "receiptId");
                        return $host;
                    }
                },
                {
                    title: t("resortTickets.receipts.tab.disbursements"),
                    template() {
                        const $host = $("<div class='resort-ticket-receipts-grid'/>");
                        disbursementGrid = buildGrid($host, "receiptId");
                        return $host;
                    }
                },
                {
                    title: t("resortTickets.receipts.tab.pendingInvoices"),
                    template() {
                        const $host = $("<div class='resort-ticket-receipts-grid'/>");
                        pendingGrid = buildPendingGrid($host);
                        return $host;
                    }
                }
            ]
        });
    }

    $(function () {
        if (!canFinance()) {
            DevExpress.ui.notify(t("common.forbidden"), "error", 3500);
            return;
        }

        loadPanel = $("#resortTicketReceiptsLoadPanel")
            .dxLoadPanel({ visible: false, showIndicator: true, shading: true })
            .dxLoadPanel("instance");

        $kpiHost = $("#resortTicketReceiptsKpi");

        window.Zaaer.PmsAdminShell.init({
            navKey: "nav-resort-ticket-receipts",
            onRefresh: refreshAll
        });

        initFilter();
        initTabs();
        refreshAll().catch((err) => {
            const msg = (err && err.responseJSON && err.responseJSON.message) || t("common.error");
            DevExpress.ui.notify(msg, "error", 3500);
        });
    });
})(window, jQuery);
