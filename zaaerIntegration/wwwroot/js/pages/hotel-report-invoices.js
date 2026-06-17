(function (window, $) {
    "use strict";

    const common = window.Zaaer.HotelReportCommon;

    $(function () {
        common.initReportPage({
            navKey: "nav-hotel-report-invoices",
            reportKey: "invoices",
            titleKey: "hotelReports.title.invoices",
            exportPrefix: "hotel-invoices",
            keyExpr: "documentId",
            load(query) {
                return common.hotelSvc().getInvoicesReport(query.fromDate, query.toDate);
            },
            computeKpiFromRows(rows, serverSummary) {
                const computed = common.computeSimpleCountAmountSummaryFromRows(rows, "totalAmount");
                return serverSummary ? Object.assign({}, serverSummary, computed) : computed;
            },
            renderKpi($host, summary, t, fmtMoney) {
                [
                    { label: t("hotelReports.kpi.count"), value: summary.count ?? summary.Count ?? 0 },
                    { label: t("hotelReports.kpi.totalAmount"), value: fmtMoney(summary.totalAmount ?? summary.TotalAmount) }
                ].forEach((c) => {
                    const $card = $("<div class='hall-reports-kpi'/>").appendTo($host);
                    $("<div class='hall-reports-kpi__label'/>").text(c.label).appendTo($card);
                    $("<div class='hall-reports-kpi__value'/>").text(c.value).appendTo($card);
                });
            },
            pdfColumns(t, fmtDate, fmtMoney) {
                return [
                    { caption: t("hotelReports.col.invoiceNo"), field: "documentNo", value: (r) => r.documentNo || r.DocumentNo },
                    { caption: t("hotelReports.col.invoiceDate"), field: "documentDate", value: (r) => fmtDate(r.documentDate || r.DocumentDate) },
                    { caption: t("hotelReports.col.amount"), field: "amount", value: (r) => fmtMoney(r.amount ?? r.Amount) },
                    { caption: t("hotelReports.col.reservationNo"), field: "reservationNo", value: (r) => r.reservationNo || r.ReservationNo }
                ];
            },
            columns(ctx) {
                return [
                    {
                        dataField: "documentNo",
                        caption: ctx.t("hotelReports.col.invoiceNo"),
                        cellTemplate(c, info) {
                            ctx.renderInvoiceLink(c, info.data);
                        }
                    },
                    ctx.dateColumn("documentDate", "hotelReports.col.invoiceDate"),
                    ctx.moneyColumn("amount", "hotelReports.col.amount"),
                    ctx.moneyColumn("amountPaid", "hotelReports.col.paid"),
                    ctx.moneyColumn("amountRemaining", "hotelReports.col.remaining"),
                    {
                        dataField: "reservationNo",
                        caption: ctx.t("hotelReports.col.reservationNo"),
                        cellTemplate(c, info) {
                            ctx.renderReservationLink(c, info.data);
                        }
                    },
                    { dataField: "unitLabel", caption: ctx.t("hotelReports.col.unit") },
                    { dataField: "customerName", caption: ctx.t("hotelReports.col.customer") },
                    {
                        dataField: "status",
                        caption: ctx.t("hotelReports.col.status"),
                        width: 100,
                        calculateCellValue(row) {
                            return ctx.mapInvoiceStatusDisplay(row.status ?? row.Status);
                        }
                    }
                ];
            }
        });
    });
})(window, jQuery);
