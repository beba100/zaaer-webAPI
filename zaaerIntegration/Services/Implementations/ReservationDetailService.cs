#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Pms.ReservationDetail;
using zaaerIntegration.Services.ActivityLog;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Integrations;
using FinanceLedgerAPI.Enums;
using FinanceLedgerAPI.Models;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Read model for reservation detail (PMS). Resolves reservation by <c>zaaer_id</c> first, then by internal <c>reservation_id</c>.
    /// </summary>
    /// <remarks>
    /// Equivalent SQL sketch (tenant DB):
    /// <code>
    /// DECLARE @id INT = @zaaerOrInternalId;
    /// SELECT TOP (1) r.*
    /// FROM dbo.reservations r
    /// WHERE (r.zaaer_id = @id OR r.reservation_id = @id)
    ///   AND (@hotelId IS NULL OR r.hotel_id = @hotelId)
    /// ORDER BY CASE WHEN r.zaaer_id = @id THEN 0 ELSE 1 END;
    ///
    /// -- Units + room context
    /// SELECT ru.*, a.apartment_code, a.apartment_name, rt.roomtype_name, b.building_name, f.floor_name
    /// FROM dbo.reservation_units ru
    /// INNER JOIN dbo.apartments a ON ru.apartment_id = a.apartment_id OR ru.apartment_id = a.zaaer_id
    /// LEFT JOIN dbo.room_types rt ON a.roomtype_id = rt.roomtype_id OR a.roomtype_id = rt.zaaer_id
    /// LEFT JOIN dbo.buildings b ON a.building_id = b.building_id OR a.building_id = b.zaaer_id
    /// LEFT JOIN dbo.floors f ON a.floor_id = f.floor_id OR a.floor_id = f.zaaer_id
    /// WHERE ru.reservation_id IN (@reservationPk, @zaaerReservationId);
    ///
    /// -- Guest (matches Zaaer / internal id patterns)
    /// SELECT c.*, n.n_name, n.n_name_ar
    /// FROM dbo.customers c
    /// LEFT JOIN dbo.nationality n ON c.n_id = n.n_id
    /// WHERE c.customer_id = @customerFk OR c.zaaer_id = @customerFk;
    ///
    /// -- Corporate
    /// SELECT * FROM dbo.corporate_customers WHERE corporate_id = @corporateId;
    /// </code>
    /// Prefer EF projections + AsNoTracking for maintainability; add filtered indexes on <c>reservations(zaaer_id)</c> and <c>reservation_units(reservation_id)</c> for scale.
    /// </remarks>
    public sealed class ReservationDetailService : IReservationDetailService
    {
        private readonly ApplicationDbContext _context;
        private readonly INumberingService _numberingService;
        private readonly ILogger<ReservationDetailService> _logger;
        private readonly ReservationPermissionGuard _permissionGuard;
        private readonly IReservationActivityLogWriter _activityLog;
        private readonly ICurrentUserContext _currentUser;
        private readonly INtmpIntegrationOrchestrator _ntmpOrchestrator;
        private readonly IPmsPosOrderService _posOrders;

        public ReservationDetailService(
            ApplicationDbContext context,
            INumberingService numberingService,
            ReservationPermissionGuard permissionGuard,
            IReservationActivityLogWriter activityLog,
            ICurrentUserContext currentUser,
            INtmpIntegrationOrchestrator ntmpOrchestrator,
            IPmsPosOrderService posOrders,
            ILogger<ReservationDetailService> logger)
        {
            _context = context;
            _numberingService = numberingService;
            _permissionGuard = permissionGuard;
            _activityLog = activityLog;
            _currentUser = currentUser;
            _ntmpOrchestrator = ntmpOrchestrator;
            _posOrders = posOrders;
            _logger = logger;
        }

        private static IReadOnlyList<int> GetCustomerIdentityRefs(int customerId, int? zaaerId)
        {
            var refs = new List<int> { customerId };
            if (zaaerId.HasValue && zaaerId.Value != customerId)
            {
                refs.Add(zaaerId.Value);
            }

            return refs;
        }

        private static CustomerIdentification? PickPrimaryIdentification(
            IEnumerable<CustomerIdentification> identifications,
            Customer customer)
        {
            var refs = GetCustomerIdentityRefs(customer.CustomerId, customer.ZaaerId).ToHashSet();
            return identifications
                .Where(i => refs.Contains(i.CustomerId))
                .OrderByDescending(i => i.IsActive)
                .ThenByDescending(i => i.IsPrimary)
                .ThenBy(i => i.IdentificationId)
                .FirstOrDefault();
        }

        private async Task<Dictionary<int, IdType>> BuildIdTypeLookupAsync(
            IEnumerable<int> idTypeRefs,
            CancellationToken cancellationToken)
        {
            var refs = idTypeRefs.Distinct().ToList();
            if (refs.Count == 0)
            {
                return new Dictionary<int, IdType>();
            }

            var rows = await _context.IdTypes.AsNoTracking()
                .Where(t => refs.Contains(t.ItId) || (t.ZaaerId.HasValue && refs.Contains(t.ZaaerId.Value)))
                .ToListAsync(cancellationToken);

            var lookup = new Dictionary<int, IdType>();
            foreach (var row in rows)
            {
                lookup.TryAdd(row.ItId, row);
                if (row.ZaaerId.HasValue)
                {
                    lookup.TryAdd(row.ZaaerId.Value, row);
                }
            }

            return lookup;
        }

        private async Task<int?> ResolveInternalApartmentIdAsync(
            int hotelId,
            int storedApartmentRef,
            CancellationToken cancellationToken)
        {
            var apt = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    a.HotelId == hotelId &&
                    (a.ApartmentId == storedApartmentRef || a.ZaaerId == storedApartmentRef))
                .OrderBy(a => a.ApartmentId == storedApartmentRef ? 0 : 1)
                .ThenBy(a => a.ApartmentId)
                .Select(a => new { a.ApartmentId })
                .FirstOrDefaultAsync(cancellationToken);

            return apt?.ApartmentId;
        }

        /// <summary>Stored in <c>reservation_companions.unit_id</c>: apartment zaaer when set, else internal apartment id.</summary>
        private static int CompanionStorageUnitIdFromApartment(Apartment apt) =>
            apt.ZaaerId is > 0 ? apt.ZaaerId.Value : apt.ApartmentId;

        private static int CompanionStorageReservationId(Reservation r) =>
            r.ZaaerId is > 0 ? r.ZaaerId.Value : r.ReservationId;

        private static int CompanionStorageCustomerId(Customer c) =>
            c.ZaaerId is > 0 ? c.ZaaerId.Value : c.CustomerId;

        private async Task<int> ResolveCorporateStorageIdAsync(
            int hotelId,
            int corporateRef,
            CancellationToken cancellationToken)
        {
            var corp = await _context.CorporateCustomers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.HotelId == hotelId && (c.CorporateId == corporateRef || c.ZaaerId == corporateRef),
                    cancellationToken);
            if (corp == null)
            {
                return corporateRef;
            }

            return corp.ZaaerId is > 0 ? corp.ZaaerId.Value : corp.CorporateId;
        }

        private static string NormalizeReservationHeaderStatusKey(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "confirmed";
            }

            var norm = status.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return norm switch
            {
                "checkin" or "checkedin" => "checkedin",
                "checkedout" or "checkout" => "checkedout",
                "cancelled" or "canceled" => "cancelled",
                "unconfirmed" => "unconfirmed",
                "noshow" => "noshow",
                _ => "confirmed"
            };
        }

        private static bool IsCheckedOutReservationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var norm = status.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return norm is "checkedout";
        }

        private static bool IsCheckedInReservationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var norm = status.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return norm is "checkedin" or "checkin";
        }

        private static bool IsCancelledReservationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var norm = status.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return norm is "cancelled" or "canceled";
        }

        /// <summary>
        /// Reservation header statuses that release physical rooms (vacant + dirty) on the board.
        /// </summary>
        private static bool ReleasesApartmentsAfterLeave(string? status)
        {
            return IsCheckedOutReservationStatus(status) || IsCancelledReservationStatus(status);
        }

        private static bool IsReceiptVoidedForCancel(string? receiptStatus)
        {
            return string.Equals(receiptStatus?.Trim(), "cancelled", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInvoiceVoidedForCancel(string? paymentStatus)
        {
            return string.Equals(paymentStatus?.Trim(), "reversed", StringComparison.OrdinalIgnoreCase);
        }

        private static List<int> GetReservationFinancialLinkKeys(Reservation reservation)
        {
            var keys = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId is > 0 && reservation.ZaaerId.Value != reservation.ReservationId)
            {
                keys.Add(reservation.ZaaerId.Value);
            }

            return keys.Distinct().ToList();
        }

        /// <summary>
        /// Source reservation line may move to another apartment unless it is already completed.
        /// </summary>
        private static bool CanSwapReservationUnitSource(string? unitStatus)
        {
            if (string.IsNullOrWhiteSpace(unitStatus))
            {
                return false;
            }

            var norm = unitStatus.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return norm is not ("checkedout" or "cancelled" or "noshow");
        }

        /// <summary>
        /// Target physical room must be free (apartments.status), not occupied/rented for another stay.
        /// </summary>
        private static bool IsVacantApartmentForUnitTransfer(string? apartmentStatus)
        {
            if (string.IsNullOrWhiteSpace(apartmentStatus))
            {
                return false;
            }

            var x = apartmentStatus.Trim().ToLowerInvariant();
            if (x.Contains("rent") ||
                x.Contains("occup") ||
                x.Contains("reserved") ||
                x.Contains("مشغول") ||
                x.Contains("حجز"))
            {
                return false;
            }

            return x.Contains("vacant") ||
                   x.Contains("available") ||
                   x.Contains("avail") ||
                   x.Contains("شاغر") ||
                   x.Contains("خالي") ||
                   x.Contains("free");
        }

        private static bool IsActiveMaintenanceRecordStatus(string? status)
        {
            var normalized = (status ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "-");
            return normalized is "" or "active" or "maintenance" or "open" or "inprogress" or "in-progress";
        }

        private async Task<bool> HasActiveMaintenanceForApartmentAsync(
            Apartment apt,
            CancellationToken cancellationToken)
        {
            var boardUnitId = apt.ZaaerId ?? apt.ApartmentId;
            var today = KsaTime.Now.Date;
            var tomorrow = today.AddDays(1);

            var maintenanceStatuses = await _context.Maintenances.AsNoTracking()
                .Where(m =>
                    m.HotelId == apt.HotelId &&
                    (m.UnitId == boardUnitId ||
                     m.UnitId == apt.ApartmentId ||
                     (apt.ZaaerId.HasValue && m.UnitId == apt.ZaaerId.Value)) &&
                    m.FromDate < tomorrow &&
                    m.ToDate >= today)
                .Select(m => m.Status)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return maintenanceStatuses.Any(IsActiveMaintenanceRecordStatus);
        }

        private async Task<HashSet<int>> GetInternalApartmentIdsForReservationUnitsAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(reservation);
            var apartmentRefs = await _context.ReservationUnits.AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .Select(u => u.ApartmentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var internalIds = new HashSet<int>();
            foreach (var pref in apartmentRefs)
            {
                var id = await ResolveInternalApartmentIdAsync(reservation.HotelId, pref, cancellationToken);
                if (id.HasValue)
                {
                    internalIds.Add(id.Value);
                }
            }

            return internalIds;
        }

        /// <summary>All ids that refer to the same physical apartment (internal id + zaaer id + stored ref).</summary>
        private async Task<HashSet<int>> ExpandApartmentLinkRefsAsync(
            int hotelId,
            int apartmentRef,
            CancellationToken cancellationToken)
        {
            var set = new HashSet<int> { apartmentRef };
            var internalId = await ResolveInternalApartmentIdAsync(hotelId, apartmentRef, cancellationToken);
            if (!internalId.HasValue)
            {
                return set;
            }

            set.Add(internalId.Value);

            var apt = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.HotelId == hotelId && a.ApartmentId == internalId.Value,
                    cancellationToken);
            if (apt?.ZaaerId is > 0)
            {
                set.Add(apt.ZaaerId.Value);
            }

            return set;
        }

        private async Task<bool> ApartmentRefsStillLinkedAsync(
            int hotelId,
            int beforeRef,
            HashSet<int> afterLinkRefs,
            CancellationToken cancellationToken)
        {
            var beforeLinks = await ExpandApartmentLinkRefsAsync(hotelId, beforeRef, cancellationToken);
            return beforeLinks.Any(afterLinkRefs.Contains);
        }

        /// <summary>
        /// Release one physical room (vacant + dirty HK) when a unit line no longer uses it.
        /// </summary>
        private async Task VacateApartmentLinkRefAsync(
            Reservation entity,
            int apartmentRef,
            CancellationToken cancellationToken)
        {
            var internalId = await ResolveInternalApartmentIdAsync(entity.HotelId, apartmentRef, cancellationToken);
            if (!internalId.HasValue)
            {
                return;
            }

            var apt = await _context.Apartments
                .FirstOrDefaultAsync(
                    a => a.HotelId == entity.HotelId && a.ApartmentId == internalId.Value,
                    cancellationToken);
            if (apt == null)
            {
                return;
            }

            apt.Status = "vacant";
            apt.HousekeepingStatus = "dirty";

            var boardId = await ResolveApartmentBoardIdAsync(entity.HotelId, apartmentRef, cancellationToken);
            if (boardId.HasValue)
            {
                await ClearRoomCardColorsForApartmentBoardIdsAsync(
                    entity.HotelId,
                    new[] { boardId.Value },
                    cancellationToken);
            }
        }

        /// <summary>
        /// After unit lines move to another apartment, release physical rooms that are no longer linked.
        /// </summary>
        private async Task VacateApartmentsOrphanedAfterUnitMutationAsync(
            Reservation entity,
            IReadOnlyCollection<int> apartmentRefsBefore,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(entity);
            var apartmentRefsAfter = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .Select(u => u.ApartmentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var afterLinkRefs = new HashSet<int>();
            foreach (var aptRef in apartmentRefsAfter)
            {
                var expanded = await ExpandApartmentLinkRefsAsync(entity.HotelId, aptRef, cancellationToken);
                foreach (var id in expanded)
                {
                    afterLinkRefs.Add(id);
                }
            }

            foreach (var aptRef in apartmentRefsBefore.Distinct())
            {
                if (await ApartmentRefsStillLinkedAsync(entity.HotelId, aptRef, afterLinkRefs, cancellationToken))
                {
                    continue;
                }

                await VacateApartmentLinkRefAsync(entity, aptRef, cancellationToken);
            }
        }

        private async Task<int?> ResolveApartmentBoardIdAsync(
            int hotelId,
            int apartmentRef,
            CancellationToken cancellationToken)
        {
            var apt = await _context.Apartments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.HotelId == hotelId &&
                         (a.ApartmentId == apartmentRef || a.ZaaerId == apartmentRef),
                    cancellationToken);

            if (apt == null)
            {
                return null;
            }

            return apt.ZaaerId ?? apt.ApartmentId;
        }

        private async Task ClearRoomCardColorsForApartmentBoardIdsAsync(
            int hotelId,
            IEnumerable<int> apartmentBoardIds,
            CancellationToken cancellationToken)
        {
            var ids = apartmentBoardIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return;
            }

            var rows = await _context.RoomCardColorSettings
                .Where(x => x.HotelId == hotelId && ids.Contains(x.ApartmentZaaerId))
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return;
            }

            _context.RoomCardColorSettings.RemoveRange(rows);
        }

        private async Task ClearRoomCardColorsForReservationUnitsAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var refs = await _context.ReservationUnits.AsNoTracking()
                .Where(u => GetReservationRateRefs(reservation).Contains(u.ReservationId))
                .Select(u => u.ApartmentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var boardIds = new List<int>();
            foreach (var aptRef in refs)
            {
                var boardId = await ResolveApartmentBoardIdAsync(reservation.HotelId, aptRef, cancellationToken);
                if (boardId.HasValue)
                {
                    boardIds.Add(boardId.Value);
                }
            }

            await ClearRoomCardColorsForApartmentBoardIdsAsync(reservation.HotelId, boardIds, cancellationToken);
        }

        /// <summary>
        /// Board occupancy: checked-in → rented; confirmed → reserved (awaiting arrival); otherwise vacant.
        /// Skips units already checked out / cancelled / no-show so partial checkout is not undone.
        /// </summary>
        private async Task SyncReservationApartmentsOccupancyForHeaderStatusAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            if (ReleasesApartmentsAfterLeave(reservation.Status))
            {
                return;
            }

            var key = NormalizeReservationHeaderStatusKey(reservation.Status);
            var aptStatus = key switch
            {
                "checkedin" => "rented",
                "confirmed" => "reserved",
                _ => "vacant"
            };

            var refs = GetReservationRateRefs(reservation);
            var unitRows = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            foreach (var unitRow in unitRows)
            {
                if (IsTerminalUnitLineStatus(unitRow.Status))
                {
                    continue;
                }

                var internalId = await ResolveInternalApartmentIdAsync(
                    reservation.HotelId,
                    unitRow.ApartmentId,
                    cancellationToken);
                if (!internalId.HasValue)
                {
                    continue;
                }

                var apt = await _context.Apartments
                    .FirstOrDefaultAsync(
                        a => a.HotelId == reservation.HotelId && a.ApartmentId == internalId.Value,
                        cancellationToken);
                if (apt != null)
                {
                    apt.Status = aptStatus;
                }
            }
        }

        private async Task MarkReservationApartmentsVacantDirtyAfterCheckoutAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            if (!ReleasesApartmentsAfterLeave(reservation.Status))
            {
                return;
            }

            var internalIds = await GetInternalApartmentIdsForReservationUnitsAsync(reservation, cancellationToken);
            if (internalIds.Count == 0)
            {
                return;
            }

            var apartments = await _context.Apartments
                .Where(a => a.HotelId == reservation.HotelId && internalIds.Contains(a.ApartmentId))
                .ToListAsync(cancellationToken);

            foreach (var apt in apartments)
            {
                apt.Status = "vacant";
                apt.HousekeepingStatus = "dirty";
            }
        }

        /// <summary>
        /// After re-check-in (re-open from checked-out), rooms are occupied again — reset HK to clean
        /// so boards do not keep showing "dirty" from the prior checkout.
        /// </summary>
        private async Task MarkReservationApartmentsHousekeepingCleanAfterReopenAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var internalIds = await GetInternalApartmentIdsForReservationUnitsAsync(reservation, cancellationToken);
            if (internalIds.Count == 0)
            {
                return;
            }

            var apartments = await _context.Apartments
                .Where(a => a.HotelId == reservation.HotelId && internalIds.Contains(a.ApartmentId))
                .ToListAsync(cancellationToken);

            foreach (var apt in apartments)
            {
                apt.HousekeepingStatus = "clean";
            }
        }

        private async Task<IReadOnlyList<ReservationDetailCompanionDto>> BuildCompanionsListAsync(
            int reservationPk,
            int? reservationZaaer,
            int hotelId,
            CancellationToken cancellationToken)
        {
            var reservationRefs = new List<int> { reservationPk };
            if (reservationZaaer.HasValue && reservationZaaer.Value != reservationPk)
            {
                reservationRefs.Add(reservationZaaer.Value);
            }

            var rows = await _context.ReservationCompanions
                .AsNoTracking()
                .Where(c => reservationRefs.Contains(c.ReservationId))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.CompanionId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Array.Empty<ReservationDetailCompanionDto>();
            }

            var rus = await _context.ReservationUnits.AsNoTracking()
                .Where(u => reservationRefs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var aptHints = rus.Select(u => u.ApartmentId).Distinct().ToList();
            var aptRows = aptHints.Count == 0
                ? new List<Apartment>()
                : await _context.Apartments.AsNoTracking()
                    .Where(a =>
                        a.HotelId == hotelId &&
                        (aptHints.Contains(a.ApartmentId) ||
                         (a.ZaaerId != null && aptHints.Contains(a.ZaaerId.Value))))
                    .ToListAsync(cancellationToken);

            var storedUnitToRuUnitId = new Dictionary<int, int>();
            foreach (var ru in rus)
            {
                var apt = aptRows
                    .Where(a => a.ApartmentId == ru.ApartmentId || (a.ZaaerId != null && a.ZaaerId == ru.ApartmentId))
                    .OrderBy(a => a.ApartmentId == ru.ApartmentId ? 0 : 1)
                    .ThenBy(a => a.ApartmentId)
                    .FirstOrDefault();
                if (apt == null)
                {
                    continue;
                }

                var stored = CompanionStorageUnitIdFromApartment(apt);
                storedUnitToRuUnitId[stored] = ru.UnitId;
            }

            int? MapCompanionRowToUiUnitId(int? storedOrLegacy)
            {
                if (!storedOrLegacy.HasValue)
                {
                    return null;
                }

                if (storedUnitToRuUnitId.TryGetValue(storedOrLegacy.Value, out var uid))
                {
                    return uid;
                }

                return rus.Exists(u => u.UnitId == storedOrLegacy.Value) ? storedOrLegacy : null;
            }

            var customerIds = rows
                .Select(r => r.CustomerId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var customerRows = await _context.Customers.AsNoTracking()
                .Where(c =>
                    c.HotelId == hotelId &&
                    (customerIds.Contains(c.CustomerId) ||
                     (c.ZaaerId != null && customerIds.Contains(c.ZaaerId.Value))))
                .ToListAsync(cancellationToken);

            var customersByAnyId = new Dictionary<int, Customer>();
            foreach (var c in customerRows)
            {
                customersByAnyId[c.CustomerId] = c;
                if (c.ZaaerId is > 0)
                {
                    customersByAnyId[c.ZaaerId.Value] = c;
                }
            }

            var customerRefs = customerRows
                .SelectMany(c => GetCustomerIdentityRefs(c.CustomerId, c.ZaaerId))
                .Distinct()
                .ToList();
            var idents = customerRefs.Count == 0
                ? new List<CustomerIdentification>()
                : await _context.CustomerIdentifications.AsNoTracking()
                    .Where(i => customerRefs.Contains(i.CustomerId))
                    .OrderByDescending(i => i.IsActive)
                    .ThenByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.IdentificationId)
                    .ToListAsync(cancellationToken);

            var primaryIdentifications = customerRows
                .Select(c => PickPrimaryIdentification(idents, c))
                .Where(i => i != null)
                .Select(i => i!)
                .ToList();
            var idTypes = await BuildIdTypeLookupAsync(primaryIdentifications.Select(i => i.IdTypeId), cancellationToken);

            var nIds = customerRows
                .Select(c => c.NId)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .Distinct()
                .ToList();

            var nationalities = await _context.Nationalities.AsNoTracking()
                .Where(n => nIds.Contains(n.NId))
                .ToDictionaryAsync(n => n.NId, cancellationToken);

            var list = new List<ReservationDetailCompanionDto>(rows.Count);
            foreach (var row in rows)
            {
                if (!customersByAnyId.TryGetValue(row.CustomerId, out var cust))
                {
                    continue;
                }

                var prim = PickPrimaryIdentification(idents, cust);
                IdType? idType = null;
                if (prim != null)
                {
                    idTypes.TryGetValue(prim.IdTypeId, out idType);
                }

                Nationality? nat = null;
                if (cust.NId.HasValue)
                {
                    nationalities.TryGetValue(cust.NId.Value, out nat);
                }

                list.Add(new ReservationDetailCompanionDto
                {
                    RowKey = row.CompanionId,
                    CustomerId = cust.ZaaerId is > 0 ? cust.ZaaerId.Value : cust.CustomerId,
                    CustomerZaaerId = cust.ZaaerId,
                    CustomerName = cust.CustomerName,
                    IdTypeName = idType?.ItName,
                    IdTypeNameAr = idType?.ItNameAr,
                    IdNumber = prim?.IdNumber,
                    BirthDate = cust.BirthdateGregorian ?? cust.Birthday,
                    NationalityName = nat?.NName,
                    NationalityNameAr = nat?.NNameAr,
                    MobileNo = cust.MobileNo,
                    Email = cust.Email,
                    UnitId = MapCompanionRowToUiUnitId(row.UnitId),
                    RelationId = row.RelationId
                });
            }

            return list;
        }

        private async Task<IReadOnlyList<ReservationDetailExtraDto>> BuildExtrasListAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var matchIds = GetReservationRateRefs(reservation);
            var rows = await _context.ReservationExtras
                .AsNoTracking()
                .Where(e => matchIds.Contains(e.ReservationId))
                .OrderBy(e => e.ExtraId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Array.Empty<ReservationDetailExtraDto>();
            }

            var unitIds = rows.Where(e => e.UnitId.HasValue).Select(e => e.UnitId!.Value).Distinct().ToList();
            var rus = unitIds.Count == 0
                ? new List<ReservationUnit>()
                : await _context.ReservationUnits.AsNoTracking()
                    .Where(u => matchIds.Contains(u.ReservationId) && unitIds.Contains(u.UnitId))
                    .ToListAsync(cancellationToken);

            var apartmentIds = rus.Select(u => u.ApartmentId).Distinct().ToList();
            var apartments = apartmentIds.Count == 0
                ? new List<Apartment>()
                : await _context.Apartments.AsNoTracking()
                    .Where(a =>
                        a.HotelId == reservation.HotelId &&
                        (apartmentIds.Contains(a.ApartmentId) ||
                         (a.ZaaerId != null && apartmentIds.Contains(a.ZaaerId.Value))))
                    .ToListAsync(cancellationToken);

            var unitsByUnitId = rus.ToDictionary(u => u.UnitId);
            var apartmentsByAnyId = BuildApartmentsByAnyId(apartments);

            string? ResolveRoomLabelFromReservationUnit(int? reservationUnitId)
            {
                if (!reservationUnitId.HasValue)
                {
                    return null;
                }

                if (!unitsByUnitId.TryGetValue(reservationUnitId.Value, out var ru))
                {
                    return $"#{reservationUnitId}";
                }

                apartmentsByAnyId.TryGetValue(ru.ApartmentId, out var apt);
                return apt != null
                    ? $"{apt.ApartmentCode} — {apt.ApartmentName ?? apt.ApartmentCode}"
                    : $"#{ru.ApartmentId}";
            }

            var list = new List<ReservationDetailExtraDto>(rows.Count);
            foreach (var e in rows)
            {
                list.Add(new ReservationDetailExtraDto
                {
                    ExtraId = e.ExtraId,
                    UnitId = e.UnitId,
                    RoomLabel = ResolveRoomLabelFromReservationUnit(e.UnitId),
                    PackageId = e.PackageId,
                    ItemName = e.ItemName,
                    PostingRule = e.PostingRule,
                    ServiceDate = e.ServiceDate,
                    GuestCount = e.GuestCount,
                    NightCount = e.NightCount,
                    UnitPrice = e.UnitPrice,
                    Subtotal = e.Subtotal,
                    TaxAmount = e.TaxAmount,
                    TotalAmount = e.TotalAmount,
                    CreatedBy = e.CreatedBy,
                    CreatedAt = e.CreatedAt
                });
            }

            return list;
        }

        private async Task<int> CountReservationNotesAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var matchIds = GetReservationRateRefs(reservation);
            return await _context.ReservationNotes
                .AsNoTracking()
                .CountAsync(
                    n => n.HotelId == reservation.HotelId && matchIds.Contains(n.ReservationId),
                    cancellationToken);
        }

        private async Task<IReadOnlyList<ReservationDetailDiscountDto>> BuildDiscountsListAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            var matchIds = GetReservationRateRefs(reservation);
            var rows = await _context.Discounts
                .AsNoTracking()
                .Where(d => matchIds.Contains(d.ReservationId) && d.IsActive)
                .OrderBy(d => d.DiscountId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Array.Empty<ReservationDetailDiscountDto>();
            }

            var rus = await _context.ReservationUnits
                .AsNoTracking()
                .Where(u => matchIds.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var apartmentIdHints = rus.Select(u => u.ApartmentId).Distinct().ToList();
            var zaaerHints = rus
                .Where(u => u.ZaaerId is > 0)
                .Select(u => u.ZaaerId!.Value)
                .ToList();
            foreach (var storedUnit in rows.Where(d => d.UnitId.HasValue).Select(d => d.UnitId!.Value))
            {
                zaaerHints.Add(storedUnit);
            }

            var apartments =
                apartmentIdHints.Count == 0 && zaaerHints.Count == 0
                    ? new List<Apartment>()
                    : await _context.Apartments.AsNoTracking()
                        .Where(a =>
                            a.HotelId == reservation.HotelId &&
                            (apartmentIdHints.Contains(a.ApartmentId) ||
                             (a.ZaaerId != null && zaaerHints.Contains(a.ZaaerId.Value))))
                        .ToListAsync(cancellationToken);

            var unitsByStoredOrUiId = BuildReservationUnitsByStoredOrUiId(rus);
            var apartmentsByAnyId = BuildApartmentsByAnyId(apartments);

            var list = new List<ReservationDetailDiscountDto>(rows.Count);
            foreach (var row in rows)
            {
                list.Add(ToReservationDetailDiscountDto(row, unitsByStoredOrUiId, apartmentsByAnyId));
            }

            return list;
        }

        private static string NormalizeExtraPostingRule(string? rule)
        {
            var s = (rule ?? "OnCheckIn").Trim();
            return s.ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        private static decimal ComputeReservationExtraSubtotal(string postingNorm, decimal unitPrice, int guests, int nights)
        {
            guests = Math.Max(1, guests);
            nights = Math.Max(1, nights);
            return postingNorm switch
            {
                "daily" => Math.Round(unitPrice * guests * nights, 2, MidpointRounding.AwayFromZero),
                "perstay" => Math.Round(unitPrice * guests * nights, 2, MidpointRounding.AwayFromZero),
                "oncheckin" or "oncheckout" or "oncustomdate" => Math.Round(unitPrice * guests, 2, MidpointRounding.AwayFromZero),
                _ => Math.Round(unitPrice * guests, 2, MidpointRounding.AwayFromZero)
            };
        }

        private static IReadOnlyList<string> DetectRemovedPosOrderNosFromExtrasPatch(
            IReadOnlyList<ReservationExtra> tracked,
            IReadOnlyList<ReservationPmsExtraPatchDto> newLines)
        {
            var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in newLines)
            {
                if (PosReservationExtraNaming.TryParseOrderNo(line.ItemName, out var orderNo))
                {
                    kept.Add(orderNo);
                }
            }

            var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in tracked)
            {
                if (PosReservationExtraNaming.TryParseOrderNo(extra.ItemName, out var orderNo)
                    && !kept.Contains(orderNo))
                {
                    removed.Add(orderNo);
                }
            }

            return removed.Count == 0 ? Array.Empty<string>() : removed.ToList();
        }

        private async Task ReplaceReservationExtrasFromPmsPatchAsync(
            Reservation entity,
            IReadOnlyList<ReservationPmsExtraPatchDto> lines,
            CancellationToken cancellationToken)
        {
            var matchIds = GetReservationRateRefs(entity);
            var tracked = await _context.ReservationExtras
                .Where(e => matchIds.Contains(e.ReservationId))
                .ToListAsync(cancellationToken);

            var removedPosOrderNos = DetectRemovedPosOrderNosFromExtrasPatch(tracked, lines);
            if (removedPosOrderNos.Count > 0)
            {
                var reservationRouteId = entity.ZaaerId is > 0 ? entity.ZaaerId.Value : entity.ReservationId;
                await _posOrders.CancelTransferredOrdersForRemovedExtrasAsync(
                    removedPosOrderNos,
                    reservationRouteId,
                    cancellationToken);
            }

            if (tracked.Count > 0)
            {
                _context.ReservationExtras.RemoveRange(tracked);
            }

            if (lines.Count == 0)
            {
                return;
            }

            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            var rus = await _context.ReservationUnits
                .Where(u => matchIds.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var storeReservationId = GetReservationExtraStorageReservationId(entity);
            var defaultNights = entity.TotalNights is > 0 ? entity.TotalNights.Value : 1;
            var serviceDateFallback = (entity.CheckInDate ?? DateTime.UtcNow).Date;

            foreach (var line in lines)
            {
                int? unitLineId = null;
                if (line.ReservationUnitId is > 0)
                {
                    var ru = rus.FirstOrDefault(u => u.UnitId == line.ReservationUnitId.Value);
                    unitLineId = ru?.UnitId;
                }

                ReservationPackage? pkg = null;
                if (line.PackageId is > 0)
                {
                    pkg = await _context.ReservationPackages.AsNoTracking()
                        .FirstOrDefaultAsync(
                            p =>
                                p.PackageId == line.PackageId!.Value &&
                                (!p.HotelId.HasValue || p.HotelId == entity.HotelId),
                            cancellationToken);
                }

                var unitPrice = line.UnitPrice ?? pkg?.UnitPrice ?? 0m;
                var itemName = string.IsNullOrWhiteSpace(line.ItemName)
                    ? (pkg?.Name ?? "Extra")
                    : line.ItemName.Trim();
                var posting = string.IsNullOrWhiteSpace(line.PostingRule) ? "OnCheckIn" : line.PostingRule.Trim();
                var postingNorm = NormalizeExtraPostingRule(posting);
                var guests = Math.Max(1, line.GuestCount ?? 1);
                var nights = line.NightCount is > 0 ? line.NightCount!.Value : defaultNights;
                var (subtotal, tax, total) = ComputeReservationExtraFinancialsFromInputs(
                    postingNorm,
                    unitPrice,
                    guests,
                    nights,
                    taxConfig);
                var svcDate = line.ServiceDate?.Date ?? (postingNorm == "oncustomdate" ? serviceDateFallback : (DateTime?)null);

                _context.ReservationExtras.Add(new ReservationExtra
                {
                    ReservationId = storeReservationId,
                    UnitId = unitLineId,
                    PackageId = line.PackageId,
                    ItemName = itemName,
                    PostingRule = posting,
                    ServiceDate = svcDate,
                    GuestCount = guests,
                    NightCount = nights,
                    UnitPrice = unitPrice,
                    Subtotal = subtotal,
                    TaxAmount = tax,
                    TotalAmount = total,
                    CreatedBy = PmsCurrentUser.ResolveUserId(_currentUser),
                    CreatedAt = KsaTime.Now
                });
            }
        }

        private async Task FinalizeReservationTotalsWithExtrasAsync(Reservation entity, CancellationToken cancellationToken)
        {
            await SyncPenaltyAndDiscountHeaderTotalsAsync(entity, cancellationToken);

            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var extraMatchIds = refs;
            var extraLines = await _context.ReservationExtras
                .Where(e => extraMatchIds.Contains(e.ReservationId))
                .ToListAsync(cancellationToken);

            if (units.Count == 0 && extraLines.Count == 0)
            {
                // Clear orphan header.total_extra when all lines are gone (e.g. manual DB delete).
                entity.TotalExtra = 0m;
                return;
            }

            var sumUnitsTotal = units.Sum(u => u.TotalAmount);
            var sumUnitsNet = units.Sum(u => u.RentAmount);
            var sumUnitsEwa = units.Sum(u => u.LodgingTaxAmount ?? 0m);
            var sumUnitsVat = units.Sum(u => u.VatAmount ?? 0m);

            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            decimal sumExtrasNet = 0m;
            decimal sumExtrasEwa = 0m;
            decimal sumExtrasVat = 0m;
            foreach (var e in extraLines)
            {
                var calc = GetReservationExtraAmountsFromPersistedLine(e, taxConfig);
                sumExtrasNet += calc.NetAmount;
                sumExtrasEwa += calc.EwaAmount;
                sumExtrasVat += calc.VatAmount;
            }

            var sumExtrasTotal = Math.Round(extraLines.Sum(e => e.TotalAmount), 2, MidpointRounding.AwayFromZero);
            entity.TotalExtra = sumExtrasTotal;

            entity.Subtotal = Math.Round(sumUnitsNet + sumExtrasNet, 2, MidpointRounding.AwayFromZero);
            entity.LodgingTaxAmount = Math.Round(sumUnitsEwa + sumExtrasEwa, 2, MidpointRounding.AwayFromZero);
            entity.VatAmount = Math.Round(sumUnitsVat + sumExtrasVat, 2, MidpointRounding.AwayFromZero);
            entity.TotalTaxAmount = Math.Round(
                entity.LodgingTaxAmount.GetValueOrDefault() + entity.VatAmount.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
            entity.VatRate = taxConfig.VatRate;
            entity.LodgingTaxRate = taxConfig.EwaRate;

            // total_amount = gross (units + extras + penalties); discounts stay in total_discounts only
            entity.TotalAmount = Math.Round(
                sumUnitsTotal + sumExtrasTotal
                + (entity.TotalPenalties ?? 0m),
                2,
                MidpointRounding.AwayFromZero);

            entity.AmountPaid ??= 0m;
            entity.BalanceAmount = Math.Round(
                entity.TotalAmount.GetValueOrDefault()
                - (entity.TotalDiscounts ?? 0m)
                - entity.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
        }

        private async Task SyncReservationAmountPaidFromLiveReceiptsAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var keys = GetReservationFinancialLinkKeys(entity);
            var receipts = await _context.PaymentReceipts
                .AsNoTracking()
                .Where(pr => pr.ReservationId.HasValue && keys.Contains(pr.ReservationId.Value))
                .ToListAsync(cancellationToken);

            decimal amountPaid = 0m;
            foreach (var pr in receipts)
            {
                if (!ReservationFinancialSyncService.CountsTowardRentPaymentTotals(pr))
                {
                    continue;
                }

                var amt = Math.Abs(pr.AmountPaid);
                if (ReservationFinancialSyncService.IsRentReceiptPayment(pr))
                {
                    amountPaid += amt;
                }
                else
                {
                    amountPaid -= amt;
                }
            }

            entity.AmountPaid = Math.Round(amountPaid, 2, MidpointRounding.AwayFromZero);
            entity.BalanceAmount = Math.Round(
                entity.TotalAmount.GetValueOrDefault()
                - (entity.TotalDiscounts ?? 0m)
                - entity.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
        }

        private async Task ReconcileReservationFinancialsForCheckoutAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            await FinalizeReservationTotalsWithExtrasAsync(entity, cancellationToken);
            await SyncReservationAmountPaidFromLiveReceiptsAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Self-heal reservation money columns when opening detail: OTA / legacy rows may have payments
        /// and unit lines but zero header totals or missing day-rate rows.
        /// </summary>
        private async Task ReconcileReservationFinancialsOnDetailReadAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            var entity = await FindReservationTrackedAsync(id, hotelId, cancellationToken);
            if (entity == null)
            {
                return;
            }

            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            if (units.Count == 0)
            {
                await SyncReservationAmountPaidFromLiveReceiptsAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var headerTotal = entity.TotalAmount ?? 0m;
            var unitsTotal = Math.Round(units.Sum(u => u.TotalAmount), 2, MidpointRounding.AwayFromZero);
            var needsPricingRefresh = units.Any(u => u.TotalAmount <= 0m);
            var needsHeaderRollup = headerTotal <= 0m && unitsTotal > 0m;
            var needsFullReconcile = headerTotal <= 0m || needsPricingRefresh;

            if (!needsFullReconcile)
            {
                await SyncReservationAmountPaidFromLiveReceiptsAsync(entity, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            await TryRefreshFinancialsFromStoredDayRatesAsync(entity, cancellationToken);

            units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            if (units.Any(u => u.TotalAmount <= 0m))
            {
                await ApplyDefaultPricingFromRoomRatesForUnitsWithoutPositiveDayRatesAsync(entity, cancellationToken);
            }

            await FinalizeReservationTotalsWithExtrasAsync(entity, cancellationToken);
            await SyncReservationAmountPaidFromLiveReceiptsAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (needsHeaderRollup || headerTotal <= 0m)
            {
                _logger.LogInformation(
                    "PMS DetailRead: reconciled reservation {ReservationId} financials (header was {HeaderTotal}, units sum {UnitsTotal}).",
                    entity.ReservationId,
                    headerTotal,
                    unitsTotal);
            }
        }

        public async Task<ReservationCheckoutSnapshotDto?> GetCheckoutSnapshotAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            await ReconcileReservationFinancialsForCheckoutAsync(entity, cancellationToken);

            var refs = GetReservationRateRefs(entity);
            var rentTotal = await _context.ReservationUnits
                .AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .SumAsync(u => u.TotalAmount, cancellationToken);

            var coverage = await ReservationInvoiceCoverage.ComputeAsync(
                _context,
                entity.HotelId,
                entity.ReservationId,
                entity.ZaaerId,
                entity.TotalAmount ?? 0m,
                entity.TotalDiscounts ?? 0m,
                entity.AmountPaid ?? 0m,
                cancellationToken);

            var discounts = entity.TotalDiscounts ?? 0m;
            var totalAmount = entity.TotalAmount ?? 0m;

            return new ReservationCheckoutSnapshotDto
            {
                ReservationId = entity.ReservationId,
                ZaaerId = entity.ZaaerId,
                RentTotal = Math.Round(rentTotal, 2, MidpointRounding.AwayFromZero),
                ExtrasTotal = entity.TotalExtra ?? 0m,
                PenaltiesTotal = entity.TotalPenalties ?? 0m,
                DiscountsTotal = discounts,
                TotalAmount = totalAmount,
                AmountPaid = entity.AmountPaid ?? 0m,
                BalanceAmount = entity.BalanceAmount ?? 0m,
                GrossInvoicedTotal = coverage.GrossInvoiced,
                CreditNotesTotal = coverage.CreditNotesTotal,
                DebitNotesTotal = coverage.DebitNotesTotal,
                NetInvoicedTotal = coverage.NetInvoiced,
                InvoicedTotal = coverage.NetInvoiced,
                InvoiceRequiredAmount = coverage.InvoiceRequiredAmount,
                InvoiceRemaining = coverage.InvoiceRemaining
            };
        }

        private static (decimal NetAmount, decimal EwaAmount, decimal VatAmount, decimal Total) GetReservationExtraAmountsFromPersistedLine(
            ReservationExtra e,
            HotelPricingTaxConfig taxConfig)
        {
            var postingNorm = NormalizeExtraPostingRule(e.PostingRule);
            var guests = Math.Max(1, e.GuestCount ?? 1);
            var nights = Math.Max(1, e.NightCount ?? 1);
            var extended = ComputeReservationExtraSubtotal(postingNorm, e.UnitPrice, guests, nights);
            return CalculatePricingAmounts(extended, taxConfig);
        }

        private static (decimal Subtotal, decimal TaxAmount, decimal TotalAmount) ComputeReservationExtraFinancialsFromInputs(
            string postingNorm,
            decimal unitPrice,
            int guests,
            int nights,
            HotelPricingTaxConfig taxConfig)
        {
            var extended = ComputeReservationExtraSubtotal(postingNorm, unitPrice, guests, nights);
            var calc = CalculatePricingAmounts(extended, taxConfig);
            var tax = Math.Round(calc.EwaAmount + calc.VatAmount, 2, MidpointRounding.AwayFromZero);
            return (
                Math.Round(calc.NetAmount, 2, MidpointRounding.AwayFromZero),
                tax,
                Math.Round(calc.Total, 2, MidpointRounding.AwayFromZero));
        }

        public async Task<ReservationDetailDto?> GetByZaaerOrReservationIdAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            await ReconcileReservationFinancialsOnDetailReadAsync(id, hotelId, cancellationToken);

            var reservation = await _context.Reservations
                .AsNoTracking()
                .Where(r =>
                    (r.ZaaerId == id || r.ReservationId == id) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == id ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);

            if (reservation == null)
            {
                return null;
            }

            var resPk = reservation.ReservationId;
            var resZaaer = reservation.ZaaerId;

            var unitsQuery =
                from unit in _context.ReservationUnits.AsNoTracking()
                where unit.ReservationId == resPk || (resZaaer.HasValue && unit.ReservationId == resZaaer.Value)
                from apartment in _context.Apartments.AsNoTracking()
                    .Where(a =>
                        a.HotelId == reservation.HotelId &&
                        (unit.ApartmentId == a.ApartmentId || unit.ApartmentId == a.ZaaerId))
                    .OrderBy(a => unit.ApartmentId == a.ApartmentId ? 0 : 1)
                    .ThenBy(a => a.ApartmentId)
                    .Take(1)
                    .DefaultIfEmpty()
                from roomType in _context.RoomTypes.AsNoTracking()
                    .Where(rt =>
                        apartment != null &&
                        rt.HotelId == apartment.HotelId &&
                        (apartment.RoomTypeId == rt.RoomTypeId || apartment.RoomTypeId == rt.ZaaerId))
                    .OrderBy(rt => apartment != null && apartment.RoomTypeId == rt.RoomTypeId ? 0 : 1)
                    .ThenBy(rt => rt.RoomTypeId)
                    .Take(1)
                    .DefaultIfEmpty()
                from building in _context.Buildings.AsNoTracking()
                    .Where(b =>
                        apartment != null &&
                        b.HotelId == apartment.HotelId &&
                        (apartment.BuildingId == b.BuildingId || apartment.BuildingId == b.ZaaerId))
                    .OrderBy(b => apartment != null && apartment.BuildingId == b.BuildingId ? 0 : 1)
                    .ThenBy(b => b.BuildingId)
                    .Take(1)
                    .DefaultIfEmpty()
                from floor in _context.Floors.AsNoTracking()
                    .Where(f =>
                        apartment != null &&
                        f.HotelId == apartment.HotelId &&
                        (apartment.FloorId == f.FloorId || apartment.FloorId == f.ZaaerId))
                    .OrderBy(f => apartment != null && apartment.FloorId == f.FloorId ? 0 : 1)
                    .ThenBy(f => f.FloorId)
                    .Take(1)
                    .DefaultIfEmpty()
                orderby unit.CheckInDate
                select new ReservationDetailUnitDto
                {
                    UnitId = unit.UnitId,
                    UnitZaaerId = unit.ZaaerId,
                    ApartmentId = apartment != null ? apartment.ApartmentId : null,
                    ApartmentZaaerId = apartment != null ? apartment.ZaaerId : null,
                    ApartmentCode = apartment != null ? apartment.ApartmentCode : null,
                    ApartmentLabel = apartment == null
                        ? unit.ApartmentId.ToString()
                        : $"{apartment.ApartmentCode} — {apartment.ApartmentName ?? apartment.ApartmentCode}",
                    RoomTypeName = roomType != null ? roomType.RoomTypeName : null,
                    BuildingName = building != null ? building.BuildingName : null,
                    FloorName = floor != null ? floor.FloorName : null,
                    CheckInDate = unit.CheckInDate,
                    CheckOutDate = unit.CheckOutDate,
                    DepartureDate = unit.DepartureDate,
                    UnitStatus = unit.Status ?? string.Empty,
                    RentAmount = unit.RentAmount,
                    TotalAmount = unit.TotalAmount,
                    DefaultGrossRate = null
                };

            var unitsRaw = await unitsQuery.ToListAsync(cancellationToken);
            var units = unitsRaw
                .GroupBy(u => u.UnitId)
                .Select(g => g
                    .OrderByDescending(u => !string.IsNullOrWhiteSpace(u.ApartmentLabel))
                    .ThenByDescending(u => !string.IsNullOrWhiteSpace(u.RoomTypeName))
                    .First())
                .OrderBy(u => u.CheckInDate)
                .ToList();

            units = (await EnrichUnitsWithDefaultGrossAsync(
                    units,
                    reservation.HotelId,
                    reservation.RentalType,
                    cancellationToken))
                .ToList();

            var customer = reservation.CustomerId is > 0
                ? await _context.Customers
                    .AsNoTracking()
                    .Where(c =>
                        c.CustomerId == reservation.CustomerId.Value ||
                        c.ZaaerId == reservation.CustomerId.Value)
                    .Select(c => new
                    {
                        c.CustomerId,
                        c.ZaaerId,
                        c.CustomerName,
                        c.MobileNo,
                        c.Email,
                        c.BirthdateGregorian,
                        c.Birthday,
                        c.NId,
                        c.GtypeId,
                        c.Gender
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            Nationality? nationality = null;
            if (customer?.NId is { } nId)
            {
                nationality = await _context.Nationalities.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.NId == nId, cancellationToken);
            }

            CustomerIdentification? primaryId = null;
            if (customer != null)
            {
                var customerRefs = GetCustomerIdentityRefs(customer.CustomerId, customer.ZaaerId);
                primaryId = await _context.CustomerIdentifications
                    .AsNoTracking()
                    .Where(i => customerRefs.Contains(i.CustomerId))
                    .OrderByDescending(i => i.IsActive)
                    .ThenByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.IdentificationId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            IdType? idType = null;
            if (primaryId != null)
            {
                var idTypeLookup = await BuildIdTypeLookupAsync(new[] { primaryId.IdTypeId }, cancellationToken);
                idTypeLookup.TryGetValue(primaryId.IdTypeId, out idType);
            }

            VisitPurpose? visitPurpose = null;
            if (reservation.VisitPurposeId.HasValue)
            {
                visitPurpose = await _context.VisitPurposes.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.VpId == reservation.VisitPurposeId.Value, cancellationToken);
            }

            CorporateCustomer? corporate = null;
            if (reservation.CorporateId.HasValue)
            {
                var corpRef = reservation.CorporateId.Value;
                corporate = await _context.CorporateCustomers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c => c.CorporateId == corpRef || c.ZaaerId == corpRef,
                        cancellationToken);
            }

            var resolvedCorporateId = corporate?.CorporateId ?? reservation.CorporateId;

            var mainGuestName = customer?.CustomerName;
            var actualArrival = reservation.CheckInDate
                ?? units.Select(u => u.CheckInDate).DefaultIfEmpty().Min();

            var guests = customer == null
                ? Array.Empty<ReservationDetailGuestDto>()
                : new[]
                {
                    new ReservationDetailGuestDto
                    {
                        CustomerId = customer.CustomerId,
                        CustomerZaaerId = customer.ZaaerId,
                        IsPrimary = true,
                        CustomerName = customer.CustomerName,
                        IdTypeName = idType?.ItName,
                        IdTypeNameAr = idType?.ItNameAr,
                        IdNumber = primaryId?.IdNumber,
                        BirthDate = customer.BirthdateGregorian ?? customer.Birthday,
                        NationalityName = nationality?.NName,
                        NationalityNameAr = nationality?.NNameAr,
                        MobileNo = customer.MobileNo,
                        Email = customer.Email,
                        Gender = customer.Gender,
                        GtypeId = customer.GtypeId,
                        NationalityId = customer.NId
                    }
                };

            var resolvedCustomerId = customer?.CustomerId ?? reservation.CustomerId;
            int? resolvedCustomerZaaerId = customer?.ZaaerId;

            var hotelCode = await _context.HotelSettings.AsNoTracking()
                .Where(h => h.HotelId == reservation.HotelId)
                .Select(h => h.HotelCode)
                .FirstOrDefaultAsync(cancellationToken);

            var pricingTaxConfig = await GetPricingTaxConfigAsync(reservation.HotelId, cancellationToken);

            var extrasList = await BuildExtrasListAsync(reservation, cancellationToken);
            var discountsList = await BuildDiscountsListAsync(reservation, cancellationToken);
            var notesCount = await CountReservationNotesAsync(reservation, cancellationToken);
            var sumExtrasFromLines = Math.Round(
                extrasList.Sum(e => e.TotalAmount),
                2,
                MidpointRounding.AwayFromZero);

            ReservationPeriodListResponseDto? periods =
                await ReservationPeriodQueries.BuildListAsync(_context, reservation, cancellationToken);

            return new ReservationDetailDto
            {
                ReservationId = reservation.ReservationId,
                ZaaerId = reservation.ZaaerId,
                HotelId = reservation.HotelId,
                HotelCode = string.IsNullOrWhiteSpace(hotelCode) ? null : hotelCode.Trim(),
                CustomerId = resolvedCustomerId,
                CustomerZaaerId = resolvedCustomerZaaerId,
                CorporateId = resolvedCorporateId,
                Header = new ReservationDetailHeaderDto
                {
                    ReservationNo = reservation.ReservationNo,
                    Source = reservation.Source,
                    MainGuestName = mainGuestName,
                    ActualArrival = actualArrival,
                    Status = reservation.Status ?? string.Empty
                },
                General = new ReservationDetailGeneralDto
                {
                    ReservationType = reservation.ReservationType ?? string.Empty,
                    VisitPurposeId = reservation.VisitPurposeId,
                    VisitPurposeName = visitPurpose?.VpName,
                    VisitPurposeNameAr = visitPurpose?.VpNameAr,
                    Source = reservation.Source,
                    CmBookingNo = reservation.CmBookingNo
                },
                Dates = new ReservationDetailDateDto
                {
                    RentalType = reservation.RentalType ?? string.Empty,
                    CheckInDate = reservation.CheckInDate,
                    CheckOutDate = reservation.CheckOutDate,
                    DepartureDate = reservation.DepartureDate,
                    NumberOfMonths = reservation.NumberOfMonths,
                    TotalNights = reservation.TotalNights,
                    MonthlyCalendarMode = reservation.MonthlyCalendarMode,
                    IsAutoExtend = reservation.IsAutoExtend,
                    ReservationDate = reservation.ReservationDate
                },
                Units = units,
                Company = corporate == null
                    ? null
                    : new ReservationDetailCorporateDto
                    {
                        CorporateId = corporate.CorporateId,
                        CorporateZaaerId = corporate.ZaaerId,
                        CorNo = corporate.CorNo,
                        CorporateName = corporate.CorporateName,
                        CorporateNameAr = corporate.CorporateNameAr,
                        Country = corporate.Country,
                        CountryAr = corporate.CountryAr,
                        City = corporate.City,
                        CityAr = corporate.CityAr,
                        PostalCode = corporate.PostalCode,
                        Address = corporate.Address,
                        AddressAr = corporate.AddressAr,
                        VatRegistrationNo = corporate.VatRegistrationNo,
                        CommercialRegistrationNo = corporate.CommercialRegistrationNo,
                        DiscountMethod = corporate.DiscountMethod,
                        DiscountValue = corporate.DiscountValue,
                        CorporatePhone = corporate.CorporatePhone,
                        Email = corporate.Email,
                        ContactPersonName = corporate.ContactPersonName,
                        ContactPersonPhone = corporate.ContactPersonPhone,
                        Notes = corporate.Notes
                    },
                Guests = guests,
                Companions = await BuildCompanionsListAsync(resPk, resZaaer, reservation.HotelId, cancellationToken),
                Extras = extrasList,
                Discounts = discountsList,
                NotesCount = notesCount,
                PricingTax = new ReservationDetailPricingTaxDto
                {
                    VatRate = pricingTaxConfig.VatRate,
                    EwaRate = pricingTaxConfig.EwaRate,
                    VatTaxIncluded = pricingTaxConfig.VatIncluded,
                    LodgingTaxIncluded = pricingTaxConfig.EwaIncluded
                },
                Financial = new ReservationDetailFinancialDto
                {
                    BalanceAmount = reservation.BalanceAmount,
                    TotalAmount = reservation.TotalAmount,
                    AmountPaid = reservation.AmountPaid,
                    Subtotal = reservation.Subtotal,
                    TotalTaxAmount = reservation.TotalTaxAmount,
                    // Use sum of persisted extra lines so UI matches reservation_extras (avoids stale reservations.total_extra).
                    TotalExtra = sumExtrasFromLines,
                    TotalPenalties = reservation.TotalPenalties,
                    TotalDiscounts = reservation.TotalDiscounts
                },
                Periods = periods
            };
        }

        /// <summary>
        /// True when the PATCH payload changes stay length or rental mode so financials may need recomputing from day rates or defaults.
        /// Guest-only / companions / corporate / visit metadata patches must return false so totals are not altered.
        /// </summary>
        private static bool PatchTouchesStayOrPricing(ReservationPmsPatchDto patch)
        {
            if (!string.IsNullOrWhiteSpace(patch.RentalType))
            {
                return true;
            }

            if (patch.CheckInDate.HasValue || patch.CheckOutDate.HasValue)
            {
                return true;
            }

            if (patch.NumberOfMonths.HasValue || patch.TotalNights.HasValue)
            {
                return true;
            }

            if (patch.IsAutoExtend.HasValue)
            {
                return true;
            }

            return false;
        }

        private static int ResolveReservationUnitLinkId(Reservation r) =>
            r.ZaaerId is > 0 ? r.ZaaerId.Value : r.ReservationId;

        private async Task<Apartment?> ResolveApartmentForHotelAsync(
            int hotelId,
            int? apartmentId,
            int? apartmentZaaerId,
            CancellationToken cancellationToken)
        {
            var aptId = apartmentId ?? 0;
            var aptZ = apartmentZaaerId ?? 0;
            if (aptId <= 0 && aptZ <= 0)
            {
                return null;
            }

            return await _context.Apartments.AsNoTracking()
                .Where(a =>
                    a.HotelId == hotelId &&
                    ((aptId > 0 && (a.ApartmentId == aptId || a.ZaaerId == aptId)) ||
                     (aptZ > 0 && (a.ZaaerId == aptZ || a.ApartmentId == aptZ))))
                .OrderBy(a => a.ApartmentId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Replaces <see cref="ReservationUnit"/> rows to match the PMS editor (add / remove / update lines).
        /// Caller runs financial refresh afterward when appropriate.
        /// </summary>
        private sealed record UnitPatchStats(int Removed, int Added);

        private async Task<UnitPatchStats> ReplaceReservationUnitsFromPmsPatchAsync(
            Reservation entity,
            IReadOnlyList<ReservationPmsUnitPatchDto> rows,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
            {
                throw new InvalidOperationException("Reservation units payload cannot be empty.");
            }

            var refs = GetReservationRateRefs(entity);
            var existing = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var apartmentRefsBefore = existing.Select(u => u.ApartmentId).ToList();

            var byId = existing.ToDictionary(u => u.UnitId);
            var keepIds = new HashSet<int>();
            foreach (var dto in rows)
            {
                // Only keep rows that match a real reservation_units.unit_id (clients may send apartment ids by mistake).
                if (dto.UnitId is > 0 && byId.ContainsKey(dto.UnitId.Value))
                {
                    keepIds.Add(dto.UnitId.Value);
                }
            }

            var toRemove = existing.Where(u => !keepIds.Contains(u.UnitId)).ToList();
            if (toRemove.Count > 0)
            {
                var rmIds = toRemove.Select(u => u.UnitId).ToList();
                var rates = await _context.ReservationUnitDayRates
                    .Where(r => rmIds.Contains(r.UnitId))
                    .ToListAsync(cancellationToken);
                _context.ReservationUnitDayRates.RemoveRange(rates);
                _context.ReservationUnits.RemoveRange(toRemove);
            }

            var linkId = ResolveReservationUnitLinkId(entity);
            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            var unitLineStatus = MapReservationHeaderToUnitLineStatus(entity.Status);

            foreach (var dto in rows)
            {
                if (dto.UnitId is > 0 && byId.TryGetValue(dto.UnitId.Value, out var row))
                {
                    if (dto.CheckInDate.HasValue)
                    {
                        row.CheckInDate = KsaTime.ToSaudiTime(dto.CheckInDate.Value);
                    }

                    if (dto.CheckOutDate.HasValue)
                    {
                        row.CheckOutDate = KsaTime.ToSaudiTime(dto.CheckOutDate.Value);
                    }

                    if (dto.DepartureDate.HasValue)
                    {
                        row.DepartureDate = KsaTime.ToSaudiTime(dto.DepartureDate.Value);
                    }
                    else if (dto.CheckOutDate.HasValue)
                    {
                        row.DepartureDate = KsaTime.ToSaudiTime(dto.CheckOutDate.Value);
                    }

                    if (dto.ApartmentId is > 0 || dto.ApartmentZaaerId is > 0)
                    {
                        var prevAptRef = row.ApartmentId;
                        var apt = await ResolveApartmentForHotelAsync(
                            entity.HotelId,
                            dto.ApartmentId,
                            dto.ApartmentZaaerId,
                            cancellationToken);
                        if (apt != null)
                        {
                            var newRef = apt.ZaaerId is > 0 ? apt.ZaaerId.Value : apt.ApartmentId;
                            if (newRef != prevAptRef)
                            {
                                await VacateApartmentLinkRefAsync(entity, prevAptRef, cancellationToken);
                            }

                            row.ApartmentId = newRef;
                        }
                    }

                    var ci = row.CheckInDate;
                    var co = row.CheckOutDate;
                    if (co > ci)
                    {
                        row.NumberOfNights = CountHotelNights(ci, co);
                    }
                }
                else
                {
                    if (dto.UnitId is > 0)
                    {
                        _logger.LogWarning(
                            "PMS Patch units: reservation {ResId} — unknown unit_id {UnitId}; treating as new line when apartment is set.",
                            entity.ReservationId,
                            dto.UnitId);
                    }

                    var apt = await ResolveApartmentForHotelAsync(
                        entity.HotelId,
                        dto.ApartmentId,
                        dto.ApartmentZaaerId,
                        cancellationToken);
                    if (apt == null)
                    {
                        throw new InvalidOperationException(
                            "Each new reservation unit must include a valid apartmentId or apartmentZaaerId.");
                    }

                    var ciSrc = dto.CheckInDate ?? entity.CheckInDate ?? KsaTime.Now.Date;
                    var coSrc = dto.CheckOutDate ?? entity.CheckOutDate ?? ciSrc.AddDays(1);
                    var ci = KsaTime.ToSaudiTime(ciSrc);
                    var co = KsaTime.ToSaudiTime(coSrc);
                    var depSrc = dto.DepartureDate ?? dto.CheckOutDate ?? coSrc;
                    var dep = KsaTime.ToSaudiTime(depSrc);
                    var nights = co > ci ? Math.Max(1, CountHotelNights(ci, co)) : 1;
                    var aptRef = apt.ZaaerId is > 0 ? apt.ZaaerId.Value : apt.ApartmentId;

                    _context.ReservationUnits.Add(new ReservationUnit
                    {
                        ReservationId = linkId,
                        ApartmentId = aptRef,
                        CheckInDate = ci,
                        CheckOutDate = co,
                        DepartureDate = dep,
                        NumberOfNights = nights,
                        RentAmount = 0m,
                        VatRate = taxConfig.VatRate,
                        VatAmount = 0m,
                        LodgingTaxRate = taxConfig.EwaRate,
                        LodgingTaxAmount = 0m,
                        TotalAmount = 0m,
                        Status = unitLineStatus,
                        CreatedAt = KsaTime.Now
                    });
                }
            }

            await VacateApartmentsOrphanedAfterUnitMutationAsync(entity, apartmentRefsBefore, cancellationToken);

            var added = rows.Count(d =>
                !(d.UnitId is > 0 && byId.ContainsKey(d.UnitId.Value)));
            return new UnitPatchStats(toRemove.Count, added);
        }

        public async Task<ReservationDetailDto?> PatchReservationAsync(
            int routeId,
            ReservationPmsPatchDto patch,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            await _permissionGuard.ValidatePatchAsync(entity, patch, cancellationToken);

            var statusBeforePatch = entity.Status;

            if (!string.IsNullOrWhiteSpace(patch.ReservationKind))
            {
                var k = patch.ReservationKind.Trim().ToLowerInvariant();
                if (k is "individual" or "فردي")
                {
                    entity.ReservationType = "individual";
                    entity.CorporateId = null;
                }
                else if (k is "company" or "corporate" or "شركة" or "شركات")
                {
                    entity.ReservationType = "corporate";
                    if (patch.CorporateId.HasValue)
                    {
                        entity.CorporateId = await ResolveCorporateStorageIdAsync(
                            entity.HotelId,
                            patch.CorporateId.Value,
                            cancellationToken);
                    }
                }
            }
            else if (patch.CorporateId.HasValue)
            {
                entity.CorporateId = await ResolveCorporateStorageIdAsync(
                    entity.HotelId,
                    patch.CorporateId.Value,
                    cancellationToken);
                entity.ReservationType = "corporate";
            }

            if (patch.VisitPurposeId.HasValue)
            {
                entity.VisitPurposeId = patch.VisitPurposeId;
            }

            if (patch.Source != null)
            {
                entity.Source = patch.Source;
            }

            if (!string.IsNullOrWhiteSpace(patch.ReservationStatus))
            {
                var raw = patch.ReservationStatus.Trim();
                var norm = raw.ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
                if (norm is "cancelled" or "canceled")
                {
                    throw new InvalidOperationException("reservationDetail.cancelErrUseCancelEndpoint");
                }

                if (norm is "checkin" or "checkedin")
                {
                    entity.Status = "checked_in";
                }
                else if (norm is "checkedout")
                {
                    entity.Status = "checked_out";
                }
                else if (norm is "confirmed")
                {
                    entity.Status = "confirmed";
                }
                else if (norm is "unconfirmed")
                {
                    entity.Status = "unconfirmed";
                }
                else if (norm is "noshow")
                {
                    entity.Status = "no_show";
                }
                else if (Enum.TryParse<ReservationStatus>(raw, true, out var reservationStatus))
                {
                    entity.Status = reservationStatus switch
                    {
                        ReservationStatus.CheckedIn => "checked_in",
                        ReservationStatus.CheckedOut => "checked_out",
                        ReservationStatus.Confirmed => "confirmed",
                        ReservationStatus.Unconfirmed => "unconfirmed",
                        ReservationStatus.Cancelled => "cancelled",
                        ReservationStatus.NoShow => "no_show",
                        _ => reservationStatus.ToString()
                    };
                }
            }

            if (patch.CmBookingNo != null)
            {
                entity.CmBookingNo = string.IsNullOrWhiteSpace(patch.CmBookingNo)
                    ? null
                    : patch.CmBookingNo.Trim();
            }

            if (!string.IsNullOrWhiteSpace(patch.RentalType))
            {
                var incomingRental = NormalizeRentalTypeForStorage(patch.RentalType);
                var currentRental = NormalizeRentalTypeForStorage(entity.RentalType);
                if (!string.Equals(incomingRental, currentRental, StringComparison.OrdinalIgnoreCase))
                {
                    var rentalPeriodRefs = ReservationPeriodStorage.GetReservationStorageRefs(entity);
                    var hasPeriods = await _context.ReservationPeriods
                        .AsNoTracking()
                        .AnyAsync(p => rentalPeriodRefs.Contains(p.ReservationId), cancellationToken);
                    if (hasPeriods)
                    {
                        throw new InvalidOperationException("reservationDetail.periods.useAppendForRentalChange");
                    }

                    entity.RentalType = incomingRental;
                }
            }

            if (patch.CheckInDate.HasValue)
            {
                entity.CheckInDate = KsaTime.ToSaudiTime(patch.CheckInDate.Value);
            }

            if (patch.CheckOutDate.HasValue)
            {
                entity.CheckOutDate = KsaTime.ToSaudiTime(patch.CheckOutDate.Value);
            }

            if (patch.NumberOfMonths.HasValue)
            {
                entity.NumberOfMonths = patch.NumberOfMonths;
            }

            if (patch.TotalNights.HasValue)
            {
                entity.TotalNights = patch.TotalNights;
            }

            if (!string.IsNullOrWhiteSpace(patch.MonthlyCalendarMode))
            {
                entity.MonthlyCalendarMode = NormalizeMonthlyCalendarMode(patch.MonthlyCalendarMode);
            }

            if (patch.IsAutoExtend.HasValue)
            {
                entity.IsAutoExtend = patch.IsAutoExtend;
            }

            if (patch.CustomerId.HasValue)
            {
                var cust = await _context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        c =>
                            c.HotelId == entity.HotelId &&
                            (c.CustomerId == patch.CustomerId.Value || c.ZaaerId == patch.CustomerId.Value),
                        cancellationToken);
                if (cust != null && !PmsCustomerMarkers.IsDraftPlaceholder(cust))
                {
                    entity.CustomerId = cust.ZaaerId is > 0 ? cust.ZaaerId.Value : cust.CustomerId;
                }
                else if (cust == null && patch.CustomerId.Value > 0)
                {
                    entity.CustomerId = patch.CustomerId.Value;
                }
            }

            if (patch.Companions != null)
            {
                var resStoreRefs = new List<int> { entity.ReservationId };
                if (entity.ZaaerId is > 0)
                {
                    resStoreRefs.Add(entity.ZaaerId.Value);
                }

                var existingCompanions = await _context.ReservationCompanions
                    .Where(c => resStoreRefs.Contains(c.ReservationId))
                    .ToListAsync(cancellationToken);
                _context.ReservationCompanions.RemoveRange(existingCompanions);

                var resHotelId = entity.HotelId;
                var candRus = await _context.ReservationUnits.AsNoTracking()
                    .Where(u => resStoreRefs.Contains(u.ReservationId))
                    .ToListAsync(cancellationToken);
                var companionApartmentCache = new Dictionary<int, Apartment?>();
                async Task<Apartment?> ResolveCachedApartmentForRuAsync(ReservationUnit line)
                {
                    if (!companionApartmentCache.TryGetValue(line.ApartmentId, out var apt))
                    {
                        apt = await ResolveApartmentForHotelAsync(resHotelId, line.ApartmentId, null, cancellationToken);
                        companionApartmentCache[line.ApartmentId] = apt;
                    }

                    return apt;
                }

                var sort = 0;
                foreach (var dto in patch.Companions)
                {
                    sort++;
                    if (dto.CustomerId <= 0)
                    {
                        continue;
                    }

                    var companionCustomer = await _context.Customers.AsNoTracking()
                        .FirstOrDefaultAsync(
                            c =>
                                c.HotelId == resHotelId &&
                                (c.CustomerId == dto.CustomerId || c.ZaaerId == dto.CustomerId),
                            cancellationToken);
                    if (companionCustomer == null)
                    {
                        continue;
                    }

                    int? unitId = dto.UnitId;
                    int? apartmentId = null;

                    int? relationId = dto.RelationId;
                    if (relationId.HasValue)
                    {
                        var relOk = await _context.CustomerRelations.AsNoTracking()
                            .AnyAsync(r => r.CrId == relationId.Value, cancellationToken);
                        if (!relOk)
                        {
                            relationId = null;
                        }
                    }

                    int? storedUnitRef = null;
                    ReservationUnit? ru = null;
                    if (unitId.HasValue)
                    {
                        ru = candRus.FirstOrDefault(u => u.UnitId == unitId.Value);
                        if (ru == null)
                        {
                            foreach (var cand in candRus)
                            {
                                var aptC = await ResolveCachedApartmentForRuAsync(cand);
                                if (aptC != null &&
                                    (aptC.ApartmentId == unitId.Value ||
                                     (aptC.ZaaerId is > 0 && aptC.ZaaerId.Value == unitId.Value)))
                                {
                                    ru = cand;
                                    break;
                                }
                            }
                        }
                    }

                    if (ru == null && dto.ApartmentZaaerId is > 0)
                    {
                        var apz = dto.ApartmentZaaerId.Value;
                        foreach (var cand in candRus)
                        {
                            var aptC = await ResolveCachedApartmentForRuAsync(cand);
                            if (aptC != null && ((aptC.ZaaerId is > 0 && aptC.ZaaerId.Value == apz) || aptC.ApartmentId == apz))
                            {
                                ru = cand;
                                break;
                            }
                        }
                    }

                    if (ru != null)
                    {
                        var aptForUnit = await ResolveCachedApartmentForRuAsync(ru);
                        apartmentId = await ResolveInternalApartmentIdAsync(resHotelId, ru.ApartmentId, cancellationToken);
                        if (aptForUnit != null)
                        {
                            storedUnitRef = CompanionStorageUnitIdFromApartment(aptForUnit);
                        }
                    }

                    _context.ReservationCompanions.Add(new ReservationCompanion
                    {
                        ReservationId = CompanionStorageReservationId(entity),
                        CustomerId = CompanionStorageCustomerId(companionCustomer),
                        UnitId = storedUnitRef,
                        ApartmentId = apartmentId,
                        RelationId = relationId,
                        SortOrder = sort,
                        CreatedAt = KsaTime.Now
                    });
                }
            }

            UnitPatchStats? unitPatchStats = null;
            ReservationPmsExtraPatchDto? packageExtraLine = null;
            if (patch.Units != null)
            {
                unitPatchStats = await ReplaceReservationUnitsFromPmsPatchAsync(entity, patch.Units, cancellationToken);
            }

            if (patch.Extras != null)
            {
                packageExtraLine = patch.Extras.FirstOrDefault(e => e.PackageId is > 0);
                await ReplaceReservationExtrasFromPmsPatchAsync(entity, patch.Extras, cancellationToken);
            }

            // Guest / corporate / companions / metadata alone must not re-run default pricing (would shift totals).
            var touchFinancials = PatchTouchesStayOrPricing(patch) || patch.Units != null;
            var periodRefs = ReservationPeriodStorage.GetReservationStorageRefs(entity);
            var hasPricingPeriods = await _context.ReservationPeriods
                .AsNoTracking()
                .AnyAsync(p => periodRefs.Contains(p.ReservationId), cancellationToken);

            if (touchFinancials || hasPricingPeriods)
            {
                if (PatchTouchesStayOrPricing(patch))
                {
                    await SyncReservationUnitsStayDatesFromReservationHeaderAsync(entity, cancellationToken);
                }

                await TryRefreshFinancialsFromStoredDayRatesAsync(entity, cancellationToken);
                // Units with per-night day rates keep TryRefresh amounts; others get room_type_rates defaults.
                if (touchFinancials)
                {
                    await ApplyDefaultPricingFromRoomRatesForUnitsWithoutPositiveDayRatesAsync(entity, cancellationToken);
                }

                await RollUpReservationFinancialsFromUnitsAsync(entity, cancellationToken);
            }

            await FinalizeReservationTotalsWithExtrasAsync(entity, cancellationToken);

            await SyncReservationUnitsStatusWithReservationAsync(entity, cancellationToken);

            await SyncReservationApartmentsOccupancyForHeaderStatusAsync(entity, cancellationToken);

            await MarkReservationApartmentsVacantDirtyAfterCheckoutAsync(entity, cancellationToken);

            await ReservationGuestGuard.EnsureGuestInvariantAsync(entity, _context, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(patch.ReservationStatus))
            {
                var norm = patch.ReservationStatus.Trim().ToLowerInvariant()
                    .Replace(" ", "", StringComparison.Ordinal)
                    .Replace("-", "", StringComparison.Ordinal)
                    .Replace("_", "", StringComparison.Ordinal);
                if (norm is "checkin" or "checkedin" && !IsCheckedInReservationStatus(statusBeforePatch))
                {
                    await ReservationActivityLogHelper.LogReservationAsync(
                        _activityLog,
                        ReservationActivityEvents.ReservationCheckIn,
                        entity,
                        cancellationToken: cancellationToken);
                }
            }

            if (unitPatchStats is { Removed: > 0 })
            {
                for (var i = 0; i < unitPatchStats.Removed; i++)
                {
                    await ReservationActivityLogHelper.LogReservationAsync(
                        _activityLog,
                        ReservationActivityEvents.UnitRemoved,
                        entity,
                        cancellationToken: cancellationToken);
                }
            }

            if (unitPatchStats is { Added: > 0 })
            {
                for (var i = 0; i < unitPatchStats.Added; i++)
                {
                    await ReservationActivityLogHelper.LogReservationAsync(
                        _activityLog,
                        ReservationActivityEvents.UnitAdded,
                        entity,
                        cancellationToken: cancellationToken);
                }
            }

            if (packageExtraLine != null)
            {
                var pkgName = string.IsNullOrWhiteSpace(packageExtraLine.ItemName)
                    ? null
                    : packageExtraLine.ItemName.Trim();
                if (packageExtraLine.PackageId is > 0 && string.IsNullOrWhiteSpace(pkgName))
                {
                    var pkg = await _context.ReservationPackages.AsNoTracking()
                        .FirstOrDefaultAsync(
                            p =>
                                p.PackageId == packageExtraLine.PackageId!.Value &&
                                (!p.HotelId.HasValue || p.HotelId == entity.HotelId),
                            cancellationToken);
                    pkgName = pkg?.Name;
                }

                await ReservationActivityLogHelper.LogReservationAsync(
                    _activityLog,
                    ReservationActivityEvents.PackageAdded,
                    entity,
                    new Dictionary<string, object?>
                    {
                        ["packageName"] = pkgName ?? "Package",
                        ["amount"] = packageExtraLine.UnitPrice
                    },
                    cancellationToken: cancellationToken);
            }

            if (PatchTouchesStayOrPricing(patch))
            {
                await ReservationActivityLogHelper.LogReservationAsync(
                    _activityLog,
                    ReservationActivityEvents.ReservationUpdated,
                    entity,
                    new Dictionary<string, object?> { ["fields"] = "stay_or_pricing" },
                    cancellationToken: cancellationToken);
            }
            else if (patch.Units != null &&
                     (unitPatchStats?.Added ?? 0) == 0 &&
                     (unitPatchStats?.Removed ?? 0) == 0)
            {
                await ReservationActivityLogHelper.LogReservationAsync(
                    _activityLog,
                    ReservationActivityEvents.ReservationUpdated,
                    entity,
                    new Dictionary<string, object?> { ["fields"] = "units" },
                    cancellationToken: cancellationToken);
            }

            await TriggerNtmpSyncAfterPatchAsync(entity, statusBeforePatch, patch, cancellationToken);

            return await GetByZaaerOrReservationIdAsync(entity.ZaaerId ?? entity.ReservationId, hotelId, cancellationToken);
        }

        private async Task TriggerNtmpSyncAfterPatchAsync(
            Reservation entity,
            string? statusBeforePatch,
            ReservationPmsPatchDto patch,
            CancellationToken cancellationToken)
        {
            try
            {
                var before = NormalizeReservationStatusKey(statusBeforePatch);
                var after = NormalizeReservationStatusKey(entity.Status);

                if (after is "cancelled" or "canceled")
                {
                    await _ntmpOrchestrator.SyncCancelAsync(entity.ReservationId, cancellationToken);
                    await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
                    return;
                }

                if (after is "checkedout" && before is not "checkedout")
                {
                    await _ntmpOrchestrator.SyncBookingAsync(
                        entity.ReservationId,
                        NtmpBookingOperation.CheckOut,
                        cancellationToken);
                    await _ntmpOrchestrator.SyncExpenseAsync(entity.ReservationId, cancellationToken);
                    await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
                    return;
                }

                if (after is "checkedin" && before is not "checkedin")
                {
                    await _ntmpOrchestrator.SyncBookingAsync(
                        entity.ReservationId,
                        NtmpBookingOperation.CheckIn,
                        cancellationToken);
                    await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
                    return;
                }

                if (string.IsNullOrWhiteSpace(entity.NtmpTransactionId)
                    && await IsReadyForNtmpBookingSyncAsync(entity, cancellationToken))
                {
                    await _ntmpOrchestrator.SyncBookingAsync(
                        entity.ReservationId,
                        NtmpBookingOperation.Booking,
                        cancellationToken);
                    await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(entity.NtmpTransactionId) && PatchHasNtmpBookingChanges(patch))
                {
                    var operation = after switch
                    {
                        "checkedout" => NtmpBookingOperation.CheckOut,
                        "checkedin" => NtmpBookingOperation.CheckIn,
                        _ => NtmpBookingOperation.Booking
                    };

                    await _ntmpOrchestrator.SyncBookingAsync(entity.ReservationId, operation, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NTMP post-patch sync failed for reservation {ReservationId}", entity.ReservationId);
            }
        }

        private async Task<bool> IsReadyForNtmpBookingSyncAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entity.ReservationNo))
            {
                return false;
            }

            if (!entity.CustomerId.HasValue || entity.CustomerId.Value <= 0)
            {
                return false;
            }

            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(
                    c =>
                        c.CustomerId == entity.CustomerId.Value ||
                        c.ZaaerId == entity.CustomerId.Value,
                    cancellationToken);
            if (customer == null || PmsCustomerMarkers.IsDraftPlaceholder(customer))
            {
                return false;
            }

            var linkId = ResolveReservationUnitLinkId(entity);
            return await _context.ReservationUnits.AsNoTracking()
                .AnyAsync(u => u.ReservationId == linkId, cancellationToken);
        }

        private static bool PatchHasNtmpBookingChanges(ReservationPmsPatchDto patch)
        {
            if (PatchTouchesStayOrPricing(patch))
            {
                return true;
            }

            if (patch.CustomerId.HasValue || patch.CorporateId.HasValue || patch.VisitPurposeId.HasValue)
            {
                return true;
            }

            if (patch.Units != null || patch.Extras != null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(patch.ReservationStatus))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeReservationStatusKey(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            return status.Trim().ToLowerInvariant()
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Replace("_", "", StringComparison.Ordinal);
        }

        private async Task EnsureReservationBalanceAllowsCheckoutAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            await ReconcileReservationFinancialsForCheckoutAsync(entity, cancellationToken);

            var bal = entity.BalanceAmount ?? 0m;
            if (bal <= 0.01m)
            {
                return;
            }

            var keys = GetReservationFinancialLinkKeys(entity);
            var promissoryNotes = await _context.PromissoryNotes
                .AsNoTracking()
                .Where(pn => pn.ReservationId.HasValue && keys.Contains(pn.ReservationId.Value))
                .ToListAsync(cancellationToken);

            var promissoryTotal = ReservationFinancialSyncService.SumActivePromissoryNoteAmounts(promissoryNotes);
            if (ReservationFinancialSyncService.IsBalanceCoveredByPromissoryNotes(bal, promissoryTotal))
            {
                return;
            }

            throw new InvalidOperationException("reservationDetail.checkoutErrBalance");
        }

        /// <summary>
        /// Planned check-out calendar day (KSA) is before today — requires <c>reservations.late_check_out</c> to complete checkout.
        /// </summary>
        private static bool IsPlannedDepartureBeforeKsaToday(Reservation entity)
        {
            var planned = entity.CheckOutDate ?? entity.DepartureDate;
            if (!planned.HasValue)
            {
                return false;
            }

            var plannedDate = KsaTime.ToSaudiTime(planned.Value).Date;
            return plannedDate < KsaTime.Now.Date;
        }

        private async Task EnsureCheckoutDepartureDateAllowedAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            if (!IsPlannedDepartureBeforeKsaToday(entity))
            {
                return;
            }

            await _permissionGuard.EnsureAsync("reservations.late_check_out", cancellationToken);
        }

        private static bool IsReservationUnitLineCheckedIn(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var n = status.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty)
                .Replace("_", string.Empty);
            return n is "checkedin";
        }

        private static bool IsReservationUnitLineCheckedOut(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var n = status.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty)
                .Replace("_", string.Empty);
            return n is "checkedout";
        }

        private static bool IsTerminalUnitLineStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var n = status.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty)
                .Replace("_", string.Empty);
            return n is "checkedout" or "cancelled" or "canceled" or "noshow";
        }

        private async Task PerformFullReservationCheckoutAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var now = KsaTime.Now;

            entity.DepartureDate = now;
            entity.Status = "checked_out";

            var resRefs = GetReservationRateRefs(entity);
            var unitRows = await _context.ReservationUnits
                .Where(u => resRefs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            foreach (var u in unitRows)
            {
                u.Status = "checked_out";
                u.CheckOutDate = now.Date;
                u.DepartureDate = now;
            }

            await MarkReservationApartmentsVacantDirtyAfterCheckoutAsync(entity, cancellationToken);

            await ClearRoomCardColorsForReservationUnitsAsync(entity, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> CheckoutReservationAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return false;
            }

            await EnsureCheckoutDepartureDateAllowedAsync(entity, cancellationToken);
            await EnsureReservationBalanceAllowsCheckoutAsync(entity, cancellationToken);
            await ReservationGuestGuard.EnsureGuestBeforeOperationalActionAsync(entity, _context, cancellationToken);
            await PerformFullReservationCheckoutAsync(entity, cancellationToken);
            await ReservationActivityLogHelper.LogReservationAsync(
                _activityLog,
                ReservationActivityEvents.ReservationCheckOut,
                entity,
                cancellationToken: cancellationToken);

            try
            {
                await _ntmpOrchestrator.SyncBookingAsync(
                    entity.ReservationId,
                    NtmpBookingOperation.CheckOut,
                    cancellationToken);
                await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NTMP checkout sync failed for reservation {ReservationId}", entity.ReservationId);
            }

            return true;
        }

        private async Task EnsureReservationCanBeCancelledAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            if (IsCancelledReservationStatus(entity.Status))
            {
                throw new InvalidOperationException("reservationDetail.cancelErrAlreadyCancelled");
            }

            var keys = GetReservationFinancialLinkKeys(entity);

            var hasActiveReceipt = await _context.PaymentReceipts.AsNoTracking()
                .AnyAsync(
                    pr => pr.HotelId == entity.HotelId
                          && pr.ReservationId.HasValue
                          && keys.Contains(pr.ReservationId.Value)
                          && (pr.ReceiptStatus == null
                              || pr.ReceiptStatus.Trim().ToLower() != "cancelled"),
                    cancellationToken);

            if (hasActiveReceipt)
            {
                throw new InvalidOperationException("reservationDetail.cancelErrActiveFinancials");
            }

            var hasActiveInvoice = await _context.Invoices.AsNoTracking()
                .AnyAsync(
                    inv => inv.HotelId == entity.HotelId
                           && inv.ReservationId.HasValue
                           && keys.Contains(inv.ReservationId.Value)
                           && !IsInvoiceVoidedForReservationCancel(inv.PaymentStatus),
                    cancellationToken);

            if (hasActiveInvoice)
            {
                throw new InvalidOperationException("reservationDetail.cancelErrActiveFinancials");
            }

            var hasActivePromissory = await _context.PromissoryNotes.AsNoTracking()
                .AnyAsync(
                    pn => pn.HotelId == entity.HotelId
                          && pn.ReservationId.HasValue
                          && keys.Contains(pn.ReservationId.Value)
                          && !ReservationFinancialSyncService.IsPromissoryNoteCancelled(pn),
                    cancellationToken);

            if (hasActivePromissory)
            {
                throw new InvalidOperationException("reservationDetail.cancelErrActiveFinancials");
            }
        }

        private static bool IsInvoiceVoidedForReservationCancel(string? paymentStatus)
        {
            var norm = (paymentStatus ?? string.Empty).Trim().ToLowerInvariant();
            return norm is "reversed" or "void" or "voided" or "cancelled" or "canceled";
        }

        private async Task PerformFullReservationCancelAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            entity.Status = "cancelled";

            await SyncReservationUnitsStatusWithReservationAsync(entity, cancellationToken);
            await MarkReservationApartmentsVacantDirtyAfterCheckoutAsync(entity, cancellationToken);
            await ClearRoomCardColorsForReservationUnitsAsync(entity, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<ReservationDetailDto?> CancelReservationAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            await _permissionGuard.EnsureAsync("reservations.cancel", cancellationToken);
            await EnsureReservationCanBeCancelledAsync(entity, cancellationToken);
            await PerformFullReservationCancelAsync(entity, cancellationToken);
            await ReservationActivityLogHelper.LogReservationAsync(
                _activityLog,
                ReservationActivityEvents.ReservationCancelled,
                entity,
                cancellationToken: cancellationToken);

            try
            {
                await _ntmpOrchestrator.SyncCancelAsync(entity.ReservationId, cancellationToken);
                await _ntmpOrchestrator.SyncOccupancyAsync(entity.HotelId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NTMP cancel sync failed for reservation {ReservationId}", entity.ReservationId);
            }

            return await GetByZaaerOrReservationIdAsync(entity.ZaaerId ?? entity.ReservationId, hotelId, cancellationToken);
        }

        public async Task<ReservationDetailDto?> CheckoutReservationUnitAsync(
            int routeId,
            int unitId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var unit = units.FirstOrDefault(u => u.UnitId == unitId);
            if (unit == null)
            {
                throw new InvalidOperationException("reservationDetail.units.unitCheckoutErrNotFound");
            }

            if (!IsReservationUnitLineCheckedIn(unit.Status))
            {
                throw new InvalidOperationException("reservationDetail.units.unitCheckoutErrNotCheckedIn");
            }

            var now = KsaTime.Now;

            await EnsureCheckoutDepartureDateAllowedAsync(entity, cancellationToken);

            if (units.Count <= 1)
            {
                await EnsureReservationBalanceAllowsCheckoutAsync(entity, cancellationToken);
                await PerformFullReservationCheckoutAsync(entity, cancellationToken);
            }
            else
            {
                unit.Status = "checked_out";
                // Keep CheckOutDate for billing/day-rate rows; record actual leave on DepartureDate only.
                unit.DepartureDate = now;

                var internalAptId = await ResolveInternalApartmentIdAsync(entity.HotelId, unit.ApartmentId, cancellationToken);
                if (internalAptId.HasValue)
                {
                    var apt = await _context.Apartments.FirstOrDefaultAsync(
                        a => a.ApartmentId == internalAptId.Value,
                        cancellationToken);
                    if (apt != null)
                    {
                        apt.Status = "vacant";
                        apt.HousekeepingStatus = "dirty";
                    }

                    var boardId = await ResolveApartmentBoardIdAsync(
                        entity.HotelId,
                        unit.ApartmentId,
                        cancellationToken);
                    if (boardId.HasValue)
                    {
                        await ClearRoomCardColorsForApartmentBoardIdsAsync(
                            entity.HotelId,
                            new[] { boardId.Value },
                            cancellationToken);
                    }
                }

                if (units.All(u => IsReservationUnitLineCheckedOut(u.Status)))
                {
                    entity.DepartureDate = now;
                    entity.Status = "checked_out";
                    await MarkReservationApartmentsVacantDirtyAfterCheckoutAsync(entity, cancellationToken);
                    await ClearRoomCardColorsForReservationUnitsAsync(entity, cancellationToken);
                }

                await FinalizeReservationTotalsWithExtrasAsync(entity, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await _activityLog.LogAsync(
                    new ReservationActivityLogEntry
                    {
                        EventKey = ReservationActivityEvents.UnitCheckOut,
                        HotelId = entity.HotelId,
                        ReservationId = entity.ReservationId,
                        ReservationNo = entity.ReservationNo,
                        UnitId = unit.UnitId,
                        IconKey = "runner",
                        Payload = new Dictionary<string, object?>
                        {
                            ["reservationNo"] = entity.ReservationNo,
                            ["unitId"] = unit.UnitId
                        },
                        ZaaerId = entity.ZaaerId
                    },
                    cancellationToken);
            }

            return await GetByZaaerOrReservationIdAsync(entity.ZaaerId ?? entity.ReservationId, hotelId, cancellationToken);
        }

        public async Task<ReservationDetailDto?> ReopenReservationAfterCheckoutAsync(
            int routeId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var entity = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            if (!IsCheckedOutReservationStatus(entity.Status))
            {
                throw new InvalidOperationException("reservationDetail.reopenErrNotCheckedOut");
            }

            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            entity.Status = "checked_in";
            entity.DepartureDate = null;

            foreach (var u in units)
            {
                u.Status = "checked_in";
                u.DepartureDate = null;
                if (entity.CheckOutDate.HasValue)
                {
                    u.CheckOutDate = entity.CheckOutDate.Value.Date;
                }
            }

            await SyncReservationApartmentsOccupancyForHeaderStatusAsync(entity, cancellationToken);
            await MarkReservationApartmentsHousekeepingCleanAfterReopenAsync(entity, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await ReservationActivityLogHelper.LogReservationAsync(
                _activityLog,
                ReservationActivityEvents.ReservationReopened,
                entity,
                cancellationToken: cancellationToken);

            return await GetByZaaerOrReservationIdAsync(entity.ZaaerId ?? entity.ReservationId, hotelId, cancellationToken);
        }

        public async Task<ReservationDetailDto?> SwapReservationUnitAsync(
            int routeId,
            ReservationUnitSwapRequestDto body,
            int? hotelId,
            int? createdByUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(body);

            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken)
                .ConfigureAwait(false);
            if (reservation == null)
            {
                return null;
            }

            var refs = GetReservationRateRefs(reservation);
            var unit = await _context.ReservationUnits.FirstOrDefaultAsync(
                    u => u.UnitId == body.UnitId && refs.Contains(u.ReservationId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (unit == null)
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrUnitNotFound");
            }

            if (!CanSwapReservationUnitSource(unit.Status))
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrSourceNotAllowed");
            }

            var modeRaw = (body.ApplyMode ?? string.Empty).Trim();
            var mode = new[] { "SamePrice", "NewFromToday", "NewForAllDays" }.FirstOrDefault(m =>
                m.Equals(modeRaw, StringComparison.OrdinalIgnoreCase));
            if (mode == null)
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrInvalidApplyMode");
            }

            var fromApt = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.HotelId == reservation.HotelId &&
                         (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (fromApt == null)
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrFromApartmentNotFound");
            }

            var toApt = await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.HotelId == reservation.HotelId &&
                         (a.ApartmentId == body.ToApartmentId || a.ZaaerId == body.ToApartmentId),
                    cancellationToken)
                .ConfigureAwait(false);

            if (toApt == null)
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrTargetNotFound");
            }

            if (!IsVacantApartmentForUnitTransfer(toApt.Status))
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrTargetNotVacant");
            }

            if (await HasActiveMaintenanceForApartmentAsync(toApt, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrTargetNotVacant");
            }

            static int ApartmentLinkId(Apartment a) => a.ZaaerId is > 0 ? a.ZaaerId.Value : a.ApartmentId;

            var fromLink = ApartmentLinkId(fromApt);
            var toLink = ApartmentLinkId(toApt);
            if (fromLink == toLink)
            {
                throw new InvalidOperationException("reservationDetail.units.transferErrSameApartment");
            }

            DateTime? effectiveDate = body.EffectiveDate;
            if (string.Equals(mode, "NewFromToday", StringComparison.OrdinalIgnoreCase) && !effectiveDate.HasValue)
            {
                effectiveDate = KsaTime.Now.Date;
            }

            var pmsUserId = PmsCurrentUser.ResolveUserId(_currentUser, createdByUserId);

            var switchIdentity = await _numberingService.GetNextBusinessIdentityAsync(
                    "reservation_unit_swap",
                    reservation.HotelId,
                    pmsUserId?.ToString() ?? "pms",
                    $"pms-unit-swap:{reservation.ReservationId}:{unit.UnitId}:{Guid.NewGuid():N}",
                    cancellationToken)
                .ConfigureAwait(false);

            var switchLog = new ReservationUnitSwitch
            {
                ReservationId = reservation.ReservationId,
                UnitId = unit.UnitId,
                FromApartmentId = fromLink,
                ToApartmentId = toLink,
                ApplyMode = mode,
                EffectiveDate = effectiveDate,
                Comment = body.Comment,
                CreatedByUserId = pmsUserId,
                ZaaerId = ZaaerIdMapper.ToNullableInt32(switchIdentity.ZaaerId),
                CreatedAt = KsaTime.Now
            };

            _context.ReservationUnitSwitches.Add(switchLog);
            await _numberingService.MarkCommittedAsync(switchIdentity.AuditId, cancellationToken).ConfigureAwait(false);

            await ApplyUnitSwapDayRatePricingAsync(
                    reservation,
                    unit,
                    toApt,
                    mode,
                    effectiveDate,
                    cancellationToken)
                .ConfigureAwait(false);

            await TryRefreshFinancialsFromStoredDayRatesAsync(reservation, cancellationToken).ConfigureAwait(false);

            var oldAptEntity = await _context.Apartments
                .FirstOrDefaultAsync(a => a.ApartmentId == fromApt.ApartmentId, cancellationToken)
                .ConfigureAwait(false);
            if (oldAptEntity != null)
            {
                oldAptEntity.Status = "vacant";
                oldAptEntity.HousekeepingStatus = "dirty";

                var oldBoardId = await ResolveApartmentBoardIdAsync(reservation.HotelId, fromLink, cancellationToken)
                    .ConfigureAwait(false);
                if (oldBoardId.HasValue)
                {
                    await ClearRoomCardColorsForApartmentBoardIdsAsync(
                            reservation.HotelId,
                            new[] { oldBoardId.Value },
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            var toAptEntity = await _context.Apartments
                .FirstOrDefaultAsync(a => a.ApartmentId == toApt.ApartmentId, cancellationToken)
                .ConfigureAwait(false);
            if (toAptEntity != null && IsReservationUnitLineCheckedIn(unit.Status))
            {
                toAptEntity.Status = "rented";
            }

            await SyncReservationApartmentsOccupancyForHeaderStatusAsync(reservation, cancellationToken).ConfigureAwait(false);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return await GetByZaaerOrReservationIdAsync(
                    reservation.ZaaerId ?? reservation.ReservationId,
                    hotelId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<ReservationDiscountApplyResultDto?> ApplyDiscountAsync(
            CreateReservationDiscountDto request,
            CancellationToken cancellationToken = default)
        {
            if (request.ReservationId <= 0)
            {
                throw new ArgumentException("ReservationId is required.");
            }

            var reservation = await FindReservationTrackedAsync(
                request.ReservationId,
                request.HotelId,
                cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var unitIds = NormalizeDiscountUnitIds(request.UnitIds);
            if (IsSelectedUnitsDiscountScope(request.ApplyScope) && unitIds.Count == 0)
            {
                throw new ArgumentException("At least one unit must be selected for a unit-scoped discount.");
            }

            var discount = await CreateDiscountEntityAsync(
                reservation,
                request.ApplyScope,
                request.CalculationMethod,
                request.CalculationValue,
                request.Description,
                unitIds,
                cancellationToken);

            _context.Discounts.Add(discount);
            return await SaveDiscountMutationAsync(reservation, discount, cancellationToken);
        }

        public async Task<ReservationDiscountApplyResultDto?> UpdateDiscountAsync(
            int discountId,
            UpdateReservationDiscountDto request,
            CancellationToken cancellationToken = default)
        {
            if (discountId <= 0)
            {
                throw new ArgumentException("DiscountId is required.");
            }

            if (request.ReservationId <= 0)
            {
                throw new ArgumentException("ReservationId is required.");
            }

            var reservation = await FindReservationTrackedAsync(
                request.ReservationId,
                request.HotelId,
                cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var matchIds = GetReservationRateRefs(reservation);
            var discount = await _context.Discounts
                .FirstOrDefaultAsync(
                    d =>
                        d.DiscountId == discountId &&
                        d.HotelId == reservation.HotelId &&
                        matchIds.Contains(d.ReservationId) &&
                        d.IsActive,
                    cancellationToken);
            if (discount == null)
            {
                return null;
            }

            var unitIds = NormalizeDiscountUnitIds(request.UnitIds);
            if (IsSelectedUnitsDiscountScope(request.ApplyScope) && unitIds.Count == 0)
            {
                throw new ArgumentException("At least one unit must be selected for a unit-scoped discount.");
            }

            var method = NormalizeDiscountCalculationMethod(request.CalculationMethod);
            var applyOn = MapDiscountApplyScope(request.ApplyScope);
            var baseAmount = await GetDiscountCalculationBaseAsync(
                reservation,
                request.ApplyScope,
                unitIds,
                cancellationToken);
            if (baseAmount <= 0)
            {
                throw new InvalidOperationException("Cannot apply a discount when the reservation has no rent total.");
            }

            var value = request.CalculationValue;
            if (value <= 0)
            {
                throw new ArgumentException("Discount value must be greater than zero.");
            }

            var discountAmount = ComputeDiscountAmount(method, value, baseAmount);
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim();

            discount.ApplyOn = applyOn;
            discount.CalculationMethod = method;
            discount.CalculationValue = value;
            discount.DiscountAmount = discountAmount;
            discount.Description = description;
            discount.UnitId = await ResolveDiscountStorageUnitIdAsync(reservation, unitIds, cancellationToken);
            discount.UpdatedAt = KsaTime.Now;

            return await SaveDiscountMutationAsync(reservation, discount, cancellationToken);
        }

        public async Task<ReservationDiscountApplyResultDto?> DeleteDiscountAsync(
            int discountId,
            int reservationRouteId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            if (discountId <= 0)
            {
                throw new ArgumentException("DiscountId is required.");
            }

            var reservation = await FindReservationTrackedAsync(reservationRouteId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var matchIds = GetReservationRateRefs(reservation);
            var discount = await _context.Discounts
                .FirstOrDefaultAsync(
                    d =>
                        d.DiscountId == discountId &&
                        d.HotelId == reservation.HotelId &&
                        matchIds.Contains(d.ReservationId),
                    cancellationToken);
            if (discount == null)
            {
                return null;
            }

            _context.Discounts.Remove(discount);
            await FinalizeReservationTotalsWithExtrasAsync(reservation, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var discountsList = await BuildDiscountsListAsync(reservation, cancellationToken);
            return new ReservationDiscountApplyResultDto
            {
                Discount = ToDiscountLineDto(discount, discountsList),
                Discounts = discountsList,
                Financial = BuildFinancialDto(reservation)
            };
        }

        private async Task<Discount> CreateDiscountEntityAsync(
            Reservation reservation,
            string applyScope,
            string calculationMethod,
            decimal calculationValue,
            string? description,
            IReadOnlyList<int> uiUnitIds,
            CancellationToken cancellationToken)
        {
            var method = NormalizeDiscountCalculationMethod(calculationMethod);
            var applyOn = MapDiscountApplyScope(applyScope);
            var baseAmount = await GetDiscountCalculationBaseAsync(
                reservation,
                applyScope,
                uiUnitIds,
                cancellationToken);
            if (baseAmount <= 0)
            {
                throw new InvalidOperationException("Cannot apply a discount when the reservation has no rent total.");
            }

            if (calculationValue <= 0)
            {
                throw new ArgumentException("Discount value must be greater than zero.");
            }

            var discountAmount = ComputeDiscountAmount(method, calculationValue, baseAmount);
            var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            return new Discount
            {
                HotelId = reservation.HotelId,
                ReservationId = GetDiscountStorageReservationId(reservation),
                UnitId = await ResolveDiscountStorageUnitIdAsync(reservation, uiUnitIds, cancellationToken),
                DiscountType = DiscountTypes.Other,
                Description = desc,
                ApplyOn = applyOn,
                CalculationMethod = method,
                CalculationValue = calculationValue,
                DiscountAmount = discountAmount,
                IsBeforeTax = true,
                AppliedDate = KsaTime.Now,
                IsActive = true,
                CreatedAt = KsaTime.Now
            };
        }

        private async Task<ReservationDiscountApplyResultDto> SaveDiscountMutationAsync(
            Reservation reservation,
            Discount discount,
            CancellationToken cancellationToken)
        {
            await FinalizeReservationTotalsWithExtrasAsync(reservation, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await _activityLog.LogAsync(
                new ReservationActivityLogEntry
                {
                    EventKey = ReservationActivityEvents.DiscountApplied,
                    HotelId = reservation.HotelId,
                    ReservationId = reservation.ReservationId,
                    ReservationNo = reservation.ReservationNo,
                    RefType = "Discount",
                    RefId = discount.DiscountId,
                    AmountTo = discount.DiscountAmount,
                    IconKey = "percent",
                    Payload = new Dictionary<string, object?>
                    {
                        ["reservationNo"] = reservation.ReservationNo,
                        ["amount"] = discount.DiscountAmount,
                        ["scope"] = discount.ApplyOn
                    },
                    ZaaerId = reservation.ZaaerId
                },
                cancellationToken);

            var discountsList = await BuildDiscountsListAsync(reservation, cancellationToken);
            return new ReservationDiscountApplyResultDto
            {
                Discount = ToDiscountLineDto(discount, discountsList),
                Discounts = discountsList,
                Financial = BuildFinancialDto(reservation)
            };
        }

        private static ReservationDetailFinancialDto BuildFinancialDto(Reservation reservation) =>
            new()
            {
                BalanceAmount = reservation.BalanceAmount,
                TotalAmount = reservation.TotalAmount,
                AmountPaid = reservation.AmountPaid,
                Subtotal = reservation.Subtotal,
                TotalTaxAmount = reservation.TotalTaxAmount,
                TotalExtra = reservation.TotalExtra,
                TotalPenalties = reservation.TotalPenalties,
                TotalDiscounts = reservation.TotalDiscounts
            };

        private static decimal ComputeDiscountAmount(string method, decimal value, decimal baseAmount)
        {
            var discountAmount = method == DiscountCalculationMethods.Percentage
                ? Math.Round(baseAmount * value / 100m, 2, MidpointRounding.AwayFromZero)
                : Math.Round(value, 2, MidpointRounding.AwayFromZero);

            if (discountAmount <= 0)
            {
                throw new ArgumentException("Calculated discount amount must be greater than zero.");
            }

            if (discountAmount > baseAmount)
            {
                discountAmount = baseAmount;
            }

            return discountAmount;
        }

        public async Task<ReservationUnitDayRatesResponseDto?> GetUnitDayRatesAsync(
            int routeId,
            int? unitId,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            var reservationRefs = GetDayRateReservationIdRefs(reservation);
            var unitRefs = await BuildUnitRateRefsAsync(reservation, unitId, cancellationToken);
            var taxConfig = await GetPricingTaxConfigAsync(reservation.HotelId, cancellationToken);

            var query = _context.ReservationUnitDayRates
                .AsNoTracking()
                .Where(r => reservationRefs.Contains(r.ReservationId));

            if (unitRefs.Count > 0)
            {
                query = query.Where(r => unitRefs.Contains(r.UnitId));
            }

            var rows = await query
                .OrderBy(r => r.UnitId)
                .ThenBy(r => r.NightDate)
                .ToListAsync(cancellationToken);

            return BuildUnitDayRatesResponse(reservation, unitId, rows, taxConfig);
        }

        public async Task<ReservationUnitDayRatesResponseDto?> SaveUnitDayRatesAsync(
            int routeId,
            ReservationUnitDayRatesSaveRequestDto request,
            int? hotelId,
            CancellationToken cancellationToken = default)
        {
            var reservation = await FindReservationTrackedAsync(routeId, hotelId, cancellationToken);
            if (reservation == null)
            {
                return null;
            }

            await _permissionGuard.ValidateSaveUnitDayRatesAsync(reservation, request, cancellationToken);

            var itemCount = request.Items?.Count ?? 0;
            _logger.LogInformation(
                "PMS SaveUnitDayRates: routeId={RouteId} reservationPk={ResPk} zaaerId={Zaaer} itemCount={Count}",
                routeId,
                reservation.ReservationId,
                reservation.ZaaerId,
                itemCount);

            if (itemCount == 0)
            {
                _logger.LogWarning("PMS SaveUnitDayRates: request.Items is empty — no rows to upsert.");
            }

            var taxConfig = await GetPricingTaxConfigAsync(reservation.HotelId, cancellationToken);
            var reservationRefs = GetDayRateReservationIdRefs(reservation);
            var reservationRateRefs = GetReservationRateRefs(reservation);
            var storedReservationId = GetDayRateStorageReservationId(reservation);
            var unitEntities = await _context.ReservationUnits
                .Where(u => reservationRateRefs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            var apartmentsByUnit = await LoadApartmentsByReservationUnitsAsync(
                reservation,
                unitEntities,
                cancellationToken);
            var unitsByDayRateRef = BuildReservationUnitsByDayRateRef(unitEntities, apartmentsByUnit);
            var unitRefs = BuildUnitRateRefsFromLoadedUnits(request.UnitId, unitsByDayRateRef);

            var existingQuery = _context.ReservationUnitDayRates
                .Where(r => reservationRefs.Contains(r.ReservationId));

            if (unitRefs.Count > 0)
            {
                existingQuery = existingQuery.Where(r => unitRefs.Contains(r.UnitId));
            }

            var existingRows = await existingQuery.ToListAsync(cancellationToken);

            var applied = 0;
            var keptNightsByStorageUnit = new Dictionary<int, (HashSet<DateTime> Nights, HashSet<int> MatchIds)>();
            foreach (var item in request.Items ?? Array.Empty<ReservationUnitDayRateSaveDto>())
            {
                if (!unitsByDayRateRef.TryGetValue(item.UnitId, out var unitEntity))
                {
                    _logger.LogWarning(
                        "PMS SaveUnitDayRates: skipped line — could not map unitId {RequestedUnitId} to reservation_unit (reservationPk={ResPk}).",
                        item.UnitId,
                        reservation.ReservationId);
                    continue;
                }

                apartmentsByUnit.TryGetValue(unitEntity.UnitId, out var apt);
                var storedUnitId = GetDayRateStorageUnitId(unitEntity, apt);
                var unitMatchIds = new HashSet<int>(GetDayRateUnitIdRefs(unitEntity, apt));

                if (!keptNightsByStorageUnit.TryGetValue(storedUnitId, out var kept))
                {
                    kept = (new HashSet<DateTime>(), new HashSet<int>(unitMatchIds));
                    keptNightsByStorageUnit[storedUnitId] = kept;
                }
                else
                {
                    foreach (var id in unitMatchIds)
                    {
                        kept.MatchIds.Add(id);
                    }
                }

                kept.Nights.Add(item.NightDate.Date);
                var existing = item.RateId.HasValue
                    ? existingRows.FirstOrDefault(r => r.RateId == item.RateId.Value)
                    : existingRows.FirstOrDefault(r =>
                        unitMatchIds.Contains(r.UnitId) &&
                        r.NightDate.Date == item.NightDate.Date);

                var calc = CalculatePricingAmounts(item.GrossRate, taxConfig);
                decimal? previousGross = null;
                if (existing == null)
                {
                    existing = new ReservationUnitDayRate
                    {
                        ReservationId = storedReservationId,
                        UnitId = storedUnitId,
                        NightDate = item.NightDate.Date,
                        CreatedAt = KsaTime.Now
                    };
                    _context.ReservationUnitDayRates.Add(existing);
                    existingRows.Add(existing);
                }
                else
                {
                    previousGross = existing.GrossRate;
                }

                existing.ReservationId = storedReservationId;
                existing.UnitId = storedUnitId;

                var newGross = Math.Round(item.GrossRate, 2, MidpointRounding.AwayFromZero);
                existing.GrossRate = newGross;
                existing.EwaAmount = calc.EwaAmount;
                existing.VatAmount = calc.VatAmount;
                existing.NetAmount = calc.NetAmount;
                existing.IsManual = true;
                existing.UpdatedAt = KsaTime.Now;
                applied++;

                if (previousGross.HasValue
                    && Math.Round(previousGross.Value, 2) != newGross)
                {
                    await _activityLog.LogAsync(
                        new ReservationActivityLogEntry
                        {
                            EventKey = ReservationActivityEvents.UnitRateUpdated,
                            HotelId = reservation.HotelId,
                            ReservationId = reservation.ReservationId,
                            ReservationNo = reservation.ReservationNo,
                            UnitId = storedUnitId,
                            RefType = "UnitDayRate",
                            RefId = existing.RateId > 0 ? existing.RateId : null,
                            AmountFrom = previousGross.Value,
                            AmountTo = newGross,
                            IconKey = "edit",
                            Payload = new Dictionary<string, object?>
                            {
                                ["reservationNo"] = reservation.ReservationNo,
                                ["unitId"] = storedUnitId,
                                ["nightDate"] = item.NightDate.Date.ToString("yyyy-MM-dd"),
                                ["amountFrom"] = previousGross.Value,
                                ["amountTo"] = newGross
                            }
                        },
                        cancellationToken);
                }
            }

            if (keptNightsByStorageUnit.Count > 0)
            {
                var staleRows = existingRows
                    .Where(r =>
                        keptNightsByStorageUnit.Any(kv =>
                            kv.Value.MatchIds.Contains(r.UnitId) &&
                            !kv.Value.Nights.Contains(r.NightDate.Date)))
                    .ToList();
                if (staleRows.Count > 0)
                {
                    _context.ReservationUnitDayRates.RemoveRange(staleRows);
                    foreach (var stale in staleRows)
                    {
                        existingRows.Remove(stale);
                    }

                    _logger.LogInformation(
                        "PMS SaveUnitDayRates: removed {Removed} stale day-rate row(s) for reservation {ReservationId}.",
                        staleRows.Count,
                        reservation.ReservationId);
                }
            }

            _logger.LogInformation(
                "PMS SaveUnitDayRates: upserted {Applied} of {Total} requested line(s) before summary save.",
                applied,
                itemCount);

            await _context.SaveChangesAsync(cancellationToken);

            var allRates = await _context.ReservationUnitDayRates
                .Where(r => reservationRefs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);
            ApplyRateSummaryToReservation(reservation, allRates, taxConfig);

            ApplyDayRateRowsToReservationUnits(unitEntities, allRates, taxConfig, apartmentsByUnit);

            await _context.SaveChangesAsync(cancellationToken);

            // Re-finalize with extras so TotalAmount/BalanceAmount include reservation_extras lines,
            // which ApplyRateSummaryToReservation omits (it sums day-rate rows only).
            await FinalizeReservationTotalsWithExtrasAsync(reservation, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var filteredRows = request.UnitId.HasValue
                ? allRates
                    .Where(r => unitRefs.Count == 0 || unitRefs.Contains(r.UnitId))
                    .OrderBy(r => r.UnitId)
                    .ThenBy(r => r.NightDate)
                    .ToList()
                : allRates
                    .OrderBy(r => r.UnitId)
                    .ThenBy(r => r.NightDate)
                    .ToList();

            return BuildUnitDayRatesResponse(reservation, request.UnitId, filteredRows, taxConfig);
        }

        /// <summary>
        /// PMS storage uses lowercase rental_type values (e.g. daily, monthly).
        /// </summary>
        private static string NormalizeRentalTypeForStorage(string? rentalType)
        {
            if (string.IsNullOrWhiteSpace(rentalType))
            {
                return RentalTypeHelper.ToStorageValue(RentalType.Daily);
            }

            if (RentalTypeHelper.TryParseStorage(rentalType, out var parsed))
            {
                return RentalTypeHelper.ToStorageValue(parsed);
            }

            return rentalType.Trim().ToLowerInvariant();
        }

        private static string NormalizeMonthlyCalendarMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return "ThirtyDay";
            }

            return string.Equals(mode.Trim(), "Actual", StringComparison.OrdinalIgnoreCase)
                ? "Actual"
                : "ThirtyDay";
        }

        private static int CountHotelNights(DateTime checkIn, DateTime checkOut)
        {
            if (checkOut <= checkIn)
            {
                return 0;
            }

            var nights = (int)(checkOut.Date - checkIn.Date).TotalDays;
            return Math.Max(1, nights);
        }

        private static string MapReservationHeaderToUnitLineStatus(string? reservationStatus)
        {
            if (string.IsNullOrWhiteSpace(reservationStatus))
            {
                return "confirmed";
            }

            var norm = reservationStatus.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
            return norm switch
            {
                "checkedin" => "checked_in",
                "checkedout" => "checked_out",
                "noshow" => "no_show",
                _ => reservationStatus.Trim().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Aligns persisted unit stay dates with the reservation header after PATCH (so night counts and pricing match the UI).
        /// </summary>
        private async Task SyncReservationUnitsStayDatesFromReservationHeaderAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            if (units.Count == 0)
            {
                return;
            }

            var ci = entity.CheckInDate;
            var co = entity.CheckOutDate;
            foreach (var u in units)
            {
                if (IsTerminalUnitLineStatus(u.Status))
                {
                    continue;
                }

                if (ci.HasValue)
                {
                    u.CheckInDate = ci.Value;
                }

                if (co.HasValue)
                {
                    u.CheckOutDate = co.Value;
                }

                if (!u.DepartureDate.HasValue && co.HasValue)
                {
                    u.DepartureDate = co.Value;
                }

                if (ci.HasValue && co.HasValue && co.Value > ci.Value)
                {
                    u.NumberOfNights = CountHotelNights(ci.Value, co.Value);
                }
            }
        }

        /// <summary>
        /// When <c>reservation_unit_day_rates</c> has positive gross rows, roll totals into <see cref="Reservation"/>
        /// and per-unit rent columns (same rules as <see cref="SaveUnitDayRatesAsync"/>).
        /// </summary>
        private async Task<bool> TryRefreshFinancialsFromStoredDayRatesAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetDayRateReservationIdRefs(entity);
            var rows = await _context.ReservationUnitDayRates.AsNoTracking()
                .Where(r => refs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            if (!rows.Any(r => r.GrossRate > 0m))
            {
                return false;
            }

            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            ApplyRateSummaryToReservation(entity, rows, taxConfig);

            var unitsTracked = await _context.ReservationUnits
                .Where(u => GetReservationRateRefs(entity).Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            var apartmentsByUnit = await LoadApartmentsByReservationUnitsAsync(entity, unitsTracked, cancellationToken);
            ApplyDayRateRowsToReservationUnits(unitsTracked, rows, taxConfig, apartmentsByUnit);

            _logger.LogInformation(
                "PMS Patch: refreshed reservation {ReservationId} financials from {DayRateCount} day-rate row(s).",
                entity.ReservationId,
                rows.Count);

            return true;
        }

        /// <summary>
        /// For each reservation unit that has no positive-gross <c>reservation_unit_day_rates</c> rows, fill
        /// rent/tax/total from <c>room_type_rates</c> (same rules as draft). Units already priced from day rates are left unchanged.
        /// </summary>
        private async Task ApplyDefaultPricingFromRoomRatesForUnitsWithoutPositiveDayRatesAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetDayRateReservationIdRefs(entity);
            var dayRates = await _context.ReservationUnitDayRates.AsNoTracking()
                .Where(r => refs.Contains(r.ReservationId))
                .ToListAsync(cancellationToken);

            var units = await _context.ReservationUnits
                .Where(u => GetReservationRateRefs(entity).Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            if (units.Count == 0)
            {
                return;
            }

            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            var rentalNorm = NormalizeRentalTypeForStorage(entity.RentalType ?? "daily");
            var apartmentsByUnit = await LoadApartmentsByReservationUnitsAsync(entity, units, cancellationToken);

            foreach (var u in units)
            {
                apartmentsByUnit.TryGetValue(u.UnitId, out var aptForUnit);
                if (dayRates.Any(r => DayRateRowMatchesUnit(r, u, aptForUnit) && r.GrossRate > 0m))
                {
                    continue;
                }

                var apt = await _context.Apartments.AsNoTracking()
                    .FirstOrDefaultAsync(
                        a =>
                            a.HotelId == entity.HotelId &&
                            (a.ApartmentId == u.ApartmentId || a.ZaaerId == u.ApartmentId),
                        cancellationToken);
                if (apt == null)
                {
                    _logger.LogWarning(
                        "PMS ApplyDefaultPricing: reservation {ResId} unit_id={UnitId} apartment_id={AptId} — no apartment row for hotel {HotelId}; skipping unit.",
                        entity.ReservationId,
                        u.UnitId,
                        u.ApartmentId,
                        entity.HotelId);
                    continue;
                }

                var checkInForRate = entity.CheckInDate ?? u.CheckInDate;
                var (grossNight, _) = await ResolveDefaultGrossRateFromRoomTypeRatesAsync(
                    apt,
                    entity.HotelId,
                    rentalNorm,
                    checkInForRate.Date,
                    cancellationToken);
                if (grossNight <= 0m)
                {
                    _logger.LogWarning(
                        "PMS ApplyDefaultPricing: reservation {ResId} unit_id={UnitId} apartment_id={AptId} room_type_id={RtId} — room_type_rates returned no gross; skipping unit (draft amounts preserved).",
                        entity.ReservationId,
                        u.UnitId,
                        u.ApartmentId,
                        apt.RoomTypeId);
                    continue;
                }

                var checkIn = entity.CheckInDate ?? u.CheckInDate;
                var checkOut = entity.CheckOutDate ?? u.CheckOutDate;
                var nights = checkOut > checkIn ? Math.Max(1, CountHotelNights(checkIn, checkOut)) : 1;
                var scale = rentalNorm == "monthly"
                    ? (entity.NumberOfMonths is > 0 ? entity.NumberOfMonths.Value : 1)
                    : nights;
                var calc = CalculatePricingAmounts(grossNight, taxConfig);
                var net = Math.Round(calc.NetAmount * scale, 2, MidpointRounding.AwayFromZero);
                var ewa = Math.Round(calc.EwaAmount * scale, 2, MidpointRounding.AwayFromZero);
                var vat = Math.Round(calc.VatAmount * scale, 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(calc.Total * scale, 2, MidpointRounding.AwayFromZero);

                u.RentAmount = net;
                u.VatRate = taxConfig.VatRate;
                u.LodgingTaxRate = taxConfig.EwaRate;
                u.VatAmount = vat;
                u.LodgingTaxAmount = ewa;
                u.TotalAmount = total;
                u.NumberOfNights = nights;
            }
        }

        /// <summary>
        /// Sets reservation header financial columns from the sum of all linked <see cref="ReservationUnit"/> rows.
        /// </summary>
        private async Task RollUpReservationFinancialsFromUnitsAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(entity);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);
            if (units.Count == 0)
            {
                return;
            }

            var sumNet = units.Sum(u => u.RentAmount);
            var sumEwa = units.Sum(u => u.LodgingTaxAmount ?? 0m);
            var sumVat = units.Sum(u => u.VatAmount ?? 0m);
            var sumTotal = units.Sum(u => u.TotalAmount);

            if (sumTotal <= 0m)
            {
                _logger.LogWarning(
                    "PMS RollUpFinancials: reservation {ResId} hotel {HotelId} — sum of unit totals is 0; leaving reservation money columns as-is.",
                    entity.ReservationId,
                    entity.HotelId);
                return;
            }

            var taxConfig = await GetPricingTaxConfigAsync(entity.HotelId, cancellationToken);
            entity.Subtotal = Math.Round(sumNet, 2, MidpointRounding.AwayFromZero);
            entity.VatRate = taxConfig.VatRate;
            entity.LodgingTaxRate = taxConfig.EwaRate;
            entity.VatAmount = Math.Round(sumVat, 2, MidpointRounding.AwayFromZero);
            entity.LodgingTaxAmount = Math.Round(sumEwa, 2, MidpointRounding.AwayFromZero);
            entity.TotalTaxAmount = Math.Round(sumEwa + sumVat, 2, MidpointRounding.AwayFromZero);
            entity.TotalAmount = Math.Round(sumTotal, 2, MidpointRounding.AwayFromZero);
            entity.AmountPaid ??= 0m;
            entity.BalanceAmount = Math.Round(
                entity.TotalAmount.GetValueOrDefault() - entity.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
        }

        private async Task SyncReservationUnitsStatusWithReservationAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(entity);
            var targetStatus = MapReservationHeaderToUnitLineStatus(entity.Status);
            var units = await _context.ReservationUnits
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            foreach (var u in units)
            {
                if (IsTerminalUnitLineStatus(u.Status))
                {
                    continue;
                }

                u.Status = targetStatus;
            }
        }

        /// <summary>
        /// After a unit swap, optionally re-price day-rate rows per <paramref name="applyMode"/>.
        /// SamePrice keeps historical gross amounts; NewFromToday / NewForAllDays apply the target room list price.
        /// </summary>
        private async Task ApplyUnitSwapDayRatePricingAsync(
            Reservation reservation,
            ReservationUnit unit,
            Apartment toApartment,
            string applyMode,
            DateTime? effectiveDate,
            CancellationToken cancellationToken)
        {
            if (string.Equals(applyMode, "SamePrice", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var rentalNorm = NormalizeRentalTypeForStorage(reservation.RentalType ?? "daily");
            var rateDate = effectiveDate?.Date ?? KsaTime.Now.Date;
            var (newGross, _) = await ResolveDefaultGrossRateFromRoomTypeRatesAsync(
                    toApartment,
                    reservation.HotelId,
                    rentalNorm,
                    rateDate,
                    cancellationToken)
                .ConfigureAwait(false);

            if (newGross <= 0m)
            {
                _logger.LogWarning(
                    "PMS UnitSwap: reservation {ResId} unit {UnitId} — no gross rate for target apartment {AptId}; keeping existing day rates.",
                    reservation.ReservationId,
                    unit.UnitId,
                    toApartment.ApartmentId);
                return;
            }

            var taxConfig = await GetPricingTaxConfigAsync(reservation.HotelId, cancellationToken).ConfigureAwait(false);
            var reservationRefs = GetDayRateReservationIdRefs(reservation);
            var matchIds = new HashSet<int>(GetDayRateUnitIdRefs(unit, toApartment));

            var rows = await _context.ReservationUnitDayRates
                .Where(r => reservationRefs.Contains(r.ReservationId) && matchIds.Contains(r.UnitId))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rows.Count == 0)
            {
                return;
            }

            var applyAll = string.Equals(applyMode, "NewForAllDays", StringComparison.OrdinalIgnoreCase);
            var effDate = (effectiveDate ?? KsaTime.Now).Date;
            var roundedGross = Math.Round(newGross, 2, MidpointRounding.AwayFromZero);
            var now = KsaTime.Now;

            foreach (var row in rows)
            {
                if (!applyAll && row.NightDate.Date < effDate)
                {
                    continue;
                }

                row.GrossRate = roundedGross;
                var calc = CalculatePricingAmounts(row.GrossRate, taxConfig);
                row.EwaAmount = calc.EwaAmount;
                row.VatAmount = calc.VatAmount;
                row.NetAmount = calc.NetAmount;
                row.UpdatedAt = now;
            }
        }

        /// <summary>
        /// Default gross (tax-inclusive list price) from <c>room_type_rates</c> for the apartment's room type:
        /// <c>daily_rate_min</c> when rental is daily, <c>monthly_rate_min</c> when monthly (with fallbacks).
        /// </summary>
        private async Task<(decimal Gross, string SourceCode)> ResolveDefaultGrossRateFromRoomTypeRatesAsync(
            Apartment apartment,
            int hotelId,
            string rentalTypeNormalized,
            DateTime? rateDate,
            CancellationToken cancellationToken)
        {
            var (rateKeys, internalRoomTypeId) = await RoomTypeRateQueryHelper.BuildRateKeysForApartmentAsync(
                _context,
                apartment,
                hotelId,
                cancellationToken);

            if (rateKeys.Count == 0)
            {
                return (0m, RoomTypeGrossRateSourceCodes.None);
            }

            var rates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == hotelId)
                .ToListAsync(cancellationToken);
            var rate = rates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));
            RoomType? roomType = null;
            if (internalRoomTypeId.HasValue)
            {
                roomType = await _context.RoomTypes.AsNoTracking()
                    .FirstOrDefaultAsync(rt => rt.RoomTypeId == internalRoomTypeId.Value, cancellationToken);
            }

            var (gross, source) = await RoomTypeGrossRateResolver.ResolveAsync(
                _context,
                hotelId,
                null,
                rateKeys,
                rate,
                roomType,
                rentalTypeNormalized,
                rateDate,
                RoomTypeGrossRateOptions.Standard,
                cancellationToken);

            return (gross, RoomTypeGrossRateSourceCodes.ToCode(source));
        }

        private static (HashSet<int> RateKeys, RoomType? RoomTypeFallback) BuildRateKeysForLoadedApartment(
            Apartment apartment,
            IReadOnlyList<RoomType> roomTypes)
        {
            var rateKeys = new HashSet<int>();
            if (!apartment.RoomTypeId.HasValue || apartment.RoomTypeId.Value <= 0)
            {
                return (rateKeys, null);
            }

            var apartmentRoomTypeRef = apartment.RoomTypeId.Value;
            rateKeys.Add(apartmentRoomTypeRef);

            RoomType? fallback = null;
            foreach (var roomType in roomTypes
                         .Where(rt => rt.RoomTypeId == apartmentRoomTypeRef || rt.ZaaerId == apartmentRoomTypeRef)
                         .OrderBy(rt => rt.RoomTypeId == apartmentRoomTypeRef ? 0 : 1)
                         .ThenBy(rt => rt.RoomTypeId))
            {
                fallback ??= roomType;
                rateKeys.Add(roomType.RoomTypeId);
                if (roomType.ZaaerId is > 0)
                {
                    rateKeys.Add(roomType.ZaaerId.Value);
                }
            }

            return (rateKeys, fallback);
        }

        private async Task<IReadOnlyList<ReservationDetailUnitDto>> EnrichUnitsWithDefaultGrossAsync(
            IReadOnlyList<ReservationDetailUnitDto> units,
            int hotelId,
            string? rentalTypeRaw,
            CancellationToken cancellationToken)
        {
            if (units.Count == 0)
            {
                return units;
            }

            var rentalNorm = NormalizeRentalTypeForStorage(
                string.IsNullOrWhiteSpace(rentalTypeRaw) ? nameof(RentalType.Daily) : rentalTypeRaw);

            var keys = new HashSet<int>();
            foreach (var u in units)
            {
                if (u.ApartmentId is > 0)
                {
                    keys.Add(u.ApartmentId.Value);
                }

                if (u.ApartmentZaaerId is > 0)
                {
                    keys.Add(u.ApartmentZaaerId.Value);
                }
            }

            if (keys.Count == 0)
            {
                return units;
            }

            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a =>
                    a.HotelId == hotelId &&
                    (keys.Contains(a.ApartmentId) || (a.ZaaerId != null && keys.Contains(a.ZaaerId.Value))))
                .ToListAsync(cancellationToken);

            if (apartments.Count == 0)
            {
                return units;
            }

            var roomTypeRefs = apartments
                .Where(a => a.RoomTypeId is > 0)
                .Select(a => a.RoomTypeId!.Value)
                .Distinct()
                .ToList();
            var roomTypes = roomTypeRefs.Count == 0
                ? new List<RoomType>()
                : await _context.RoomTypes.AsNoTracking()
                    .Where(rt =>
                        rt.HotelId == hotelId &&
                        (roomTypeRefs.Contains(rt.RoomTypeId) ||
                         (rt.ZaaerId.HasValue && roomTypeRefs.Contains(rt.ZaaerId.Value))))
                    .ToListAsync(cancellationToken);
            var roomTypeRates = await _context.RoomTypeRates.AsNoTracking()
                .Where(r => r.HotelId == hotelId)
                .ToListAsync(cancellationToken);

            var grossByApartmentPk = new Dictionary<int, (decimal Gross, string Source)>();
            foreach (var apt in apartments)
            {
                if (grossByApartmentPk.ContainsKey(apt.ApartmentId))
                {
                    continue;
                }

                var (rateKeys, roomTypeFallback) = BuildRateKeysForLoadedApartment(apt, roomTypes);
                if (rateKeys.Count == 0)
                {
                    grossByApartmentPk[apt.ApartmentId] = (0m, RoomTypeGrossRateSourceCodes.None);
                    continue;
                }

                var rate = roomTypeRates.FirstOrDefault(r => RoomTypeRateResolver.RateMatchesRoomType(r, rateKeys));
                var (gross, source) = await RoomTypeGrossRateResolver.ResolveAsync(
                    _context,
                    hotelId,
                    null,
                    rateKeys,
                    rate,
                    roomTypeFallback,
                    rentalNorm,
                    null,
                    RoomTypeGrossRateOptions.Standard,
                    cancellationToken);
                var resolved = (gross, RoomTypeGrossRateSourceCodes.ToCode(source));
                grossByApartmentPk[apt.ApartmentId] = resolved;
            }

            var apartmentsByAnyId = new Dictionary<int, Apartment>();
            foreach (var apartment in apartments)
            {
                apartmentsByAnyId.TryAdd(apartment.ApartmentId, apartment);
                if (apartment.ZaaerId is > 0)
                {
                    apartmentsByAnyId.TryAdd(apartment.ZaaerId.Value, apartment);
                }
            }

            Apartment? MatchApartment(ReservationDetailUnitDto u)
            {
                if (u.ApartmentId is > 0)
                {
                    if (apartmentsByAnyId.TryGetValue(u.ApartmentId.Value, out var byApartmentId))
                    {
                        return byApartmentId;
                    }
                }

                if (u.ApartmentZaaerId is > 0)
                {
                    apartmentsByAnyId.TryGetValue(u.ApartmentZaaerId.Value, out var byZaaerId);
                    return byZaaerId;
                }

                return null;
            }

            return units.Select(u =>
            {
                var apt = MatchApartment(u);
                var gross = 0m;
                var source = (string?)null;
                if (apt != null && grossByApartmentPk.TryGetValue(apt.ApartmentId, out var resolved))
                {
                    gross = resolved.Gross;
                    source = resolved.Source;
                }

                return new ReservationDetailUnitDto
                {
                    UnitId = u.UnitId,
                    UnitZaaerId = u.UnitZaaerId,
                    ApartmentId = u.ApartmentId,
                    ApartmentZaaerId = u.ApartmentZaaerId,
                    ApartmentCode = u.ApartmentCode,
                    ApartmentLabel = u.ApartmentLabel,
                    RoomTypeName = u.RoomTypeName,
                    BuildingName = u.BuildingName,
                    FloorName = u.FloorName,
                    CheckInDate = u.CheckInDate,
                    CheckOutDate = u.CheckOutDate,
                    DepartureDate = u.DepartureDate,
                    UnitStatus = u.UnitStatus,
                    DefaultGrossRate = apt != null ? gross : u.DefaultGrossRate,
                    DefaultGrossRateSource = apt != null ? source : u.DefaultGrossRateSource
                };
            }).ToList();
        }

        public async Task<ReservationDetailDto?> CreateReservationAsync(
            ReservationCreateDto body,
            CancellationToken cancellationToken = default)
        {
            if (body.ApartmentId <= 0)
            {
                return null;
            }

            var apartment = await _context.Apartments
                .Where(a => a.ZaaerId == body.ApartmentId || a.ApartmentId == body.ApartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (apartment == null)
            {
                return null;
            }

            var hotelId = apartment.HotelId;
            await ReservationGuestGuard.EnsureCreatePayloadHasGuestAsync(body, _context, hotelId, cancellationToken);

            var customerStorageId = await ReservationGuestGuard.RequireResolvedCustomerStorageIdAsync(
                hotelId,
                body.CustomerId!.Value,
                _context,
                cancellationToken);

            var createdReservation = await CreateReservationShellAsync(
                apartment,
                customerStorageId,
                cancellationToken);

            return await PatchReservationAsync(
                createdReservation.ZaaerId ?? createdReservation.ReservationId,
                (ReservationPmsPatchDto)body,
                hotelId,
                cancellationToken);
        }

        /// <summary>
        /// Minimal reservation + one unit row with a required guest (first step of create flow; PATCH completes editor state).
        /// </summary>
        private async Task<Reservation> CreateReservationShellAsync(
            Apartment apartment,
            int customerStorageId,
            CancellationToken cancellationToken)
        {
            var hotelId = apartment.HotelId;

            var taxConfig = await GetPricingTaxConfigAsync(hotelId, cancellationToken);
            var rentalNorm = NormalizeRentalTypeForStorage(nameof(RentalType.Daily));
            var (grossPerNight, _) = await ResolveDefaultGrossRateFromRoomTypeRatesAsync(
                apartment,
                hotelId,
                rentalNorm,
                KsaTime.Now.Date,
                cancellationToken);
            if (grossPerNight <= 0m)
            {
                _logger.LogWarning(
                    "PMS CreateReservation: apartment_id={AptId} hotel_id={HotelId} — room_type_rates has no daily gross; financials will be zero until PATCH or day rates.",
                    apartment.ApartmentId,
                    hotelId);
            }

            var checkIn = KsaTime.Now.Date;
            var checkOut = checkIn.AddDays(1).AddHours(18);
            var nights = CountHotelNights(checkIn, checkOut);
            var calc = CalculatePricingAmounts(grossPerNight, taxConfig);
            var net = Math.Round(calc.NetAmount * nights, 2, MidpointRounding.AwayFromZero);
            var ewa = Math.Round(calc.EwaAmount * nights, 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(calc.VatAmount * nights, 2, MidpointRounding.AwayFromZero);
            var total = Math.Round(calc.Total * nights, 2, MidpointRounding.AwayFromZero);
            var taxSum = Math.Round(ewa + vat, 2, MidpointRounding.AwayFromZero);
            var apartmentStoredId = apartment.ZaaerId is > 0 ? apartment.ZaaerId.Value : apartment.ApartmentId;
            var unitLineStatus = nameof(ReservationUnitStatus.Reserved);

            long? numberingAuditId = null;
            Reservation? createdReservation = null;
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "reservation",
                    hotelId,
                    null,
                    $"pms-reservation:{hotelId}:{Guid.NewGuid():N}",
                    cancellationToken);
                numberingAuditId = identity.AuditId;

                var zaaerIdInt = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId);
                var reservationLinkId = zaaerIdInt ?? 0;

                var reservation = new Reservation
                {
                    HotelId = hotelId,
                    CustomerId = customerStorageId,
                    ReservationNo = identity.DocumentNo,
                    ZaaerId = zaaerIdInt,
                    ExternalRefNo = zaaerIdInt,
                    ReservationType = "individual",
                    RentalType = NormalizeRentalTypeForStorage(nameof(RentalType.Daily)),
                    Status = "unconfirmed",
                    ReservationDate = KsaTime.Now,
                    CheckInDate = checkIn,
                    CheckOutDate = checkOut,
                    TotalPenalties = 0m,
                    TotalDiscounts = 0m,
                    TotalExtra = 0m,
                    Subtotal = net,
                    VatRate = taxConfig.VatRate,
                    VatAmount = vat,
                    LodgingTaxRate = taxConfig.EwaRate,
                    LodgingTaxAmount = ewa,
                    TotalTaxAmount = taxSum,
                    TotalAmount = total,
                    AmountPaid = 0m,
                    BalanceAmount = total,
                    NtmpSyncedStages = 0,
                    CreatedBy = PmsCurrentUser.ResolveUserId(_currentUser),
                    CreatedAt = KsaTime.Now
                };

                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync(cancellationToken);
                createdReservation = reservation;

                if (reservationLinkId <= 0)
                {
                    reservationLinkId = reservation.ReservationId;
                }

                var unit = new ReservationUnit
                {
                    ReservationId = reservationLinkId,
                    ApartmentId = apartmentStoredId,
                    CheckInDate = checkIn,
                    CheckOutDate = checkOut,
                    DepartureDate = checkOut,
                    NumberOfNights = nights,
                    RentAmount = net,
                    VatRate = taxConfig.VatRate,
                    VatAmount = vat,
                    LodgingTaxRate = taxConfig.EwaRate,
                    LodgingTaxAmount = ewa,
                    TotalAmount = total,
                    Status = unitLineStatus,
                    CreatedAt = KsaTime.Now
                };

                _context.ReservationUnits.Add(unit);
                await _context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (numberingAuditId.HasValue)
                {
                    await _numberingService.MarkVoidedAsync(numberingAuditId.Value, ex.Message, cancellationToken);
                }

                throw;
            }

            if (numberingAuditId.HasValue)
            {
                await _numberingService.MarkCommittedAsync(numberingAuditId.Value, cancellationToken);
            }

            if (createdReservation == null)
            {
                throw new InvalidOperationException("Reservation create did not produce an entity.");
            }

            await ReservationActivityLogHelper.LogReservationAsync(
                _activityLog,
                ReservationActivityEvents.ReservationCreated,
                createdReservation,
                cancellationToken: cancellationToken);

            return createdReservation;
        }

        private Task<Reservation?> FindReservationTrackedAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            return _context.Reservations
                .Where(r =>
                    (r.ZaaerId == id || r.ReservationId == id) &&
                    (!hotelId.HasValue || r.HotelId == hotelId.Value))
                .OrderByDescending(r => r.ZaaerId == id ? 1 : 0)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static IReadOnlyList<int> GetReservationRateRefs(Reservation reservation)
        {
            var refs = new List<int> { reservation.ReservationId };
            if (reservation.ZaaerId.HasValue && reservation.ZaaerId.Value != reservation.ReservationId)
            {
                refs.Add(reservation.ZaaerId.Value);
            }

            return refs;
        }

        private async Task SyncPenaltyAndDiscountHeaderTotalsAsync(
            Reservation entity,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(entity);
            var penalties = await _context.Penalties
                .Where(p => refs.Contains(p.ReservationId) && p.IsActive)
                .SumAsync(p => (decimal?)p.TotalAmount, cancellationToken);
            var discounts = await _context.Discounts
                .Where(d => refs.Contains(d.ReservationId) && d.IsActive)
                .SumAsync(d => (decimal?)d.DiscountAmount, cancellationToken);

            entity.TotalPenalties = Math.Round(penalties ?? 0m, 2, MidpointRounding.AwayFromZero);
            entity.TotalDiscounts = Math.Round(discounts ?? 0m, 2, MidpointRounding.AwayFromZero);
        }

        private static IReadOnlyList<int> NormalizeDiscountUnitIds(IReadOnlyList<int>? unitIds)
        {
            if (unitIds == null || unitIds.Count == 0)
            {
                return Array.Empty<int>();
            }

            return unitIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private async Task<decimal> GetDiscountCalculationBaseAsync(
            Reservation reservation,
            string applyScope,
            IReadOnlyList<int> unitIds,
            CancellationToken cancellationToken)
        {
            var refs = GetReservationRateRefs(reservation);
            var units = await _context.ReservationUnits
                .AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            if (IsSelectedUnitsDiscountScope(applyScope))
            {
                var selected = units.Where(u => unitIds.Contains(u.UnitId)).ToList();
                if (selected.Count == 0)
                {
                    return 0m;
                }

                return Math.Round(selected.Sum(u => u.TotalAmount), 2, MidpointRounding.AwayFromZero);
            }

            var unitsTotal = units.Sum(u => u.TotalAmount);

            var extrasTotal = await _context.ReservationExtras
                .AsNoTracking()
                .Where(e => refs.Contains(e.ReservationId))
                .SumAsync(e => (decimal?)e.TotalAmount, cancellationToken);

            return Math.Round(unitsTotal + (extrasTotal ?? 0m), 2, MidpointRounding.AwayFromZero);
        }

        private static bool IsSelectedUnitsDiscountScope(string applyScope)
        {
            var x = (applyScope ?? string.Empty).Trim().ToLowerInvariant();
            return x is "selectedunits" or "selected_units" or "units" or "rent";
        }

        private static string MapDiscountApplyScope(string applyScope)
        {
            return IsSelectedUnitsDiscountScope(applyScope)
                ? DiscountApplyOn.Rent
                : DiscountApplyOn.Total;
        }

        private static string NormalizeDiscountCalculationMethod(string method)
        {
            var x = (method ?? string.Empty).Trim().ToLowerInvariant();
            return x is "percentage" or "percent" or "%"
                ? DiscountCalculationMethods.Percentage
                : DiscountCalculationMethods.Amount;
        }

        private static int GetDiscountStorageReservationId(Reservation reservation) =>
            GetReservationExtraStorageReservationId(reservation);

        private static int? GetDiscountStorageUnitId(ReservationUnit unit) =>
            unit.ZaaerId is > 0 ? unit.ZaaerId.Value : unit.UnitId;

        /// <summary>Stored on <c>reservation_unit_day_rates.reservation_id</c> (global Zaaer id when set).</summary>
        private static int GetDayRateStorageReservationId(Reservation reservation) =>
            GetReservationExtraStorageReservationId(reservation);

        /// <summary>
        /// Stored on <c>reservation_unit_day_rates.unit_id</c> — global <b>apartment</b> key (not <c>reservation_units.unit_id</c>).
        /// Resolved from <c>reservation_units.apartment_id</c> → <c>apartments</c> (same rule as <see cref="CompanionStorageUnitIdFromApartment"/>).
        /// </summary>
        private static int GetDayRateStorageUnitId(ReservationUnit unit, Apartment? apartment = null)
        {
            if (apartment != null)
            {
                return CompanionStorageUnitIdFromApartment(apartment);
            }

            // apartment_id on the unit row may already be apartments.zaaer_id or apartments.apartment_id
            return unit.ApartmentId;
        }

        private static IReadOnlyList<int> GetDayRateReservationIdRefs(Reservation reservation)
        {
            var refs = new HashSet<int>(GetReservationRateRefs(reservation))
            {
                GetDayRateStorageReservationId(reservation)
            };

            return refs.ToList();
        }

        private static IReadOnlyList<int> GetDayRateUnitIdRefs(ReservationUnit unit, Apartment? apartment = null)
        {
            var storageApartmentId = GetDayRateStorageUnitId(unit, apartment);
            var refs = new HashSet<int>
            {
                storageApartmentId,
                unit.ApartmentId,
                unit.UnitId
            };

            if (unit.ZaaerId is > 0)
            {
                refs.Add(unit.ZaaerId.Value);
            }

            if (apartment != null)
            {
                refs.Add(apartment.ApartmentId);
                if (apartment.ZaaerId is > 0)
                {
                    refs.Add(apartment.ZaaerId.Value);
                }
            }

            return refs.Where(id => id > 0).ToList();
        }

        private static bool DayRateRowMatchesUnit(
            ReservationUnitDayRate row,
            ReservationUnit unit,
            Apartment? apartment = null)
        {
            return GetDayRateUnitIdRefs(unit, apartment).Contains(row.UnitId);
        }

        private async Task<Apartment?> ResolveApartmentForReservationUnitAsync(
            Reservation reservation,
            ReservationUnit unit,
            CancellationToken cancellationToken)
        {
            return await _context.Apartments.AsNoTracking()
                .FirstOrDefaultAsync(
                    a =>
                        a.HotelId == reservation.HotelId &&
                        (a.ApartmentId == unit.ApartmentId || a.ZaaerId == unit.ApartmentId),
                    cancellationToken);
        }

        private async Task<Dictionary<int, Apartment>> LoadApartmentsByReservationUnitsAsync(
            Reservation reservation,
            IReadOnlyList<ReservationUnit> units,
            CancellationToken cancellationToken)
        {
            if (units.Count == 0)
            {
                return new Dictionary<int, Apartment>();
            }

            var aptIds = units
                .SelectMany(u => new[] { u.ApartmentId })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var apartments = await _context.Apartments.AsNoTracking()
                .Where(a => a.HotelId == reservation.HotelId &&
                            (aptIds.Contains(a.ApartmentId) ||
                             (a.ZaaerId != null && aptIds.Contains(a.ZaaerId.Value))))
                .ToListAsync(cancellationToken);

            var map = new Dictionary<int, Apartment>();
            foreach (var u in units)
            {
                var apt = apartments.FirstOrDefault(a =>
                    a.ApartmentId == u.ApartmentId || a.ZaaerId == u.ApartmentId);
                if (apt != null)
                {
                    map[u.UnitId] = apt;
                }
            }

            return map;
        }

        private async Task<int?> ResolveDiscountStorageUnitIdAsync(
            Reservation reservation,
            IReadOnlyList<int> uiUnitIds,
            CancellationToken cancellationToken)
        {
            if (uiUnitIds.Count != 1)
            {
                return null;
            }

            var refs = GetReservationRateRefs(reservation);
            var unit = await _context.ReservationUnits.AsNoTracking()
                .FirstOrDefaultAsync(
                    u => refs.Contains(u.ReservationId) && u.UnitId == uiUnitIds[0],
                    cancellationToken);

            return unit == null ? null : GetDiscountStorageUnitId(unit);
        }

        private static Dictionary<int, Apartment> BuildApartmentsByAnyId(IEnumerable<Apartment> apartments)
        {
            var lookup = new Dictionary<int, Apartment>();
            foreach (var apartment in apartments)
            {
                lookup.TryAdd(apartment.ApartmentId, apartment);
                if (apartment.ZaaerId is > 0)
                {
                    lookup.TryAdd(apartment.ZaaerId.Value, apartment);
                }
            }

            return lookup;
        }

        private static Dictionary<int, ReservationUnit> BuildReservationUnitsByStoredOrUiId(
            IEnumerable<ReservationUnit> units)
        {
            var lookup = new Dictionary<int, ReservationUnit>();
            foreach (var unit in units)
            {
                lookup.TryAdd(unit.UnitId, unit);
                lookup.TryAdd(unit.ApartmentId, unit);
                if (unit.ZaaerId is > 0)
                {
                    lookup.TryAdd(unit.ZaaerId.Value, unit);
                }
            }

            return lookup;
        }

        private static Dictionary<int, ReservationUnit> BuildReservationUnitsByDayRateRef(
            IEnumerable<ReservationUnit> units,
            IReadOnlyDictionary<int, Apartment> apartmentsByUnitId)
        {
            var lookup = new Dictionary<int, ReservationUnit>();
            foreach (var unit in units)
            {
                apartmentsByUnitId.TryGetValue(unit.UnitId, out var apartment);
                foreach (var id in GetDayRateUnitIdRefs(unit, apartment))
                {
                    lookup.TryAdd(id, unit);
                }
            }

            return lookup;
        }

        private static List<int> BuildUnitRateRefsFromLoadedUnits(
            int? unitId,
            IReadOnlyDictionary<int, ReservationUnit> unitsByDayRateRef)
        {
            if (!unitId.HasValue)
            {
                return new List<int>();
            }

            var refs = new HashSet<int> { unitId.Value };
            if (unitsByDayRateRef.TryGetValue(unitId.Value, out var unit))
            {
                foreach (var pair in unitsByDayRateRef.Where(pair => pair.Value.UnitId == unit.UnitId))
                {
                    refs.Add(pair.Key);
                }
            }

            return refs.ToList();
        }

        private static ReservationUnit? FindUnitByStoredOrUiId(
            IReadOnlyDictionary<int, ReservationUnit> unitsByStoredOrUiId,
            int? storedUnitId)
        {
            if (!storedUnitId.HasValue)
            {
                return null;
            }

            unitsByStoredOrUiId.TryGetValue(storedUnitId.Value, out var unit);
            return unit;
        }

        private static string MapDiscountApplyScopeFromPersisted(string applyOn, int? storedUnitId) =>
            applyOn == DiscountApplyOn.Rent || storedUnitId.HasValue ? "selectedUnits" : "reservation";

        private static Apartment? FindApartmentForReservationUnit(
            ReservationUnit ru,
            IReadOnlyDictionary<int, Apartment> apartmentsByAnyId)
        {
            apartmentsByAnyId.TryGetValue(ru.ApartmentId, out var apartment);
            return apartment;
        }

        private static Apartment? FindApartmentByStoredRef(
            int storedRef,
            IReadOnlyDictionary<int, Apartment> apartmentsByAnyId)
        {
            apartmentsByAnyId.TryGetValue(storedRef, out var apartment);
            return apartment;
        }

        private static string FormatApartmentShortLabel(Apartment? apt)
        {
            if (apt == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(apt.ApartmentCode))
            {
                return apt.ApartmentCode.Trim();
            }

            return string.IsNullOrWhiteSpace(apt.ApartmentName) ? string.Empty : apt.ApartmentName.Trim();
        }

        private static string ResolveDiscountUnitLabel(
            int? storedUnitId,
            IReadOnlyDictionary<int, ReservationUnit> unitsByStoredOrUiId,
            IReadOnlyDictionary<int, Apartment> apartmentsByAnyId)
        {
            var ru = FindUnitByStoredOrUiId(unitsByStoredOrUiId, storedUnitId);
            if (ru != null)
            {
                var fromUnit = FormatApartmentShortLabel(FindApartmentForReservationUnit(ru, apartmentsByAnyId));
                if (!string.IsNullOrEmpty(fromUnit))
                {
                    return fromUnit;
                }
            }

            if (storedUnitId.HasValue)
            {
                var fromStored = FormatApartmentShortLabel(
                    FindApartmentByStoredRef(storedUnitId.Value, apartmentsByAnyId));
                if (!string.IsNullOrEmpty(fromStored))
                {
                    return fromStored;
                }
            }

            return string.Empty;
        }

        private static ReservationDetailDiscountDto ToReservationDetailDiscountDto(
            Discount row,
            IReadOnlyDictionary<int, ReservationUnit> unitsByStoredOrUiId,
            IReadOnlyDictionary<int, Apartment> apartmentsByAnyId)
        {
            var ru = FindUnitByStoredOrUiId(unitsByStoredOrUiId, row.UnitId);
            return new ReservationDetailDiscountDto
            {
                DiscountId = row.DiscountId,
                UnitId = ru?.UnitId,
                UnitLabel = ResolveDiscountUnitLabel(row.UnitId, unitsByStoredOrUiId, apartmentsByAnyId),
                ApplyScope = MapDiscountApplyScopeFromPersisted(row.ApplyOn, row.UnitId),
                ApplyOn = row.ApplyOn,
                CalculationMethod = row.CalculationMethod,
                CalculationValue = row.CalculationValue,
                DiscountAmount = row.DiscountAmount,
                Description = row.Description,
                AppliedDate = row.AppliedDate,
                IsActive = row.IsActive
            };
        }

        private static ReservationDiscountLineDto ToDiscountLineDto(
            Discount discount,
            IReadOnlyList<ReservationDetailDiscountDto> discountsList)
        {
            var mapped = discountsList.FirstOrDefault(d => d.DiscountId == discount.DiscountId);
            if (mapped != null)
            {
                return new ReservationDiscountLineDto
                {
                    DiscountId = mapped.DiscountId,
                    UnitId = mapped.UnitId,
                    ApplyScope = mapped.ApplyScope,
                    ApplyOn = mapped.ApplyOn,
                    CalculationMethod = mapped.CalculationMethod,
                    CalculationValue = mapped.CalculationValue,
                    DiscountAmount = mapped.DiscountAmount,
                    Description = mapped.Description,
                    AppliedDate = mapped.AppliedDate,
                    IsActive = mapped.IsActive
                };
            }

            return new ReservationDiscountLineDto
            {
                DiscountId = discount.DiscountId,
                UnitId = discount.UnitId,
                ApplyScope = MapDiscountApplyScopeFromPersisted(discount.ApplyOn, discount.UnitId),
                ApplyOn = discount.ApplyOn,
                CalculationMethod = discount.CalculationMethod,
                CalculationValue = discount.CalculationValue,
                DiscountAmount = discount.DiscountAmount,
                Description = discount.Description,
                AppliedDate = discount.AppliedDate,
                IsActive = discount.IsActive
            };
        }

        /// <summary>
        /// Value persisted on <see cref="ReservationExtra.ReservationId"/> and <see cref="Discount.ReservationId"/>:
        /// Zaaer id when set, else internal reservation PK.
        /// </summary>
        private static int GetReservationExtraStorageReservationId(Reservation reservation) =>
            reservation.ZaaerId is > 0 ? reservation.ZaaerId.Value : reservation.ReservationId;

        /// <summary>
        /// Resolves a UI unit reference (internal PK, unit Zaaer id, apartment id, or apartment Zaaer id) to a <see cref="ReservationUnit"/> row.
        /// </summary>
        private async Task<ReservationUnit?> ResolveReservationUnitForDayRateRowAsync(
            Reservation reservation,
            int requestedUnitId,
            CancellationToken cancellationToken)
        {
            if (requestedUnitId <= 0)
            {
                return null;
            }

            var refs = GetReservationRateRefs(reservation);
            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u => refs.Contains(u.ReservationId))
                .ToListAsync(cancellationToken);

            var direct = units.FirstOrDefault(u => u.UnitId == requestedUnitId);
            if (direct != null)
            {
                return direct;
            }

            var byApartment = units.FirstOrDefault(u => u.ApartmentId == requestedUnitId);
            if (byApartment != null)
            {
                return byApartment;
            }

            var byUnitZaaer = units.FirstOrDefault(u => u.ZaaerId is > 0 && u.ZaaerId.Value == requestedUnitId);
            if (byUnitZaaer != null)
            {
                return byUnitZaaer;
            }

            foreach (var u in units)
            {
                var apt = await ResolveApartmentForReservationUnitAsync(reservation, u, cancellationToken);
                if (apt != null && (apt.ApartmentId == requestedUnitId || apt.ZaaerId == requestedUnitId))
                {
                    return u;
                }
            }

            return null;
        }

        /// <summary>Internal <see cref="ReservationUnit.UnitId"/> for callers that still need the PK.</summary>
        private async Task<int?> ResolveReservationUnitIdForDayRateRowAsync(
            Reservation reservation,
            int requestedUnitId,
            CancellationToken cancellationToken)
        {
            var unit = await ResolveReservationUnitForDayRateRowAsync(reservation, requestedUnitId, cancellationToken);
            return unit?.UnitId;
        }

        private async Task<List<int>> BuildUnitRateRefsAsync(
            Reservation reservation,
            int? unitId,
            CancellationToken cancellationToken)
        {
            if (!unitId.HasValue)
            {
                return new List<int>();
            }

            var reservationRefs = GetReservationRateRefs(reservation);
            var units = await _context.ReservationUnits.AsNoTracking()
                .Where(u =>
                    reservationRefs.Contains(u.ReservationId) &&
                    (u.UnitId == unitId.Value ||
                     u.ApartmentId == unitId.Value ||
                     u.ZaaerId == unitId.Value))
                .ToListAsync(cancellationToken);

            var refs = new HashSet<int> { unitId.Value };
            foreach (var unit in units)
            {
                foreach (var id in GetDayRateUnitIdRefs(
                             unit,
                             await ResolveApartmentForReservationUnitAsync(reservation, unit, cancellationToken)))
                {
                    refs.Add(id);
                }
            }

            return refs.ToList();
        }

        private sealed record HotelPricingTaxConfig(
            decimal VatRate,
            bool VatIncluded,
            decimal EwaRate,
            bool EwaIncluded)
        {
            public bool TaxIncluded => VatIncluded && EwaIncluded;
        }

        private async Task<HotelPricingTaxConfig> GetPricingTaxConfigAsync(int hotelId, CancellationToken cancellationToken)
        {
            var taxes = await _context.Taxes.AsNoTracking()
                .Where(t => t.HotelId == hotelId && t.Enabled)
                .ToListAsync(cancellationToken);

            var vat = taxes
                .Where(IsVatTaxRow)
                .OrderByDescending(t => t.TaxRate)
                .FirstOrDefault();

            var ewa = taxes
                .Where(IsEwaTaxRow)
                .OrderByDescending(t => t.TaxRate)
                .FirstOrDefault();

            return new HotelPricingTaxConfig(
                vat?.TaxRate ?? 0m,
                vat?.TaxIncluded ?? true,
                ewa?.TaxRate ?? 0m,
                ewa?.TaxIncluded ?? true);
        }

        private static bool IsVatTaxRow(Tax tax)
        {
            var taxType = tax.TaxType ?? string.Empty;
            var taxName = tax.TaxName ?? string.Empty;
            return taxType.Equals("vat", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("vat", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEwaTaxRow(Tax tax)
        {
            var taxType = (tax.TaxType ?? string.Empty).ToLowerInvariant();
            var taxName = (tax.TaxName ?? string.Empty).ToLowerInvariant();
            return taxType is "ewa" or "lodging" or "lodging_tax" or "lodgingtax" ||
                   taxName.Contains("ewa", StringComparison.OrdinalIgnoreCase) ||
                   taxName.Contains("lodging", StringComparison.OrdinalIgnoreCase);
        }

        private static (decimal NetAmount, decimal EwaAmount, decimal VatAmount, decimal Total) CalculatePricingAmounts(
            decimal grossRate,
            HotelPricingTaxConfig taxConfig)
        {
            var gross = Math.Round(grossRate, 2, MidpointRounding.AwayFromZero);
            var vatRate = taxConfig.VatRate / 100m;
            var ewaRate = taxConfig.EwaRate / 100m;

            if (vatRate <= 0m && ewaRate <= 0m)
            {
                return (gross, 0m, 0m, gross);
            }

            // Both inclusive: list price is total; EWA applies on net rent, VAT on (net + EWA).
            // T = R + R·lr + (R + R·lr)·vr  =>  R = T / (1 + lr + (1+lr)·vr)
            if (taxConfig.EwaIncluded && taxConfig.VatIncluded)
            {
                var lr = ewaRate;
                var vr = vatRate;
                var divisor = 1m + lr + (1m + lr) * vr;
                if (divisor == 0m)
                {
                    return (gross, 0m, 0m, gross);
                }

                var net = Math.Round(gross / divisor, 2, MidpointRounding.AwayFromZero);
                var ewa = Math.Round(net * lr, 2, MidpointRounding.AwayFromZero);
                var vat = Math.Round((net + ewa) * vr, 2, MidpointRounding.AwayFromZero);
                var total = Math.Round(net + ewa + vat, 2, MidpointRounding.AwayFromZero);
                var drift = Math.Round(gross - total, 2, MidpointRounding.AwayFromZero);
                if (drift != 0m)
                {
                    vat = Math.Round(vat + drift, 2, MidpointRounding.AwayFromZero);
                    total = gross;
                }

                return (net, ewa, vat, total);
            }

            var addedEwa = Math.Round(gross * ewaRate, 2, MidpointRounding.AwayFromZero);
            var vatBase = gross + addedEwa;
            var addedVat = Math.Round(vatBase * vatRate, 2, MidpointRounding.AwayFromZero);
            return (gross, addedEwa, addedVat, Math.Round(gross + addedEwa + addedVat, 2, MidpointRounding.AwayFromZero));
        }

        private static ReservationUnitDayRatesResponseDto BuildUnitDayRatesResponse(
            Reservation reservation,
            int? unitId,
            IReadOnlyList<ReservationUnitDayRate> rows,
            HotelPricingTaxConfig taxConfig)
        {
            var responseReservationId = GetDayRateStorageReservationId(reservation);
            var items = rows.Select(r =>
            {
                var calc = CalculatePricingAmounts(r.GrossRate, taxConfig);
                return new ReservationUnitDayRateDto
                {
                    RateId = r.RateId,
                    ReservationId = r.ReservationId,
                    UnitId = r.UnitId,
                    NightDate = r.NightDate,
                    GrossRate = Math.Round(r.GrossRate, 2, MidpointRounding.AwayFromZero),
                    EwaAmount = calc.EwaAmount,
                    VatAmount = calc.VatAmount,
                    NetAmount = calc.NetAmount,
                    IsManual = r.IsManual
                };
            }).ToList();

            var subtotal = items.Sum(i => i.NetAmount ?? 0m);
            var ewa = items.Sum(i => i.EwaAmount ?? 0m);
            var vat = items.Sum(i => i.VatAmount ?? 0m);
            var total = rows.Sum(r => CalculatePricingAmounts(r.GrossRate, taxConfig).Total);

            return new ReservationUnitDayRatesResponseDto
            {
                ReservationId = responseReservationId,
                UnitId = unitId,
                Items = items,
                Summary = new ReservationUnitDayRateSummaryDto
                {
                    VatRate = taxConfig.VatRate,
                    EwaRate = taxConfig.EwaRate,
                    TaxIncluded = taxConfig.TaxIncluded,
                    Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero),
                    EwaAmount = Math.Round(ewa, 2, MidpointRounding.AwayFromZero),
                    VatAmount = Math.Round(vat, 2, MidpointRounding.AwayFromZero),
                    Total = Math.Round(total, 2, MidpointRounding.AwayFromZero)
                }
            };
        }

        private static void ApplyDayRateRowsToReservationUnits(
            IEnumerable<ReservationUnit> units,
            IReadOnlyList<ReservationUnitDayRate> rows,
            HotelPricingTaxConfig taxConfig,
            IReadOnlyDictionary<int, Apartment>? apartmentsByUnitId = null)
        {
            var unitList = units as IList<ReservationUnit> ?? units.ToList();
            foreach (var u in unitList)
            {
                Apartment? apt = null;
                if (apartmentsByUnitId != null)
                {
                    apartmentsByUnitId.TryGetValue(u.UnitId, out apt);
                }

                var subset = rows.Where(r => DayRateRowMatchesUnit(r, u, apt)).ToList();
                if (unitList.Count == 1 && rows.Count > 0 && subset.Count < rows.Count)
                {
                    subset = rows.ToList();
                }

                if (subset.Count == 0)
                {
                    continue;
                }

                var net = 0m;
                var ewa = 0m;
                var vat = 0m;
                var total = 0m;
                foreach (var row in subset)
                {
                    var calc = CalculatePricingAmounts(row.GrossRate, taxConfig);
                    net += calc.NetAmount;
                    ewa += calc.EwaAmount;
                    vat += calc.VatAmount;
                    total += calc.Total;
                }

                u.RentAmount = Math.Round(net, 2, MidpointRounding.AwayFromZero);
                u.VatRate = taxConfig.VatRate;
                u.LodgingTaxRate = taxConfig.EwaRate;
                u.VatAmount = Math.Round(vat, 2, MidpointRounding.AwayFromZero);
                u.LodgingTaxAmount = Math.Round(ewa, 2, MidpointRounding.AwayFromZero);
                u.TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero);
                u.NumberOfNights ??= subset.Count;
            }
        }

        private static void ApplyRateSummaryToReservation(
            Reservation reservation,
            IReadOnlyList<ReservationUnitDayRate> rows,
            HotelPricingTaxConfig taxConfig)
        {
            var subtotal = 0m;
            var ewa = 0m;
            var vat = 0m;
            var total = 0m;

            foreach (var row in rows)
            {
                var calc = CalculatePricingAmounts(row.GrossRate, taxConfig);
                subtotal += calc.NetAmount;
                ewa += calc.EwaAmount;
                vat += calc.VatAmount;
                total += calc.Total;
            }

            reservation.Subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
            reservation.VatRate = taxConfig.VatRate;
            reservation.LodgingTaxRate = taxConfig.EwaRate;
            reservation.LodgingTaxAmount = Math.Round(ewa, 2, MidpointRounding.AwayFromZero);
            reservation.VatAmount = Math.Round(vat, 2, MidpointRounding.AwayFromZero);
            reservation.TotalTaxAmount = Math.Round(ewa + vat, 2, MidpointRounding.AwayFromZero);
            reservation.TotalAmount = Math.Round(total, 2, MidpointRounding.AwayFromZero);
            reservation.AmountPaid ??= 0m;
            reservation.BalanceAmount = Math.Round(
                reservation.TotalAmount.GetValueOrDefault() - reservation.AmountPaid.GetValueOrDefault(),
                2,
                MidpointRounding.AwayFromZero);
        }

    }
}
