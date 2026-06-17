namespace zaaerIntegration.Services
{
    /// <summary>
    /// Master DB <c>DocumentTypes.doc_code</c> values for central numbering.
    /// Uniqueness for integration IDs is <c>(entity type, zaaer_id)</c>, not <c>zaaer_id</c> alone.
    /// </summary>
    public static class NumberingDocCodes
    {
        public const string Customer = "customer";
        public const string Corporate = "corporate";
        public const string Reservation = "reservation";
        public const string PaymentReceipt = "payment_receipt";
        public const string PaymentRefund = "payment_refund";
        public const string Invoice = "invoice";
        public const string Order = "order";
        public const string CreditNote = "credit_note";
        public const string DebitNote = "debit_note";
        public const string PromissoryNote = "promissory_note";
        public const string Expense = "expense";
        public const string Building = "building";
        public const string Floor = "floor";
        public const string Apartment = "apartment";
        public const string RoomType = "room_type";
        public const string Facility = "facility";
        public const string ResortTicketType = "resort_ticket_type";
        public const string ResortTicketOrder = "resort_ticket_order";
        public const string ResortTicket = "resort_ticket";
    }
}
