namespace FinanceLedgerAPI.Enums
{
	/// <summary>
	/// Reservation Status Enum
	/// حالات الحجز
	/// </summary>
	public enum ReservationStatus
	{
		/// <summary>
		/// Unconfirmed - غير مؤكد (Pending)
		/// </summary>
		Unconfirmed = 0,

		/// <summary>
		/// Confirmed - مؤكد
		/// </summary>
		Confirmed = 1,

		/// <summary>
		/// Checked-In - تم تسجيل الدخول
		/// </summary>
		CheckedIn = 2,

		/// <summary>
		/// Checked-Out - تم تسجيل الخروج
		/// </summary>
		CheckedOut = 3,

		/// <summary>
		/// Cancelled - ملغي
		/// </summary>
		Cancelled = 4,

		/// <summary>
		/// No-show - لم يحضر
		/// </summary>
		NoShow = 5
	}

	/// <summary>
	/// Reservation Status Helper
	/// مساعد حالات الحجز
	/// </summary>
	public static class ReservationStatusHelper
	{
		/// <summary>
		/// Get status display name
		/// الحصول على اسم الحالة للعرض
		/// </summary>
		public static string GetDisplayName(ReservationStatus status)
		{
			return status switch
			{
				ReservationStatus.Unconfirmed => "Unconfirmed",
				ReservationStatus.Confirmed => "Confirmed",
				ReservationStatus.CheckedIn => "Checked-In",
				ReservationStatus.CheckedOut => "Checked-Out",
				ReservationStatus.Cancelled => "Cancelled",
				ReservationStatus.NoShow => "No-Show",
				_ => "Unknown"
			};
		}

		/// <summary>
		/// Get status display name in Arabic
		/// الحصول على اسم الحالة بالعربية
		/// </summary>
		public static string GetDisplayNameAr(ReservationStatus status)
		{
			return status switch
			{
				ReservationStatus.Unconfirmed => "غير مؤكد",
				ReservationStatus.Confirmed => "مؤكد",
				ReservationStatus.CheckedIn => "تم تسجيل الدخول",
				ReservationStatus.CheckedOut => "تم تسجيل الخروج",
				ReservationStatus.Cancelled => "ملغي",
				ReservationStatus.NoShow => "لم يحضر",
				_ => "غير معروف"
			};
		}

		/// <summary>
		/// Get status color for UI
		/// الحصول على لون الحالة للواجهة
		/// </summary>
		public static string GetStatusColor(ReservationStatus status)
		{
			return status switch
			{
				ReservationStatus.Unconfirmed => "orange",    // Orange for Unconfirmed
				ReservationStatus.Confirmed => "blue",        // Blue for Confirmed
				ReservationStatus.CheckedIn => "green",       // Green for Checked-In
				ReservationStatus.CheckedOut => "red",        // Red for Checked-Out
				ReservationStatus.Cancelled => "grey",        // Grey for Cancelled
				ReservationStatus.NoShow => "purple",         // Distinct for No-Show
				_ => "default"
			};
		}

		/// <summary>
		/// Persisted <c>reservations.status</c> value (snake_case, matches PMS patch API).
		/// </summary>
		public static string ToStorageValue(ReservationStatus status) => status switch
		{
			ReservationStatus.Unconfirmed => "unconfirmed",
			ReservationStatus.Confirmed => "confirmed",
			ReservationStatus.CheckedIn => "checked_in",
			ReservationStatus.CheckedOut => "checked_out",
			ReservationStatus.Cancelled => "cancelled",
			ReservationStatus.NoShow => "no_show",
			_ => status.ToString().ToLowerInvariant()
		};

		/// <summary>
		/// Parse stored <c>reservations.status</c> (snake_case, legacy enum names, or display labels).
		/// </summary>
		public static bool TryParseStorage(string? value, out ReservationStatus status)
		{
			status = default;
			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			var norm = value.Trim().ToLowerInvariant()
				.Replace(" ", string.Empty, StringComparison.Ordinal)
				.Replace("-", string.Empty, StringComparison.Ordinal)
				.Replace("_", string.Empty, StringComparison.Ordinal);

			switch (norm)
			{
				case "unconfirmed":
					status = ReservationStatus.Unconfirmed;
					return true;
				case "confirmed":
					status = ReservationStatus.Confirmed;
					return true;
				case "checkedin":
				case "checkin":
					status = ReservationStatus.CheckedIn;
					return true;
				case "checkedout":
				case "checkout":
					status = ReservationStatus.CheckedOut;
					return true;
				case "cancelled":
				case "canceled":
					status = ReservationStatus.Cancelled;
					return true;
				case "noshow":
					status = ReservationStatus.NoShow;
					return true;
				default:
					return Enum.TryParse(value, true, out status);
			}
		}

		/// <summary>
		/// Check if status allows check-in
		/// التحقق من إمكانية تسجيل الدخول
		/// </summary>
		public static bool CanCheckIn(ReservationStatus status)
		{
			return status == ReservationStatus.Confirmed;
		}

		/// <summary>
		/// Check if status allows check-out
		/// التحقق من إمكانية تسجيل الخروج
		/// </summary>
		public static bool CanCheckOut(ReservationStatus status)
		{
			return status == ReservationStatus.CheckedIn;
		}

		/// <summary>
		/// Check if status allows cancellation
		/// التحقق من إمكانية الإلغاء
		/// </summary>
		public static bool CanCancel(ReservationStatus status)
		{
			return status == ReservationStatus.Unconfirmed || status == ReservationStatus.Confirmed;
		}
	}
}
