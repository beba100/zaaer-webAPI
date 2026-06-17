using System.Text.RegularExpressions;
using AutoMapper;
using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// PMS corporate customer create/update using central numbering (same pattern as <see cref="PmsCustomerService"/>).
    /// </summary>
    public sealed class PmsCorporateCustomerService : IPmsCorporateCustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICorporateCustomerRepository _corporateCustomerRepository;
        private readonly INumberingService _numberingService;
        private readonly IMapper _mapper;
        private readonly ICorporateCustomerService _corporateCustomerService;

        /// <summary>Initializes a new instance of the <see cref="PmsCorporateCustomerService"/> class.</summary>
        public PmsCorporateCustomerService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            ICorporateCustomerRepository corporateCustomerRepository,
            INumberingService numberingService,
            IMapper mapper,
            ICorporateCustomerService corporateCustomerService)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _corporateCustomerRepository = corporateCustomerRepository;
            _numberingService = numberingService;
            _mapper = mapper;
            _corporateCustomerService = corporateCustomerService;
        }

        public async Task<CorporatePickerResponseDto> GetForPickerAsync(
            int? hotelId,
            string? hotelCode,
            CancellationToken cancellationToken = default)
        {
            var resolvedHotelId = await ResolveHotelIdForPickerAsync(hotelId, hotelCode, cancellationToken);
            if (resolvedHotelId is not > 0)
            {
                return new CorporatePickerResponseDto();
            }

            var hotelKeys = new HashSet<int> { resolvedHotelId.Value };
            if (hotelId is > 0)
            {
                hotelKeys.Add(hotelId.Value);
            }

            if (!string.IsNullOrWhiteSpace(hotelCode))
            {
                var norm = hotelCode.Trim().ToLowerInvariant();
                var rows = await _context.HotelSettings.AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.Trim().ToLower() == norm)
                    .Select(h => new { h.HotelId, h.ZaaerId })
                    .ToListAsync(cancellationToken);

                foreach (var row in rows)
                {
                    if (row.HotelId > 0)
                    {
                        hotelKeys.Add(row.HotelId);
                    }

                    if (row.ZaaerId is > 0)
                    {
                        hotelKeys.Add(row.ZaaerId.Value);
                    }
                }
            }

            var merged = new List<CorporateCustomerResponseDto>();
            var seenCorporateIds = new HashSet<int>();
            foreach (var hotelKey in hotelKeys.OrderBy(x => x))
            {
                var batch = await _corporateCustomerService.GetCorporateCustomersByHotelIdAsync(hotelKey);
                foreach (var row in batch)
                {
                    if (seenCorporateIds.Add(row.CorporateId))
                    {
                        merged.Add(row);
                    }
                }
            }

            merged.Sort((a, b) => string.Compare(a.CorporateName, b.CorporateName, StringComparison.OrdinalIgnoreCase));

            var effectiveResolved = hotelId is > 0 ? hotelId : merged.FirstOrDefault()?.HotelId ?? resolvedHotelId;
            return new CorporatePickerResponseDto
            {
                ResolvedHotelId = effectiveResolved,
                Items = merged
            };
        }

        /// <inheritdoc />
        public async Task<CorporateCustomerResponseDto?> GetByZaaerOrCorporateIdAsync(
            int id,
            int? hotelId = null,
            CancellationToken cancellationToken = default)
        {
            var entity = await ResolveCorporateAsync(id, hotelId, asNoTracking: true, cancellationToken);
            return entity == null ? null : await MapToResponseDtoAsync(entity, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<CorporateCustomerResponseDto> CreateAsync(
            CreateCorporateCustomerDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (dto.HotelId <= 0)
            {
                throw new ArgumentException("HotelId is required for PMS corporate customer creation.", nameof(dto));
            }

            var hotelId = dto.HotelId;
            var auditIds = new List<long>();

            try
            {
                ValidatePmsInstitutionBusinessRules(
                    dto.Country,
                    dto.City,
                    dto.Address,
                    dto.PostalCode,
                    dto.CommercialRegistrationNo,
                    dto.VatRegistrationNo);

                await AssertUniqueInHotelAsync(
                    hotelId,
                    dto.CorporateName.Trim(),
                    dto.VatRegistrationNo,
                    dto.CommercialRegistrationNo,
                    dto.Email,
                    excludeCorporateId: null,
                    cancellationToken);

                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "corporate",
                    hotelId,
                    null,
                    $"pms-corporate:{hotelId}:{Guid.NewGuid():N}",
                    cancellationToken);

                var zaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId);
                var corNo = identity.DocumentNo;
                auditIds.Add(identity.AuditId);

                var entity = _mapper.Map<CorporateCustomer>(dto);
                entity.CorporateName = dto.CorporateName.Trim();
                entity.CorNo = corNo;
                entity.ZaaerId = zaaerId;
                entity.HotelId = hotelId;
                entity.CreatedAt = KsaTime.Now;
                entity.UpdatedAt = KsaTime.Now;

                NormalizeOptionalStrings(entity, dto);

                var created = await _corporateCustomerRepository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                var reloaded = await ReloadCorporateAsync(created.CorporateId, cancellationToken);
                var forDto = reloaded ?? created;
                return await MapToResponseDtoAsync(forDto, cancellationToken);
            }
            catch (Exception ex)
            {
                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkVoidedAsync(auditId, ex.Message, cancellationToken);
                }

                throw;
            }
        }

        /// <inheritdoc />
        public async Task<CorporateCustomerResponseDto?> UpdateAsync(
            int id,
            UpdateCorporateCustomerDto dto,
            int? hotelId = null,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var entity = await ResolveCorporateAsync(id, hotelId, asNoTracking: false, cancellationToken);
            if (entity == null)
            {
                return null;
            }

            if (dto.HotelId != entity.HotelId)
            {
                throw new ArgumentException("HotelId does not match this corporate customer record.");
            }

            ValidatePmsInstitutionBusinessRules(
                dto.Country,
                dto.City,
                dto.Address,
                dto.PostalCode,
                dto.CommercialRegistrationNo,
                dto.VatRegistrationNo);

            await AssertUniqueInHotelAsync(
                entity.HotelId,
                dto.CorporateName.Trim(),
                dto.VatRegistrationNo,
                dto.CommercialRegistrationNo,
                dto.Email,
                entity.CorporateId,
                cancellationToken);

            entity.CorporateName = dto.CorporateName.Trim();

            if (dto.CorporateNameAr != null)
            {
                entity.CorporateNameAr = string.IsNullOrWhiteSpace(dto.CorporateNameAr)
                    ? null
                    : dto.CorporateNameAr.Trim();
            }

            if (dto.Country != null)
            {
                entity.Country = string.IsNullOrWhiteSpace(dto.Country) ? null : dto.Country.Trim();
            }

            if (dto.CountryAr != null)
            {
                entity.CountryAr = string.IsNullOrWhiteSpace(dto.CountryAr) ? null : dto.CountryAr.Trim();
            }

            if (dto.VatRegistrationNo != null)
            {
                entity.VatRegistrationNo = string.IsNullOrWhiteSpace(dto.VatRegistrationNo)
                    ? null
                    : dto.VatRegistrationNo.Trim();
            }

            if (dto.CommercialRegistrationNo != null)
            {
                entity.CommercialRegistrationNo = string.IsNullOrWhiteSpace(dto.CommercialRegistrationNo)
                    ? null
                    : dto.CommercialRegistrationNo.Trim();
            }

            if (dto.DiscountMethod != null)
            {
                entity.DiscountMethod = string.IsNullOrWhiteSpace(dto.DiscountMethod)
                    ? null
                    : dto.DiscountMethod.Trim();
            }

            if (dto.DiscountValue.HasValue)
            {
                entity.DiscountValue = dto.DiscountValue;
            }

            if (dto.City != null)
            {
                entity.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
            }

            if (dto.CityAr != null)
            {
                entity.CityAr = string.IsNullOrWhiteSpace(dto.CityAr) ? null : dto.CityAr.Trim();
            }

            if (dto.PostalCode != null)
            {
                entity.PostalCode = string.IsNullOrWhiteSpace(dto.PostalCode) ? null : dto.PostalCode.Trim();
            }

            if (dto.Address != null)
            {
                entity.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            }

            if (dto.AddressAr != null)
            {
                entity.AddressAr = string.IsNullOrWhiteSpace(dto.AddressAr) ? null : dto.AddressAr.Trim();
            }

            if (dto.Email != null)
            {
                entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            }

            if (dto.CorporatePhone != null)
            {
                entity.CorporatePhone = string.IsNullOrWhiteSpace(dto.CorporatePhone)
                    ? null
                    : dto.CorporatePhone.Trim();
            }

            if (dto.ContactPersonName != null)
            {
                entity.ContactPersonName = string.IsNullOrWhiteSpace(dto.ContactPersonName)
                    ? null
                    : dto.ContactPersonName.Trim();
            }

            if (dto.ContactPersonNameAr != null)
            {
                entity.ContactPersonNameAr = string.IsNullOrWhiteSpace(dto.ContactPersonNameAr)
                    ? null
                    : dto.ContactPersonNameAr.Trim();
            }

            if (dto.ContactPersonPhone != null)
            {
                entity.ContactPersonPhone = string.IsNullOrWhiteSpace(dto.ContactPersonPhone)
                    ? null
                    : dto.ContactPersonPhone.Trim();
            }

            if (dto.CorporateLogoUrl != null)
            {
                entity.CorporateLogoUrl = string.IsNullOrWhiteSpace(dto.CorporateLogoUrl)
                    ? null
                    : dto.CorporateLogoUrl.Trim();
            }

            if (dto.Notes != null)
            {
                entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
            }

            entity.IsActive = dto.IsActive;
            entity.UpdatedAt = KsaTime.Now;

            await _corporateCustomerRepository.UpdateAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            var reloaded = await ReloadCorporateAsync(entity.CorporateId, cancellationToken);
            return await MapToResponseDtoAsync(reloaded ?? entity, cancellationToken);
        }

        /// <summary>
        /// KSA-oriented rules for the PMS corporate / institution editor (10-digit CR, 15-digit VAT starting and ending with 3).
        /// </summary>
        private static void ValidatePmsInstitutionBusinessRules(
            string? country,
            string? city,
            string? address,
            string? postalCode,
            string? commercialRegistrationNo,
            string? vatRegistrationNo)
        {
            if (string.IsNullOrWhiteSpace(country))
            {
                throw new ArgumentException("Country is required.");
            }

            if (string.IsNullOrWhiteSpace(city))
            {
                throw new ArgumentException("City is required.");
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.");
            }

            if (string.IsNullOrWhiteSpace(postalCode))
            {
                throw new ArgumentException("Postal code is required.");
            }

            var cr = (commercialRegistrationNo ?? string.Empty).Trim();
            if (!Regex.IsMatch(cr, @"^\d{10}$"))
            {
                throw new ArgumentException("Commercial registration must be exactly 10 digits.");
            }

            var vat = (vatRegistrationNo ?? string.Empty).Trim();
            if (!Regex.IsMatch(vat, @"^3\d{13}3$"))
            {
                throw new ArgumentException("VAT number must be 15 digits and start and end with 3.");
            }
        }

        private static void NormalizeOptionalStrings(CorporateCustomer entity, CreateCorporateCustomerDto dto)
        {
            entity.CorporateNameAr = string.IsNullOrWhiteSpace(dto.CorporateNameAr) ? null : dto.CorporateNameAr.Trim();
            entity.Country = string.IsNullOrWhiteSpace(dto.Country) ? null : dto.Country.Trim();
            entity.CountryAr = string.IsNullOrWhiteSpace(dto.CountryAr) ? null : dto.CountryAr.Trim();
            entity.VatRegistrationNo = string.IsNullOrWhiteSpace(dto.VatRegistrationNo)
                ? null
                : dto.VatRegistrationNo.Trim();
            entity.CommercialRegistrationNo = string.IsNullOrWhiteSpace(dto.CommercialRegistrationNo)
                ? null
                : dto.CommercialRegistrationNo.Trim();
            entity.DiscountMethod = string.IsNullOrWhiteSpace(dto.DiscountMethod) ? null : dto.DiscountMethod.Trim();
            entity.City = string.IsNullOrWhiteSpace(dto.City) ? null : dto.City.Trim();
            entity.CityAr = string.IsNullOrWhiteSpace(dto.CityAr) ? null : dto.CityAr.Trim();
            entity.PostalCode = string.IsNullOrWhiteSpace(dto.PostalCode) ? null : dto.PostalCode.Trim();
            entity.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            entity.AddressAr = string.IsNullOrWhiteSpace(dto.AddressAr) ? null : dto.AddressAr.Trim();
            entity.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            entity.CorporatePhone = string.IsNullOrWhiteSpace(dto.CorporatePhone) ? null : dto.CorporatePhone.Trim();
            entity.ContactPersonName = string.IsNullOrWhiteSpace(dto.ContactPersonName)
                ? null
                : dto.ContactPersonName.Trim();
            entity.ContactPersonNameAr = string.IsNullOrWhiteSpace(dto.ContactPersonNameAr)
                ? null
                : dto.ContactPersonNameAr.Trim();
            entity.ContactPersonPhone = string.IsNullOrWhiteSpace(dto.ContactPersonPhone)
                ? null
                : dto.ContactPersonPhone.Trim();
            entity.CorporateLogoUrl = string.IsNullOrWhiteSpace(dto.CorporateLogoUrl)
                ? null
                : dto.CorporateLogoUrl.Trim();
            entity.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        }

        private async Task AssertUniqueInHotelAsync(
            int hotelId,
            string corporateName,
            string? vatRegistrationNo,
            string? commercialRegistrationNo,
            string? email,
            int? excludeCorporateId,
            CancellationToken cancellationToken)
        {
            var nameQ = _context.CorporateCustomers.Where(cc => cc.HotelId == hotelId && cc.CorporateName == corporateName);
            if (excludeCorporateId is > 0)
            {
                nameQ = nameQ.Where(cc => cc.CorporateId != excludeCorporateId.Value);
            }

            if (await nameQ.AnyAsync(cancellationToken))
            {
                throw new ArgumentException($"Corporate customer with name '{corporateName}' already exists for this hotel.");
            }

            if (!string.IsNullOrWhiteSpace(vatRegistrationNo))
            {
                var vat = vatRegistrationNo.Trim();
                var vatQ = _context.CorporateCustomers.Where(cc => cc.HotelId == hotelId && cc.VatRegistrationNo == vat);
                if (excludeCorporateId is > 0)
                {
                    vatQ = vatQ.Where(cc => cc.CorporateId != excludeCorporateId.Value);
                }

                if (await vatQ.AnyAsync(cancellationToken))
                {
                    throw new ArgumentException($"Corporate customer with VAT registration number '{vat}' already exists for this hotel.");
                }
            }

            if (!string.IsNullOrWhiteSpace(commercialRegistrationNo))
            {
                var cr = commercialRegistrationNo.Trim();
                var crQ = _context.CorporateCustomers.Where(
                    cc => cc.HotelId == hotelId && cc.CommercialRegistrationNo == cr);
                if (excludeCorporateId is > 0)
                {
                    crQ = crQ.Where(cc => cc.CorporateId != excludeCorporateId.Value);
                }

                if (await crQ.AnyAsync(cancellationToken))
                {
                    throw new ArgumentException(
                        $"Corporate customer with commercial registration number '{cr}' already exists for this hotel.");
                }
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var em = email.Trim();
                var emQ = _context.CorporateCustomers.Where(cc => cc.HotelId == hotelId && cc.Email == em);
                if (excludeCorporateId is > 0)
                {
                    emQ = emQ.Where(cc => cc.CorporateId != excludeCorporateId.Value);
                }

                if (await emQ.AnyAsync(cancellationToken))
                {
                    throw new ArgumentException($"Corporate customer with email '{em}' already exists for this hotel.");
                }
            }
        }

        private async Task<int?> ResolveHotelIdForPickerAsync(
            int? hotelId,
            string? hotelCode,
            CancellationToken cancellationToken)
        {
            List<int> codeCandidates = new();
            string? normCode = null;
            if (!string.IsNullOrWhiteSpace(hotelCode))
            {
                normCode = hotelCode.Trim().ToLowerInvariant();
                codeCandidates = await _context.HotelSettings.AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.Trim().ToLower() == normCode)
                    .OrderBy(h => h.HotelId)
                    .Select(h => h.HotelId)
                    .ToListAsync(cancellationToken);
            }

            if (hotelId is > 0)
            {
                var inSettings = await _context.HotelSettings.AsNoTracking()
                    .AnyAsync(h => h.HotelId == hotelId.Value, cancellationToken);
                if (inSettings)
                {
                    return hotelId;
                }

                var corporateForHotel = await _context.CorporateCustomers.AsNoTracking()
                    .AnyAsync(c => c.HotelId == hotelId.Value, cancellationToken);
                if (corporateForHotel)
                {
                    return hotelId;
                }

                var reservationForHotel = await _context.Reservations.AsNoTracking()
                    .AnyAsync(r => r.HotelId == hotelId.Value, cancellationToken);
                if (reservationForHotel)
                {
                    return hotelId;
                }

                var internalFromZaaer = await _context.HotelSettings.AsNoTracking()
                    .Where(h => h.ZaaerId == hotelId.Value)
                    .Select(h => (int?)h.HotelId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (internalFromZaaer is > 0)
                {
                    return internalFromZaaer;
                }
            }

            foreach (var candidate in codeCandidates)
            {
                var hasCorporate = await _context.CorporateCustomers.AsNoTracking()
                    .AnyAsync(cc => cc.HotelId == candidate, cancellationToken);
                if (hasCorporate)
                {
                    return candidate;
                }
            }

            if (normCode != null)
            {
                var zaaerKeys = await _context.HotelSettings.AsNoTracking()
                    .Where(h => h.HotelCode != null && h.HotelCode.Trim().ToLower() == normCode)
                    .Where(h => h.ZaaerId != null && h.ZaaerId > 0)
                    .Select(h => h.ZaaerId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var zaaerKey in zaaerKeys)
                {
                    var hasCorporate = await _context.CorporateCustomers.AsNoTracking()
                        .AnyAsync(cc => cc.HotelId == zaaerKey, cancellationToken);
                    if (hasCorporate)
                    {
                        return zaaerKey;
                    }
                }
            }

            return null;
        }

        private async Task<CorporateCustomer?> ResolveCorporateAsync(
            int id,
            int? hotelId,
            bool asNoTracking,
            CancellationToken cancellationToken)
        {
            var query = asNoTracking ? _context.CorporateCustomers.AsNoTracking() : _context.CorporateCustomers.AsQueryable();
            if (hotelId is > 0)
            {
                query = query.Where(cc => cc.HotelId == hotelId.Value);
            }

            return await query.FirstOrDefaultAsync(
                cc => cc.CorporateId == id || (cc.ZaaerId.HasValue && cc.ZaaerId.Value == id),
                cancellationToken);
        }

        private Task<CorporateCustomer?> ReloadCorporateAsync(int corporateId, CancellationToken cancellationToken) =>
            _context.CorporateCustomers
                .AsNoTracking()
                .Include(cc => cc.HotelSettings)
                .FirstOrDefaultAsync(cc => cc.CorporateId == corporateId, cancellationToken);

        private async Task<CorporateCustomerResponseDto> MapToResponseDtoAsync(
            CorporateCustomer corporate,
            CancellationToken cancellationToken)
        {
            if (corporate == null)
            {
                throw new InvalidOperationException("Corporate customer entity is required for mapping.");
            }

            var dto = _mapper.Map<CorporateCustomerResponseDto>(corporate);
            if (dto == null)
            {
                throw new InvalidOperationException("Failed to map corporate customer to response DTO.");
            }

            var reservationRefs = new List<int> { corporate.CorporateId };
            if (corporate.ZaaerId is int zid && zid > 0)
            {
                reservationRefs.Add(zid);
            }

            dto.TotalReservations = await _context.Reservations.CountAsync(
                r => r.CorporateId != null && reservationRefs.Contains(r.CorporateId.Value),
                cancellationToken);

            return dto;
        }
    }
}
