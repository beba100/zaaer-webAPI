namespace zaaerIntegration.Services.ActivityLog
{
    /// <summary>Stable event keys for activity_logs.event_key (PMS + i18n on client).</summary>
    public static class ReservationActivityEvents
    {
        public const string ReservationCreated = "reservation.created";
        public const string ReservationCheckIn = "reservation.check_in";
        public const string ReservationCheckOut = "reservation.check_out";
        public const string ReservationCancelled = "reservation.cancelled";
        public const string ReservationReopened = "reservation.reopened";
        public const string ReservationUpdated = "reservation.updated";
        public const string UnitAdded = "reservation.unit_added";
        public const string UnitRemoved = "reservation.unit_removed";
        public const string UnitCheckOut = "reservation.unit_check_out";
        public const string PaymentReceiptCreated = "payment.receipt_created";
        public const string PaymentReceiptUpdated = "payment.receipt_updated";
        public const string PaymentRefundCreated = "payment.refund_created";
        public const string PaymentRefundUpdated = "payment.refund_updated";
        public const string PromissoryCreated = "promissory.created";
        public const string PromissoryUpdated = "promissory.updated";
        public const string PromissoryCancelled = "promissory.cancelled";
        public const string PromissoryCollected = "promissory.collected";
        public const string UnitRateUpdated = "reservation.unit_rate_updated";
        public const string DiscountApplied = "reservation.discount_applied";
        public const string PenaltyApplied = "reservation.penalty_applied";
        public const string PackageAdded = "reservation.package_added";
        public const string NoteAdded = "reservation.note_added";
        public const string InvoiceCreated = "invoice.created";
        public const string CreditNoteCreated = "credit_note.created";
        public const string DebitNoteCreated = "debit_note.created";
        public const string RentalPeriodAppended = "reservation.rental_period_appended";
        public const string RentalPeriodUpdated = "reservation.rental_period_updated";

        public static string DefaultIcon(string eventKey) =>
            eventKey switch
            {
                ReservationCreated => "event",
                ReservationCheckIn => "check",
                ReservationCheckOut => "runner",
                ReservationCancelled => "warning",
                ReservationReopened => "undo",
                ReservationUpdated => "edit",
                UnitAdded => "box",
                UnitRemoved => "trash",
                UnitCheckOut => "runner",
                PaymentReceiptCreated => "money",
                PaymentReceiptUpdated => "edit",
                PaymentRefundCreated => "undo",
                PaymentRefundUpdated => "edit",
                PromissoryCreated => "card",
                PromissoryUpdated => "edit",
                PromissoryCancelled => "warning",
                PromissoryCollected => "money",
                UnitRateUpdated => "edit",
                DiscountApplied => "percent",
                PenaltyApplied => "warning",
                PackageAdded => "plus",
                NoteAdded => "comment",
                InvoiceCreated => "money",
                CreditNoteCreated => "undo",
                DebitNoteCreated => "edit",
                RentalPeriodAppended => "plus",
                RentalPeriodUpdated => "edit",
                _ => "info"
            };
    }
}
