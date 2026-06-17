using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Integrations
{
    /// <summary>
    /// Maps PMS reservation state to NTMP CreateOrUpdateBooking parameters (API guide v2.13).
    /// Event 1: new booking — type 1, cuFlag 1, no transactionId.
    /// Event 2: booking update — type 1, cuFlag 2, with transactionId.
    /// Event 3: check-in (existing booking) — type 2, cuFlag 1, with transactionId.
    /// Event 3a: walk-in — type 1, cuFlag 1, no transactionId.
    /// Event 5/6: check-out — type 3, cuFlag 1 or 2, with transactionId.
    /// </summary>
    public enum NtmpBookingOperation
    {
        Booking = 1,
        CheckIn = 2,
        CheckOut = 3
    }

    public static class NtmpBookingSyncPlanner
    {
        public const int StageBooking = 1;
        public const int StageCheckIn = 2;
        public const int StageCheckOut = 4;

        public sealed class Plan
        {
            public int TransactionTypeId { get; init; }
            public string CuFlag { get; init; } = "1";
            public string LogEventType { get; init; } = "CreateBooking";
            public int StageBits { get; init; }
            public bool IncludeTransactionId { get; init; }
        }

        public static Plan PlanSync(Reservation reservation, NtmpBookingOperation operation)
        {
            var hasTxn = !string.IsNullOrWhiteSpace(reservation.NtmpTransactionId);
            var stages = reservation.NtmpSyncedStages;
            var logEventType = hasTxn ? "UpdateBooking" : "CreateBooking";

            return operation switch
            {
                NtmpBookingOperation.Booking => PlanBooking(hasTxn, stages, logEventType),
                NtmpBookingOperation.CheckIn => PlanCheckIn(hasTxn, stages, logEventType),
                NtmpBookingOperation.CheckOut => PlanCheckOut(hasTxn, stages, logEventType),
                _ => PlanBooking(hasTxn, stages, logEventType)
            };
        }

        private static Plan PlanBooking(bool hasTxn, int stages, string logEventType)
        {
            var bookingSynced = (stages & StageBooking) != 0;
            return new Plan
            {
                TransactionTypeId = NtmpApiConstants.TransactionTypeBooking,
                CuFlag = bookingSynced ? "2" : "1",
                LogEventType = logEventType,
                StageBits = StageBooking,
                IncludeTransactionId = hasTxn && bookingSynced
            };
        }

        private static Plan PlanCheckIn(bool hasTxn, int stages, string logEventType)
        {
            var bookingSynced = (stages & StageBooking) != 0 || hasTxn;
            if (!bookingSynced)
            {
                // Walk-in (event 3a): no prior booking sync — create at check-in.
                return new Plan
                {
                    TransactionTypeId = NtmpApiConstants.TransactionTypeBooking,
                    CuFlag = "1",
                    LogEventType = logEventType,
                    StageBits = StageBooking | StageCheckIn,
                    IncludeTransactionId = false
                };
            }

            var checkInSynced = (stages & StageCheckIn) != 0;
            return new Plan
            {
                TransactionTypeId = NtmpApiConstants.TransactionTypeCheckIn,
                CuFlag = checkInSynced ? "2" : "1",
                LogEventType = logEventType,
                StageBits = StageCheckIn,
                IncludeTransactionId = hasTxn
            };
        }

        private static Plan PlanCheckOut(bool hasTxn, int stages, string logEventType)
        {
            var checkOutSynced = (stages & StageCheckOut) != 0;
            return new Plan
            {
                TransactionTypeId = NtmpApiConstants.TransactionTypeCheckOut,
                CuFlag = checkOutSynced ? "2" : "1",
                LogEventType = logEventType,
                StageBits = StageCheckOut,
                IncludeTransactionId = hasTxn
            };
        }
    }
}
