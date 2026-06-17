using FinanceLedgerAPI.Models;
using Microsoft.EntityFrameworkCore;
using zaaerIntegration.Data;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Security;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// PMS customer create/update using the same numbering rules as the legacy Zaaer customer sync service.
    /// </summary>
    public sealed class PmsCustomerService : IPmsCustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerIdentificationRepository _customerIdentificationRepository;
        private readonly INumberingService _numberingService;
        private readonly ICurrentUserContext _currentUser;
        private readonly ICustomerService _customerService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PmsCustomerService"/> class.
        /// </summary>
        public PmsCustomerService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            ICustomerRepository customerRepository,
            ICustomerIdentificationRepository customerIdentificationRepository,
            INumberingService numberingService,
            ICurrentUserContext currentUser,
            ICustomerService customerService)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _customerRepository = customerRepository;
            _customerIdentificationRepository = customerIdentificationRepository;
            _numberingService = numberingService;
            _currentUser = currentUser;
            _customerService = customerService;
        }

        /// <inheritdoc />
        public Task<(IEnumerable<CustomerResponseDto> Customers, int TotalCount)> GetPagedAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string? searchMode = null,
            int? nationalityId = null,
            int? guestCategoryId = null) =>
            _customerService.GetAllCustomersAsync(
                pageNumber,
                pageSize,
                searchTerm,
                searchMode,
                nationalityId,
                guestCategoryId);

        /// <inheritdoc />
        public async Task<CustomerResponseDto?> GetByZaaerOrCustomerIdAsync(
            int id,
            int? hotelId = null,
            CancellationToken cancellationToken = default)
        {
            var customer = await ResolveCustomerEntityAsync(id, hotelId, cancellationToken);
            return customer == null ? null : await MapToResponseDtoAsync(customer, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<CustomerResponseDto> CreateAsync(
            CreateCustomerDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (!dto.HotelId.HasValue || dto.HotelId.Value <= 0)
            {
                throw new ArgumentException("HotelId is required for PMS customer creation.", nameof(dto));
            }

            var hotelId = dto.HotelId.Value;
            var auditIds = new List<long>();
            int? zaaerId = null;
            string? customerNo = string.IsNullOrWhiteSpace(dto.CustomerNo) ? null : dto.CustomerNo.Trim();

            try
            {
                var identity = await _numberingService.GetNextBusinessIdentityAsync(
                    "customer",
                    hotelId,
                    null,
                    $"pms-customer:{hotelId}:{Guid.NewGuid():N}",
                    cancellationToken);

                zaaerId = ZaaerIdMapper.ToNullableInt32(identity.ZaaerId);
                customerNo = identity.DocumentNo;
                auditIds.Add(identity.AuditId);

                var birthGregorian = KsaTime.ToGregorianBirthDateOnly(dto.BirthdateGregorian ?? dto.Birthday);
                var enteredBy = PmsCurrentUser.ResolveUserId(_currentUser);

                var customer = new Customer
                {
                    CustomerNo = customerNo,
                    CustomerName = dto.CustomerName.Trim(),
                    GtypeId = dto.GtypeId,
                    NId = dto.NId,
                    GuestCategoryId = dto.GuestCategoryId,
                    VisaNo = string.IsNullOrWhiteSpace(dto.VisaNo) ? null : dto.VisaNo.Trim(),
                    MobileNo = string.IsNullOrWhiteSpace(dto.MobileNo) ? null : dto.MobileNo.Trim(),
                    Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                    Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                    Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim(),
                    Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim(),
                    EnteredBy = enteredBy,
                    EnteredAt = KsaTime.Now,
                    BirthdateHijri = string.IsNullOrWhiteSpace(dto.BirthdateHijri) ? null : dto.BirthdateHijri.Trim(),
                    BirthdateGregorian = birthGregorian,
                    Birthday = birthGregorian,
                    HotelId = hotelId,
                    ZaaerId = zaaerId,
                    IsActive = true,
                    CreatedAt = KsaTime.Now,
                    UpdatedAt = KsaTime.Now
                };

                var created = await _customerRepository.AddAsync(customer);
                await _unitOfWork.SaveChangesAsync();

                await ReplaceIdentificationsAsync(created, dto.Identifications, cancellationToken);

                foreach (var auditId in auditIds)
                {
                    await _numberingService.MarkCommittedAsync(auditId, cancellationToken);
                }

                return (await MapToResponseDtoAsync(created, cancellationToken))!;
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
        public async Task<CustomerResponseDto?> UpdateAsync(
            int id,
            UpdateCustomerDto dto,
            int? hotelId = null,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            var customer = await ResolveCustomerEntityForUpdateAsync(id, hotelId, cancellationToken);
            if (customer == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                customer.CustomerName = dto.CustomerName.Trim();
            }

            if (dto.GtypeId.HasValue)
            {
                customer.GtypeId = dto.GtypeId;
            }

            if (dto.NId.HasValue)
            {
                customer.NId = dto.NId;
            }

            if (dto.GuestCategoryId.HasValue)
            {
                customer.GuestCategoryId = dto.GuestCategoryId;
            }

            if (dto.VisaNo != null)
            {
                customer.VisaNo = string.IsNullOrWhiteSpace(dto.VisaNo) ? null : dto.VisaNo.Trim();
            }

            if (dto.MobileNo != null)
            {
                customer.MobileNo = string.IsNullOrWhiteSpace(dto.MobileNo) ? null : dto.MobileNo.Trim();
            }

            if (dto.Email != null)
            {
                customer.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();
            }

            if (dto.Address != null)
            {
                customer.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
            }

            if (dto.Comments != null)
            {
                customer.Comments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
            }

            if (dto.Gender != null)
            {
                customer.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : dto.Gender.Trim();
            }

            if (dto.BirthdateHijri != null)
            {
                customer.BirthdateHijri = string.IsNullOrWhiteSpace(dto.BirthdateHijri) ? null : dto.BirthdateHijri.Trim();
            }

            if (dto.BirthdateGregorian.HasValue || dto.Birthday.HasValue)
            {
                var birthGregorian = KsaTime.ToGregorianBirthDateOnly(dto.BirthdateGregorian ?? dto.Birthday);
                customer.BirthdateGregorian = birthGregorian;
                customer.Birthday = birthGregorian;
            }

            customer.UpdatedAt = KsaTime.Now;

            await _customerRepository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            if (dto.Identifications != null)
            {
                await ReplaceIdentificationsAsync(customer, dto.Identifications, cancellationToken);
            }

            return await MapToResponseDtoAsync(customer, cancellationToken);
        }

        private async Task<Customer?> ResolveCustomerEntityAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            var query = _context.Customers.AsNoTracking();
            if (hotelId is > 0)
            {
                query = query.Where(c => c.HotelId == hotelId.Value);
            }

            return await query.FirstOrDefaultAsync(
                c => c.CustomerId == id || c.ZaaerId == id,
                cancellationToken);
        }

        private async Task<Customer?> ResolveCustomerEntityForUpdateAsync(
            int id,
            int? hotelId,
            CancellationToken cancellationToken)
        {
            var query = _context.Customers.AsQueryable();
            if (hotelId is > 0)
            {
                query = query.Where(c => c.HotelId == hotelId.Value);
            }

            return await query.FirstOrDefaultAsync(
                c => c.CustomerId == id || c.ZaaerId == id,
                cancellationToken);
        }

        private static int IdentificationStorageCustomerId(Customer customer) =>
            customer.ZaaerId is > 0 ? customer.ZaaerId.Value : customer.CustomerId;

        private static IReadOnlyList<int> GetCustomerIdentityRefs(int customerId, int? zaaerId)
        {
            var refs = new List<int> { customerId };
            if (zaaerId.HasValue && zaaerId.Value != customerId)
            {
                refs.Add(zaaerId.Value);
            }

            return refs;
        }

        private async Task ReplaceIdentificationsAsync(
            Customer customer,
            IReadOnlyList<CustomerIdentificationDto>? identifications,
            CancellationToken cancellationToken)
        {
            if (identifications == null)
            {
                return;
            }

            var storageId = IdentificationStorageCustomerId(customer);
            var refs = GetCustomerIdentityRefs(customer.CustomerId, customer.ZaaerId).ToHashSet();

            var existing = await _context.CustomerIdentifications
                .Where(i => refs.Contains(i.CustomerId))
                .ToListAsync(cancellationToken);

            foreach (var row in existing)
            {
                await _customerIdentificationRepository.DeleteAsync(row);
            }

            await _unitOfWork.SaveChangesAsync();

            var toAdd = identifications
                .Where(i => i.IdTypeId > 0 && !string.IsNullOrWhiteSpace(i.IdNumber))
                .ToList();

            for (var idx = 0; idx < toAdd.Count; idx += 1)
            {
                var identificationDto = toAdd[idx];
                await _customerIdentificationRepository.AddAsync(
                    new CustomerIdentification
                    {
                        CustomerId = storageId,
                        IdTypeId = identificationDto.IdTypeId,
                        IdNumber = identificationDto.IdNumber.Trim(),
                        VersionNumber = identificationDto.VersionNumber,
                        IssuePlace = identificationDto.IssuePlace,
                        IssuePlaceAr = identificationDto.IssuePlaceAr,
                        IssueDate = identificationDto.IssueDate,
                        ExpiryDate = identificationDto.ExpiryDate,
                        Notes = identificationDto.Notes,
                        IsPrimary = identificationDto.IsPrimary || idx == 0,
                        IsActive = identificationDto.IsActive,
                        CreatedAt = KsaTime.Now,
                        UpdatedAt = KsaTime.Now
                    });
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<CustomerResponseDto> MapToResponseDtoAsync(
            Customer customer,
            CancellationToken cancellationToken)
        {
            var guestType = customer.GtypeId.HasValue
                ? await _unitOfWork.GuestTypes.GetByIdAsync(customer.GtypeId.Value)
                : null;
            var nationality = customer.NId.HasValue
                ? await _unitOfWork.Nationalities.GetByIdAsync(customer.NId.Value)
                : null;
            var guestCategory = customer.GuestCategoryId.HasValue
                ? await _unitOfWork.GuestCategories.GetByIdAsync(customer.GuestCategoryId.Value)
                : null;

            var refs = GetCustomerIdentityRefs(customer.CustomerId, customer.ZaaerId);
            var identRows = await _context.CustomerIdentifications
                .AsNoTracking()
                .Where(i => refs.Contains(i.CustomerId))
                .OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.IdentificationId)
                .ToListAsync(cancellationToken);

            var idTypeIds = identRows.Select(i => i.IdTypeId).Distinct().ToList();
            var idTypeMap = new Dictionary<int, IdType>();
            foreach (var idTypeId in idTypeIds)
            {
                var idType = await _unitOfWork.IdTypes.GetByIdAsync(idTypeId);
                if (idType != null)
                {
                    idTypeMap[idTypeId] = idType;
                }
            }

            return new CustomerResponseDto
            {
                CustomerId = customer.CustomerId,
                ZaaerId = customer.ZaaerId,
                CustomerNo = customer.CustomerNo,
                CustomerName = customer.CustomerName,
                GtypeId = customer.GtypeId,
                NId = customer.NId,
                GuestCategoryId = customer.GuestCategoryId,
                VisaNo = customer.VisaNo,
                MobileNo = customer.MobileNo,
                Email = customer.Email,
                Address = customer.Address,
                Comments = customer.Comments,
                EnteredBy = customer.EnteredBy,
                EnteredAt = customer.EnteredAt,
                Gender = customer.Gender,
                Birthday = customer.Birthday,
                BirthdateGregorian = customer.BirthdateGregorian,
                BirthdateHijri = customer.BirthdateHijri,
                GuestTypeName = guestType?.GtypeName,
                NationalityName = nationality?.NName,
                NationalityNameAr = nationality?.NNameAr,
                GuestCategoryName = guestCategory?.GcName,
                Identifications = identRows
                    .Select(i =>
                    {
                        idTypeMap.TryGetValue(i.IdTypeId, out var idt);
                        return new CustomerIdentificationResponseDto
                        {
                            IdentificationId = i.IdentificationId,
                            IdTypeId = i.IdTypeId,
                            IdTypeName = idt?.ItName,
                            IdTypeNameAr = idt?.ItNameAr,
                            IdNumber = i.IdNumber,
                            VersionNumber = i.VersionNumber,
                            IsPrimary = i.IsPrimary
                        };
                    })
                    .ToList()
            };
        }
    }
}
