(function (window) {
    "use strict";

    /** @type {ReadonlyArray<{ id: string, reportKey: string, titleKey: string, link: string, icon: string }>} */
    const LODGING_REPORTS = Object.freeze([
        { id: "nav-hotel-report-daily-journal", reportKey: "daily_journal", titleKey: "hotelReports.nav.dailyJournal", link: "/hotel-report-daily-journal.html", icon: "doc" },
        { id: "nav-hotel-report-cash-ledger", reportKey: "cash_ledger", titleKey: "hotelReports.nav.cashLedger", link: "/hotel-report-cash-ledger.html", icon: "chart" },
        { id: "nav-hotel-report-network-cash", reportKey: "network_cash", titleKey: "hotelReports.nav.networkCashPayments", link: "/hotel-report-network-cash.html", icon: "money" },
        { id: "nav-hotel-report-bookings", reportKey: "bookings", titleKey: "hotelReports.nav.bookings", link: "/hotel-report-bookings.html", icon: "event" },
        { id: "nav-hotel-report-departures", reportKey: "departures", titleKey: "hotelReports.nav.departures", link: "/hotel-report-departures.html", icon: "runner" },
        { id: "nav-hotel-report-online-bookings", reportKey: "online_bookings", titleKey: "hotelReports.nav.onlineBookings", link: "/hotel-report-online-bookings.html", icon: "globe" },
        { id: "nav-hotel-report-unit-transfers", reportKey: "unit_transfers", titleKey: "hotelReports.nav.unitTransfers", link: "/hotel-report-unit-transfers.html", icon: "repeat" },
        { id: "nav-hotel-report-targets", reportKey: "targets", titleKey: "hotelReports.nav.targets", link: "/hotel-report-targets.html", icon: "runner" },
        { id: "nav-hotel-report-month-end-closing", reportKey: "month_end_closing", titleKey: "hotelReports.nav.monthEndClosing", link: "/hotel-report-month-end-closing.html", icon: "chart" },
        { id: "nav-hotel-report-receipts", reportKey: "receipts", titleKey: "hotelReports.nav.receipts", link: "/hotel-report-receipts.html", icon: "money" },
        { id: "nav-hotel-report-disbursements", reportKey: "disbursements", titleKey: "hotelReports.nav.disbursements", link: "/hotel-report-disbursements.html", icon: "undo" },
        { id: "nav-hotel-report-deposits", reportKey: "deposits", titleKey: "hotelReports.nav.deposits", link: "/hotel-report-deposits.html", icon: "box" },
        { id: "nav-hotel-report-expenses", reportKey: "expenses", titleKey: "hotelReports.nav.expenses", link: "/hotel-report-expenses.html", icon: "money" },
        { id: "nav-hotel-report-invoices", reportKey: "invoices", titleKey: "hotelReports.nav.invoices", link: "/hotel-report-invoices.html", icon: "doc" },
        { id: "nav-hotel-report-credit-notes", reportKey: "credit_notes", titleKey: "hotelReports.nav.creditNotes", link: "/hotel-report-credit-notes.html", icon: "clearformat" }
    ]);

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsLodgingReportsCatalog = {
        hubLink: "/hotel-reports-hub.html",
        hubNavId: "nav-hotel-reports-group",
        getAll() {
            return LODGING_REPORTS.slice();
        },
        findById(id) {
            return LODGING_REPORTS.find((item) => item.id === id) || null;
        }
    };
})(window);
