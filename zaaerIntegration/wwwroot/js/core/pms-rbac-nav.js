(function (window) {
  "use strict";

  let activePropertyMode = null;

  const NAV_PERMISSIONS = {
    "nav-board": "nav.menu.board",
    "nav-property": "nav.menu.property",
    "nav-property-settings": "nav.menu.property.settings",
    "nav-property-rates": "nav.menu.property.rates",
    "nav-booking-engine": "nav.menu.booking_engine",
    "nav-booking-engine-settings": "nav.menu.booking_engine.settings",
    "nav-booking-engine-open": "nav.menu.booking_engine.preview",
    "nav-pos": "nav.menu.pos",
    "nav-pos-terminal": "nav.menu.pos.terminal",
    "nav-pos-orders": "nav.menu.pos.orders",
    "nav-pos-settings": "nav.menu.pos.settings",
    "nav-resort-tickets": "nav.menu.resort.tickets.cashier",
    "nav-resort-ticket-scanner": "nav.menu.resort.tickets.scanner",
    "nav-resort-ticket-gate": "nav.menu.resort.tickets.gate",
    "nav-resort-ticket-settings": "nav.menu.resort.tickets.settings",
    "nav-resort-ticket-finance": "nav.menu.resort.tickets.finance",
    "nav-resort-ticket-receipts": "nav.menu.resort.tickets.receipts",
    "nav-resort-ticket-invoices": "nav.menu.resort.tickets.invoices",
    "nav-hall-operations": "nav.menu.hall.operations",
    "nav-hall-report-daily-journal": "nav.menu.hall.report.daily_journal",
    "nav-hall-report-cash-ledger": "nav.menu.hall.report.cash_ledger",
    "nav-hall-report-network-cash": "nav.menu.hall.report.network_cash",
    "nav-hall-report-bookings": "nav.menu.hall.report.bookings",
    "nav-hall-report-receipts": "nav.menu.hall.report.receipts",
    "nav-hall-report-disbursements": "nav.menu.hall.report.disbursements",
    "nav-hall-report-deposits": "nav.menu.hall.report.deposits",
    "nav-hall-report-expenses": "nav.menu.hall.report.expenses",
    "nav-hall-report-invoices": "nav.menu.hall.report.invoices",
    "nav-hall-report-credit-notes": "nav.menu.hall.report.credit_notes",
    "nav-hotel-report-bookings": "nav.menu.hotel.report.bookings",
    "nav-hotel-report-departures": "nav.menu.hotel.report.departures",
    "nav-hotel-report-online-bookings": "nav.menu.hotel.report.online_bookings",
    "nav-hotel-report-unit-transfers": "nav.menu.hotel.report.unit_transfers",
    "nav-hotel-report-targets": "nav.menu.hotel.report.targets",
    "nav-hotel-report-month-end-closing": "nav.menu.hotel.report.month_end_closing",
    "nav-hotel-report-receipts": "nav.menu.hotel.report.receipts",
    "nav-hotel-report-disbursements": "nav.menu.hotel.report.disbursements",
    "nav-hotel-report-deposits": "nav.menu.hotel.report.deposits",
    "nav-hotel-report-expenses": "nav.menu.hotel.report.expenses",
    "nav-hotel-report-cash-ledger": "nav.menu.hotel.report.cash_ledger",
    "nav-hotel-report-daily-journal": "nav.menu.hotel.report.daily_journal",
    "nav-hotel-report-network-cash": "nav.menu.hotel.report.network_cash",
    "nav-hotel-report-invoices": "nav.menu.hotel.report.invoices",
    "nav-hotel-report-credit-notes": "nav.menu.hotel.report.credit_notes",
    "nav-finance-cash": "nav.menu.finance",
    "nav-finance-expenses": "nav.menu.finance.expenses",
    "nav-finance-deposits": "nav.menu.finance.deposits",
    "nav-integrations": "nav.menu.integrations",
    "nav-integrations-ntmp": "nav.menu.integrations.ntmp",
    "nav-integrations-shomoos": "nav.menu.integrations.shomoos",
    "nav-integrations-zatca": "nav.menu.integrations.zatca",
    "nav-integrations-balady": "nav.menu.integrations.balady",
    "nav-integrations-responses": "nav.menu.integrations.responses",
    "nav-system": "nav.menu.system",
    "nav-rbac-users": "nav.menu.system.users",
    "nav-rbac-roles": "nav.menu.system.roles",
    "nav-rbac-permissions": "nav.menu.system.permissions",
    "nav-numbering-admin": "nav.menu.system.numbering"
  };

  const RESORT_LODGING_REPORT_NAV = [
    "nav.menu.resort.reports",
    "nav.menu.resort.report.daily_journal",
    "nav.menu.resort.report.cash_ledger",
    "nav.menu.resort.report.network_cash",
    "nav.menu.resort.report.bookings",
    "nav.menu.resort.report.departures",
    "nav.menu.resort.report.online_bookings",
    "nav.menu.resort.report.unit_transfers",
    "nav.menu.resort.report.targets",
    "nav.menu.resort.report.month_end_closing",
    "nav.menu.resort.report.receipts",
    "nav.menu.resort.report.disbursements",
    "nav.menu.resort.report.deposits",
    "nav.menu.resort.report.expenses",
    "nav.menu.resort.report.invoices",
    "nav.menu.resort.report.credit_notes"
  ];

  const HOTEL_LODGING_REPORT_NAV = [
    "nav.menu.hotel.reports",
    "nav.menu.hotel.report.daily_journal",
    "nav.menu.hotel.report.cash_ledger",
    "nav.menu.hotel.report.network_cash",
    "nav.menu.hotel.report.bookings",
    "nav.menu.hotel.report.departures",
    "nav.menu.hotel.report.online_bookings",
    "nav.menu.hotel.report.unit_transfers",
    "nav.menu.hotel.report.targets",
    "nav.menu.hotel.report.month_end_closing",
    "nav.menu.hotel.report.receipts",
    "nav.menu.hotel.report.disbursements",
    "nav.menu.hotel.report.deposits",
    "nav.menu.hotel.report.expenses",
    "nav.menu.hotel.report.invoices",
    "nav.menu.hotel.report.credit_notes"
  ];

  const LEGACY_NAV_PERMISSION_ALIASES = {
    "nav.menu.board": "room_board.view",
    "nav.menu.hall.operations": "hall.events.view",
    "nav.menu.property.settings": "property.settings.view",
    "nav.menu.property.rates": "property.rates.view",
    "nav.menu.booking_engine.settings": "booking_engine.settings.view",
    "nav.menu.booking_engine.preview": "booking_engine.settings.view",
    "nav.menu.pos.terminal": "pos.view",
    "nav.menu.pos.orders": "pos.view",
    "nav.menu.pos.settings": "pos.settings.view",
    "nav.menu.resort.tickets.cashier": "resort_tickets.view",
    "nav.menu.resort.tickets.scanner": "resort_tickets.validate",
    "nav.menu.resort.tickets.gate": "resort_tickets.validate",
    "nav.menu.resort.tickets.settings": "resort_tickets.manage_types",
    "nav.menu.resort.tickets.finance": "resort_tickets.finance",
    "nav.menu.resort.tickets.receipts": "resort_tickets.finance",
    "nav.menu.resort.tickets.invoices": "resort_tickets.finance",
    "nav.menu.hall.report.daily_journal": "hall.reports.daily_journal",
    "nav.menu.hall.report.cash_ledger": "hall.reports.cash_ledger",
    "nav.menu.hall.report.network_cash": "hall.reports.network_cash",
    "nav.menu.hall.report.bookings": "hall.reports.bookings",
    "nav.menu.hall.report.receipts": "hall.reports.receipts",
    "nav.menu.hall.report.disbursements": "hall.reports.disbursements",
    "nav.menu.hall.report.deposits": "hall.reports.deposits",
    "nav.menu.hall.report.expenses": "hall.reports.expenses",
    "nav.menu.hall.report.invoices": "hall.reports.invoices",
    "nav.menu.hall.report.credit_notes": "hall.reports.credit_notes",
    "nav.menu.hotel.report.daily_journal": "hotel.reports.daily_journal",
    "nav.menu.hotel.report.cash_ledger": "hotel.reports.cash_ledger",
    "nav.menu.hotel.report.network_cash": "hotel.reports.network_cash",
    "nav.menu.hotel.report.bookings": "hotel.reports.bookings",
    "nav.menu.hotel.report.departures": "hotel.reports.departures",
    "nav.menu.hotel.report.online_bookings": "hotel.reports.online_bookings",
    "nav.menu.hotel.report.unit_transfers": "hotel.reports.unit_transfers",
    "nav.menu.hotel.report.targets": "hotel.reports.targets",
    "nav.menu.hotel.report.month_end_closing": "hotel.reports.month_end_closing",
    "nav.menu.hotel.report.receipts": "hotel.reports.receipts",
    "nav.menu.hotel.report.disbursements": "hotel.reports.disbursements",
    "nav.menu.hotel.report.deposits": "hotel.reports.deposits",
    "nav.menu.hotel.report.expenses": "hotel.reports.expenses",
    "nav.menu.hotel.report.invoices": "hotel.reports.invoices",
    "nav.menu.hotel.report.credit_notes": "hotel.reports.credit_notes",
    "nav.menu.resort.report.daily_journal": "resort.reports.daily_journal",
    "nav.menu.resort.report.cash_ledger": "resort.reports.cash_ledger",
    "nav.menu.resort.report.network_cash": "resort.reports.network_cash",
    "nav.menu.resort.report.bookings": "resort.reports.bookings",
    "nav.menu.resort.report.departures": "resort.reports.departures",
    "nav.menu.resort.report.online_bookings": "resort.reports.online_bookings",
    "nav.menu.resort.report.unit_transfers": "resort.reports.unit_transfers",
    "nav.menu.resort.report.targets": "resort.reports.targets",
    "nav.menu.resort.report.month_end_closing": "resort.reports.month_end_closing",
    "nav.menu.resort.report.receipts": "resort.reports.receipts",
    "nav.menu.resort.report.disbursements": "resort.reports.disbursements",
    "nav.menu.resort.report.deposits": "resort.reports.deposits",
    "nav.menu.resort.report.expenses": "resort.reports.expenses",
    "nav.menu.resort.report.invoices": "resort.reports.invoices",
    "nav.menu.resort.report.credit_notes": "resort.reports.credit_notes",
    "nav.menu.finance.expenses": "finance.expense.view",
    "nav.menu.finance.deposits": "finance.deposit.view",
    "nav.menu.integrations.ntmp": "integrations.view",
    "nav.menu.integrations.shomoos": "integrations.view",
    "nav.menu.integrations.zatca": "integrations.view",
    "nav.menu.integrations.balady": "integrations.balady.view",
    "nav.menu.integrations.responses": "integrations.view",
    "nav.menu.system.users": "rbac.users.manage",
    "nav.menu.system.roles": "rbac.roles.manage",
    "nav.menu.system.permissions": "rbac.permissions.view",
    "nav.menu.system.numbering": "admin.numbering.manage"
  };

  const NAV_GROUP_PERMISSIONS = {
    "nav-property": [
      "nav.menu.property",
      "nav.menu.property.settings",
      "nav.menu.property.rates"
    ],
    "nav-booking-engine": [
      "nav.menu.booking_engine",
      "nav.menu.booking_engine.settings",
      "nav.menu.booking_engine.preview"
    ],
    "nav-pos": [
      "nav.menu.pos",
      "nav.menu.pos.terminal",
      "nav.menu.pos.orders",
      "nav.menu.pos.settings"
    ],
    "nav-resort-tickets-group": [
      "nav.menu.resort.tickets",
      "nav.menu.resort.tickets.cashier",
      "nav.menu.resort.tickets.scanner",
      "nav.menu.resort.tickets.gate",
      "nav.menu.resort.tickets.settings",
      "nav.menu.resort.tickets.finance",
      "nav.menu.resort.tickets.receipts",
      "nav.menu.resort.tickets.invoices"
    ],
    "nav-resort-ticket-finance-group": [
      "nav.menu.resort.tickets.finance",
      "nav.menu.resort.tickets.receipts",
      "nav.menu.resort.tickets.invoices"
    ],
    "nav-hall-events-group": [
      "nav.menu.hall",
      "nav.menu.hall.operations",
      "nav.menu.hall.reports",
      "nav.menu.hall.report.daily_journal",
      "nav.menu.hall.report.cash_ledger",
      "nav.menu.hall.report.network_cash",
      "nav.menu.hall.report.bookings",
      "nav.menu.hall.report.receipts",
      "nav.menu.hall.report.disbursements",
      "nav.menu.hall.report.deposits",
      "nav.menu.hall.report.expenses",
      "nav.menu.hall.report.invoices",
      "nav.menu.hall.report.credit_notes"
    ],
    "nav-hall-reports-group": [
      "nav.menu.hall.reports",
      "nav.menu.hall.report.daily_journal",
      "nav.menu.hall.report.cash_ledger",
      "nav.menu.hall.report.network_cash",
      "nav.menu.hall.report.bookings",
      "nav.menu.hall.report.receipts",
      "nav.menu.hall.report.disbursements",
      "nav.menu.hall.report.deposits",
      "nav.menu.hall.report.expenses",
      "nav.menu.hall.report.invoices",
      "nav.menu.hall.report.credit_notes"
    ],
    "nav-finance-cash": [
      "nav.menu.finance",
      "nav.menu.finance.expenses",
      "nav.menu.finance.deposits"
    ],
    "nav-integrations": [
      "nav.menu.integrations",
      "nav.menu.integrations.ntmp",
      "nav.menu.integrations.shomoos",
      "nav.menu.integrations.zatca",
      "nav.menu.integrations.balady",
      "nav.menu.integrations.responses"
    ],
    "nav-system": [
      "nav.menu.system",
      "nav.menu.system.users",
      "nav.menu.system.roles",
      "nav.menu.system.permissions",
      "nav.menu.system.numbering"
    ]
  };

  function normalizeReportKey(reportKey) {
    return `${reportKey || ""}`.trim().toLowerCase().replace(/-/g, "_");
  }

  function lodgingReportNavPermissions(mode) {
    return mode && mode.isResort ? RESORT_LODGING_REPORT_NAV : HOTEL_LODGING_REPORT_NAV;
  }

  function resolveNavPermissionCode(navId, mode) {
    if (!navId) {
      return null;
    }

    if (navId === "nav-board") {
      if (mode && mode.isHall) {
        return ["nav.menu.hall.operations", "nav.menu.board"];
      }
      return NAV_PERMISSIONS[navId];
    }

    if (mode && mode.isResort && navId.indexOf("nav-hotel-report-") === 0) {
      const suffix = navId.replace("nav-hotel-report-", "").replace(/-/g, "_");
      return `nav.menu.resort.report.${suffix}`;
    }

    return NAV_PERMISSIONS[navId];
  }

  function resolveHallReportPermissionKeys(reportKey) {
    const key = normalizeReportKey(reportKey);
    return [`hall.reports.${key}`, "hall.reports"];
  }

  function resolveLodgingReportPermissionKeys(reportKey, mode) {
    const key = normalizeReportKey(reportKey);
    if (mode && mode.isResort) {
      return [
        `resort.reports.${key}`,
        "resort.reports",
        `hotel.reports.${key}`,
        "hotel.reports"
      ];
    }
    return [`hotel.reports.${key}`, "hotel.reports"];
  }

    function hasNavPermission(code) {
        const api = window.Zaaer && window.Zaaer.ApiService;
        if (!api || !api.getToken()) {
            return true;
        }
        if (!code) {
            return true;
        }
        if (Array.isArray(code)) {
            return code.some((entry) => hasNavPermission(entry));
        }

        const normalized = `${code}`.trim();
        // Menu visibility follows nav.menu.* grants only (not legacy rbac/admin codes).
        if (normalized.startsWith("nav.menu.")) {
            return typeof api.hasPermission === "function" && api.hasPermission(normalized);
        }

        if (typeof api.hasPermission === "function" && api.hasPermission(code)) {
            return true;
        }
        const legacy = LEGACY_NAV_PERMISSION_ALIASES[code];
        return !!(legacy && typeof api.hasPermission === "function" && api.hasPermission(legacy));
    }

  function canSeeNavItem(item) {
    const api = window.Zaaer && window.Zaaer.ApiService;
    if (!api || !api.getToken()) {
      return true;
    }

    if (item && item.id === "nav-hotel-reports-group") {
      return lodgingReportNavPermissions(activePropertyMode).some((code) => hasNavPermission(code));
    }

    if (item && item.id && NAV_GROUP_PERMISSIONS[item.id]) {
      return NAV_GROUP_PERMISSIONS[item.id].some((code) => hasNavPermission(code));
    }

    const code = resolveNavPermissionCode(item && item.id, activePropertyMode);
    if (!code) {
      return true;
    }

    return hasNavPermission(code);
  }

  function filterNavItems(items, propertyMode) {
    activePropertyMode = propertyMode || null;
    if (!Array.isArray(items)) {
      return [];
    }

    return items
      .map((item) => {
        const next = { ...item };
        if (Array.isArray(next.items)) {
          next.items = filterNavItems(next.items, propertyMode);
        }
        return next;
      })
      .filter((item) => {
        if (!canSeeNavItem(item)) {
          return false;
        }

        if (Array.isArray(item.items)) {
          if (item.items.length > 0) {
            return true;
          }

          return !!(item.link && item.link !== "#");
        }

        return true;
      });
  }

  function handleNavItemClick(e, getHotelCode) {
    const item = e && e.itemData;
    if (!item) {
      return false;
    }

    if (item.action === "numbering-settings") {
      return "numbering-settings";
    }

    if (item.action === "open-booking-page") {
      const code =
        typeof getHotelCode === "function"
          ? getHotelCode()
          : window.Zaaer && window.Zaaer.ApiService && window.Zaaer.ApiService.getHotelCode
            ? window.Zaaer.ApiService.getHotelCode()
            : "";
      const url = code
        ? `/booking-engine.html?hotel=${encodeURIComponent(code)}`
        : "/booking-engine.html";
      window.open(url, "_blank", "noopener,noreferrer");
      return true;
    }

    const link = item.link;
    if (link && link !== "#") {
      window.location.href = link;
      return true;
    }

    return false;
  }

  window.Zaaer = window.Zaaer || {};
  window.Zaaer.PmsRbacNav = {
    filterNavItems,
    canSeeNavItem,
    handleNavItemClick,
    resolveNavPermissionCode,
    resolveHallReportPermissionKeys,
    resolveLodgingReportPermissionKeys,
    lodgingReportNavPermissions,
    NAV_PERMISSIONS,
    NAV_GROUP_PERMISSIONS,
    LEGACY_NAV_PERMISSION_ALIASES,
    hasNavPermission
  };
})(window);
