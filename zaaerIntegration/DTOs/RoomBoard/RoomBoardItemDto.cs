#pragma warning disable CS1591

namespace zaaerIntegration.DTOs.RoomBoard
{
    public sealed class RoomBoardItemDto
    {
        public int ApartmentId { get; init; }
        public int HotelId { get; init; }
        public int? InternalApartmentId { get; init; }
        public string ApartmentCode { get; init; } = string.Empty;
        public string? ApartmentName { get; init; }
        public string OperationalStatus { get; init; } = string.Empty;
        public string StatusCssClass { get; init; } = string.Empty;
        public string? ApartmentStatus { get; init; }
        public string? HousekeepingStatus { get; init; }
        public bool IsDepartureToday { get; init; }
        /// <summary>Checked-in stay whose planned departure is before the board date.</summary>
        public bool IsOverstay { get; init; }
        public int OverstayDays { get; init; }
        public bool HasUnpaidBalance { get; init; }
        public bool StatusType { get; init; }
        public string CheckInDateShort { get; init; } = string.Empty;
        public string CheckOutDateShort { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public int? CustomerId { get; init; }
        public decimal BalanceAmount { get; init; }
        public string RentalType { get; init; } = string.Empty;
        /// <summary>True when the linked reservation has more than one distinct pricing period rental type.</summary>
        public bool HasMixedRentalPeriods { get; init; }
        public string ReservationNo { get; init; } = string.Empty;
        public string ReservationStatus { get; init; } = string.Empty;
        public string? OccupiedCardBackColor { get; init; }
        public string? OccupiedHeaderBackColor { get; init; }
        public string? OccupiedGuestBackColor { get; init; }
        public string? OccupiedDatesBackColor { get; init; }
        public string? OccupiedTextColor { get; init; }
        public int? BuildingId { get; init; }
        public string? BuildingName { get; init; }
        public int? FloorId { get; init; }
        public string? FloorName { get; init; }
        public int? RoomTypeId { get; init; }
        public string? RoomTypeName { get; init; }
        /// <summary>Active maintenance reason code when the room is under maintenance.</summary>
        public string? MaintenanceReason { get; init; }
        /// <summary>Active maintenance work types (ac, paint, pest_control, other).</summary>
        public IReadOnlyList<string> MaintenanceCategories { get; init; } = Array.Empty<string>();
        /// <summary>Notes from the active maintenance record (room-board card hint).</summary>
        public string? MaintenanceComment { get; init; }
        /// <summary>End date (dd/MM/yyyy) of the active maintenance record.</summary>
        public string MaintenanceToDateShort { get; init; } = string.Empty;
        public CurrentStayDto? CurrentStay { get; init; }
        public NextStayDto? NextStay { get; init; }
    }
}
