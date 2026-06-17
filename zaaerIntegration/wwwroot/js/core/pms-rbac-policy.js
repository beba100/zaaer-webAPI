/**
 * PMS RBAC policy (stable — do not change per incident).
 *
 * - *.update     = general mutation within the module/screen
 * - sub-perms    = partial edits when update is absent
 * - actions      = independent (check_in, reopen, …); not implied by update on API
 * - backend      = RequirePermission + domain guards (final authority)
 * - frontend     = hide/disable before the user acts
 */
(function (window) {
    "use strict";

    function api() {
        return window.Zaaer && window.Zaaer.ApiService;
    }

    function has(code) {
        const svc = api();
        return !!(svc && typeof svc.hasPermission === "function" && svc.hasPermission(code));
    }

    function hasAny(codes) {
        if (!Array.isArray(codes)) {
            return false;
        }

        return codes.some((c) => has(c));
    }

    /** General reservation save / PATCH. */
    function canReservationUpdate() {
        return has("reservations.update");
    }

    function canReservationCreate() {
        return has("reservations.create");
    }

    function canReservationView() {
        return hasAny(["reservations.view", "reservations.update", "reservations.create"]);
    }

    function canRoomBoardView() {
        return has("room_board.view");
    }

    function canRoomBoardUpdateStatus() {
        return has("room_board.update_status");
    }

    function canGuestCreate() {
        return has("guests.create");
    }

    function canGuestUpdate() {
        return has("guests.update");
    }

    function canGuestView() {
        return hasAny(["guests.view", "guests.create", "guests.update"]);
    }

    function canPaymentCreate() {
        return has("payments.create");
    }

    function canPaymentCancel() {
        return has("payments.cancel");
    }

    function canPaymentRefundVoucherCancel() {
        return has("payments.refund_voucher.cancel");
    }

    function canPaymentRefund() {
        return has("payments.refund");
    }

    function canCorporateOnReservation() {
        return hasAny(["reservations.company_add", "reservations.update"]);
    }

    function canResortTicketsView() {
        return has("resort_tickets.view");
    }

    function canResortTicketsIssue() {
        return has("resort_tickets.issue");
    }

    function canResortTicketsCancel() {
        return has("resort_tickets.cancel");
    }

    function canResortTicketsPrint() {
        return has("resort_tickets.print");
    }

    window.Zaaer = window.Zaaer || {};
    window.Zaaer.PmsRbacPolicy = {
        has,
        hasAny,
        canReservationUpdate,
        canReservationCreate,
        canReservationView,
        canRoomBoardView,
        canRoomBoardUpdateStatus,
        canGuestCreate,
        canGuestUpdate,
        canGuestView,
        canPaymentCreate,
        canPaymentCancel,
        canPaymentRefundVoucherCancel,
        canPaymentRefund,
        canCorporateOnReservation,
        canResortTicketsView,
        canResortTicketsIssue,
        canResortTicketsCancel,
        canResortTicketsPrint
    };
})(window);
