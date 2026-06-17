using AutoMapper;
using FinanceLedgerAPI.Models;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;
using zaaerIntegration.Repositories.Interfaces;
using zaaerIntegration.Services.Interfaces;
using zaaerIntegration.Utilities;

namespace zaaerIntegration.Services.Implementations
{
    /// <summary>
    /// Customer Service Implementation
    /// ????? ???? ???????
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustomerRepository _customerRepository;
        private readonly ICustomerIdentificationRepository _customerIdentificationRepository;
        private readonly IMapper _mapper;

        public CustomerService(IUnitOfWork unitOfWork, ICustomerRepository customerRepository, ICustomerIdentificationRepository customerIdentificationRepository, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _customerRepository = customerRepository;
            _customerIdentificationRepository = customerIdentificationRepository;
            _mapper = mapper;
        }

        public async Task<(IEnumerable<CustomerResponseDto> Customers, int TotalCount)> GetAllCustomersAsync(
            int pageNumber = 1,
            int pageSize = 10,
            string? searchTerm = null,
            string? searchMode = null,
            int? nationalityId = null,
            int? guestCategoryId = null)
        {
            try
            {
                var (customers, totalCount) = await _customerRepository.GetPagedWithFiltersAsync(
                    pageNumber,
                    pageSize,
                    searchTerm,
                    searchMode,
                    nationalityId,
                    guestCategoryId);

                var customerDtos = customers.Select(MapCustomerForList).ToList();
                return (customerDtos, totalCount);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error retrieving customers: {ex.Message}", ex);
            }
        }

        private static CustomerResponseDto MapCustomerForList(Customer c)
        {
            var dto = new CustomerResponseDto
            {
                CustomerId = c.CustomerId,
                ZaaerId = c.ZaaerId,
                CustomerNo = c.CustomerNo,
                CustomerName = c.CustomerName,
                GtypeId = c.GtypeId,
                NId = c.NId,
                GuestCategoryId = c.GuestCategoryId,
                VisaNo = c.VisaNo,
                MobileNo = c.MobileNo,
                Email = c.Email,
                Address = c.Address,
                Comments = c.Comments,
                EnteredBy = c.EnteredBy,
                EnteredAt = c.EnteredAt,
                Gender = c.Gender,
                Birthday = c.Birthday,
                BirthdateGregorian = c.BirthdateGregorian,
                BirthdateHijri = c.BirthdateHijri,
                GuestTypeName = c.GuestType?.GtypeName,
                NationalityName = c.Nationality?.NName,
                NationalityNameAr = c.Nationality?.NNameAr,
                GuestCategoryName = c.GuestCategory?.GcName,
                Identifications = (c.Identifications ?? Array.Empty<CustomerIdentification>())
                    .OrderByDescending(i => i.IsPrimary)
                    .ThenBy(i => i.IdentificationId)
                    .Select(i => new CustomerIdentificationResponseDto
                    {
                        IdentificationId = i.IdentificationId,
                        IdTypeId = i.IdTypeId,
                        IdTypeName = i.IdType?.ItName,
                        IdTypeNameAr = i.IdType?.ItNameAr,
                        IdNumber = i.IdNumber,
                        VersionNumber = i.VersionNumber,
                        IsPrimary = i.IsPrimary
                    })
                    .ToList()
            };

            return dto;
        }

        public async Task<CustomerResponseDto?> GetCustomerByIdAsync(int customerId)
        {
            // NOTE: Do NOT rely on EF navigation Includes here because ApplicationDbContext ignores
            // several navigations to avoid shadow property issues. Load related display values manually.
            var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);
            if (customer == null)
            {
                return null;
            }

            var guestType = customer.GtypeId.HasValue
                ? await _unitOfWork.GuestTypes.GetByIdAsync(customer.GtypeId.Value)
                : null;
            var nationality = customer.NId.HasValue
                ? await _unitOfWork.Nationalities.GetByIdAsync(customer.NId.Value)
                : null;
            var guestCategory = customer.GuestCategoryId.HasValue
                ? await _unitOfWork.GuestCategories.GetByIdAsync(customer.GuestCategoryId.Value)
                : null;

            List<CustomerIdentification> identRows = new();
            var idTypeMap = new Dictionary<int, IdType>();
            try
            {
                identRows = (await _customerIdentificationRepository.GetByCustomerIdAsync(customerId)).ToList();
                var idTypeIds = identRows.Select(i => i.IdTypeId).Distinct().ToList();
                foreach (var idTypeId in idTypeIds)
                {
                    try
                    {
                        var t = await _unitOfWork.IdTypes.GetByIdAsync(idTypeId);
                        if (t != null)
                        {
                            idTypeMap[idTypeId] = t;
                        }
                    }
                    catch
                    {
                        /* skip invalid id type row */
                    }
                }
            }
            catch
            {
                identRows = new List<CustomerIdentification>();
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

        public async Task<CustomerResponseDto?> GetCustomerByNoAsync(string customerNo)
        {
            var customer = await _customerRepository.GetByCustomerNoAsync(customerNo);
            return customer != null ? _mapper.Map<CustomerResponseDto>(customer) : null;
        }

        public async Task<CustomerResponseDto> CreateCustomerAsync(CreateCustomerDto createCustomerDto)
        {
            // Check if customer number already exists
            if (!string.IsNullOrEmpty(createCustomerDto.CustomerNo))
            {
                var existingCustomer = await _customerRepository.GetByCustomerNoAsync(createCustomerDto.CustomerNo);
                if (existingCustomer != null)
                {
                    throw new InvalidOperationException($"Customer with number '{createCustomerDto.CustomerNo}' already exists.");
                }
            }

            var customer = _mapper.Map<Customer>(createCustomerDto);
            customer.HotelId = await ResolveOperationalHotelIdAsync(createCustomerDto.HotelId);

            customer.EnteredAt = KsaTime.Now;

            var createdCustomer = await _customerRepository.AddAsync(customer);
            await _unitOfWork.SaveChangesAsync();

            // Create customer identifications
            if (createCustomerDto.Identifications != null && createCustomerDto.Identifications.Any())
            {
                foreach (var identificationDto in createCustomerDto.Identifications)
                {
                    var identification = new CustomerIdentification
                    {
                        CustomerId = createdCustomer.CustomerId,
                        IdTypeId = identificationDto.IdTypeId,
                        IdNumber = identificationDto.IdNumber,
                        VersionNumber = identificationDto.VersionNumber,
                        IssuePlace = identificationDto.IssuePlace,
                        IssuePlaceAr = identificationDto.IssuePlaceAr,
                        IssueDate = identificationDto.IssueDate,
                        ExpiryDate = identificationDto.ExpiryDate,
                        Notes = identificationDto.Notes,
                        IsPrimary = identificationDto.IsPrimary,
                        IsActive = identificationDto.IsActive,
                        CreatedAt = KsaTime.Now,
                        UpdatedAt = KsaTime.Now
                    };

                    await _customerIdentificationRepository.AddAsync(identification);
                }
                await _unitOfWork.SaveChangesAsync();
            }

            return await GetCustomerByIdAsync(createdCustomer.CustomerId)
                ?? _mapper.Map<CustomerResponseDto>(createdCustomer);
        }

        public async Task<CustomerResponseDto?> UpdateCustomerAsync(UpdateCustomerDto updateCustomerDto)
        {
            var existingCustomer = await _customerRepository.GetByIdAsync(updateCustomerDto.CustomerId);
            if (existingCustomer == null)
            {
                return null;
            }

            // Check if customer number already exists (excluding current customer)
            if (!string.IsNullOrEmpty(updateCustomerDto.CustomerNo))
            {
                var customerWithSameNo = await _customerRepository.GetByCustomerNoAsync(updateCustomerDto.CustomerNo);
                if (customerWithSameNo != null && customerWithSameNo.CustomerId != updateCustomerDto.CustomerId)
                {
                    throw new InvalidOperationException($"Customer with number '{updateCustomerDto.CustomerNo}' already exists.");
                }
            }

            _mapper.Map(updateCustomerDto, existingCustomer);
            
            var updatedCustomer = await _customerRepository.UpdateAsync(existingCustomer);
            await _unitOfWork.SaveChangesAsync();

            if (updateCustomerDto.Identifications != null)
            {
                var existingIdents = (await _customerIdentificationRepository.GetByCustomerIdAsync(updateCustomerDto.CustomerId)).ToList();
                foreach (var row in existingIdents)
                {
                    await _customerIdentificationRepository.DeleteAsync(row);
                }

                await _unitOfWork.SaveChangesAsync();

                var toAdd = updateCustomerDto.Identifications
                    .Where(i => i.IdTypeId > 0 && !string.IsNullOrWhiteSpace(i.IdNumber))
                    .ToList();

                for (var idx = 0; idx < toAdd.Count; idx++)
                {
                    var identificationDto = toAdd[idx];
                    var identification = new CustomerIdentification
                    {
                        CustomerId = updateCustomerDto.CustomerId,
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
                    };

                    await _customerIdentificationRepository.AddAsync(identification);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            return await GetCustomerByIdAsync(updateCustomerDto.CustomerId);
        }

        public async Task<bool> DeleteCustomerAsync(int customerId)
        {
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
            {
                return false;
            }

            await _customerRepository.DeleteAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CustomerResponseDto>> SearchCustomersAsync(string searchTerm)
        {
            var customers = await _customerRepository.SearchByNameAsync(searchTerm);
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetCustomersByNationalityAsync(int nationalityId)
        {
            var customers = await _customerRepository.GetByNationalityAsync(nationalityId);
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetCustomersByGuestTypeAsync(int guestTypeId)
        {
            var customers = await _customerRepository.GetByGuestTypeAsync(guestTypeId);
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetCustomersByGuestCategoryAsync(int guestCategoryId)
        {
            var customers = await _customerRepository.GetByGuestCategoryAsync(guestCategoryId);
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
        }

        public async Task<IEnumerable<CustomerResponseDto>> GetCustomersByDateRangeAsync(DateTime fromDate, DateTime toDate)
        {
            var customers = await _customerRepository.GetByDateRangeAsync(fromDate, toDate);
            return _mapper.Map<IEnumerable<CustomerResponseDto>>(customers);
        }

        public async Task<object> GetCustomerStatisticsAsync()
        {
            return await _customerRepository.GetCustomerStatisticsAsync();
        }

        public async Task<bool> CustomerExistsAsync(int customerId)
        {
            return await _customerRepository.ExistsAsync(c => c.CustomerId == customerId);
        }

        public async Task<bool> CustomerNoExistsAsync(string customerNo, int? excludeCustomerId = null)
        {
            var customer = await _customerRepository.GetByCustomerNoAsync(customerNo);
            if (customer == null)
                return false;

            if (excludeCustomerId.HasValue)
                return customer.CustomerId != excludeCustomerId.Value;

            return true;
        }

        /// <summary>
        /// Resolves the operational hotel_id stored on customer rows within the current tenant database.
        /// Rejects client-supplied ids that do not map to hotel_settings.
        /// </summary>
        private async Task<int> ResolveOperationalHotelIdAsync(int? requestedHotelId)
        {
            var settings = (await _unitOfWork.HotelSettings.GetAllAsync()).ToList();
            if (settings.Count == 0)
            {
                throw new InvalidOperationException("Hotel settings are not configured for this tenant.");
            }

            var allowedKeys = new HashSet<int>();
            foreach (var setting in settings)
            {
                if (setting.HotelId > 0)
                {
                    allowedKeys.Add(setting.HotelId);
                }

                if (setting.ZaaerId is > 0)
                {
                    allowedKeys.Add(setting.ZaaerId.Value);
                }
            }

            if (requestedHotelId is > 0)
            {
                if (!allowedKeys.Contains(requestedHotelId.Value))
                {
                    throw new UnauthorizedAccessException("The requested hotel is not valid for this tenant.");
                }

                return requestedHotelId.Value;
            }

            var primary = settings.OrderBy(h => h.HotelId).First();
            if (primary.ZaaerId is > 0)
            {
                return primary.ZaaerId.Value;
            }

            return primary.HotelId;
        }
    }
}
