using AutoMapper;
using FinanceLedgerAPI.Models;
using zaaerIntegration.DTOs.Request;
using zaaerIntegration.DTOs.Response;

namespace zaaerIntegration.Configuration
{
    /// <summary>
    /// AutoMapper Profile
    /// ��� AutoMapper
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Customer mappings
            CreateMap<Customer, CustomerResponseDto>()
                .ForMember(dest => dest.GuestTypeName, opt => opt.MapFrom(src => src.GuestType != null ? src.GuestType.GtypeName : null))
                .ForMember(dest => dest.NationalityName, opt => opt.MapFrom(src => src.Nationality != null ? src.Nationality.NName : null))
                .ForMember(dest => dest.NationalityNameAr, opt => opt.MapFrom(src => src.Nationality != null ? src.Nationality.NNameAr : null))
                .ForMember(dest => dest.GuestCategoryName, opt => opt.MapFrom(src => src.GuestCategory != null ? src.GuestCategory.GcName : null))
                .ForMember(dest => dest.Identifications, opt => opt.Ignore());

            CreateMap<CreateCustomerDto, Customer>()
                .ForMember(dest => dest.CustomerId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelId, opt => opt.Ignore())
                .ForMember(dest => dest.EnteredBy, opt => opt.Ignore())
                .ForMember(dest => dest.EnteredAt, opt => opt.Ignore())
                .ForMember(dest => dest.GuestType, opt => opt.Ignore())
                .ForMember(dest => dest.Nationality, opt => opt.Ignore())
                .ForMember(dest => dest.GuestCategory, opt => opt.Ignore())
                .ForMember(dest => dest.Identifications, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAccounts, opt => opt.Ignore());

            CreateMap<UpdateCustomerDto, Customer>()
                .ForMember(dest => dest.EnteredBy, opt => opt.Ignore())
                .ForMember(dest => dest.EnteredAt, opt => opt.Ignore())
                .ForMember(dest => dest.GuestType, opt => opt.Ignore())
                .ForMember(dest => dest.Nationality, opt => opt.Ignore())
                .ForMember(dest => dest.GuestCategory, opt => opt.Ignore())
                .ForMember(dest => dest.Identifications, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAccounts, opt => opt.Ignore());

            // HotelSettings Settings mappings
            CreateMap<HotelSettings, HotelResponseDto>();
            CreateMap<CreateHotelDto, HotelSettings>()
                .ForMember(dest => dest.HotelId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Buildings, opt => opt.Ignore())
                .ForMember(dest => dest.Customers, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAccounts, opt => opt.Ignore())
                .ForMember(dest => dest.RoomTypes, opt => opt.Ignore());

            CreateMap<UpdateHotelDto, HotelSettings>()
                .ForMember(dest => dest.HotelId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Buildings, opt => opt.Ignore())
                .ForMember(dest => dest.Customers, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAccounts, opt => opt.Ignore())
                .ForMember(dest => dest.RoomTypes, opt => opt.Ignore());

            // Reservation mappings
            CreateMap<Reservation, ReservationResponseDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.StatusEnum))
                .ForMember(dest => dest.StatusWord, opt => opt.MapFrom(src => src.StatusWord))
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null))
                .ForMember(dest => dest.CustomerName, opt => opt.Ignore()) // Will be populated by service
                .ForMember(dest => dest.CorporateName, opt => opt.MapFrom(src => src.CorporateCustomer != null ? src.CorporateCustomer.CorporateName : null))
                .ForMember(dest => dest.VisitPurposeName, opt => opt.MapFrom(src => src.VisitPurpose != null ? src.VisitPurpose.VpName : null));

            CreateMap<CreateReservationDto, Reservation>()
                .ForMember(dest => dest.ReservationId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.CorporateCustomer, opt => opt.Ignore())
                .ForMember(dest => dest.VisitPurpose, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnits, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore());

            CreateMap<UpdateReservationDto, Reservation>()
                .ForMember(dest => dest.ReservationId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.CorporateCustomer, opt => opt.Ignore())
                .ForMember(dest => dest.VisitPurpose, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnits, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore());

            // Invoice mappings
            CreateMap<Invoice, InvoiceResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerName, opt => opt.Ignore());

            CreateMap<CreateInvoiceDto, Invoice>()
                .ForMember(dest => dest.InvoiceId, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnit, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerTransactions, opt => opt.Ignore());

            CreateMap<UpdateInvoiceDto, Invoice>()
                .ForMember(dest => dest.InvoiceId, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnit, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerTransactions, opt => opt.Ignore());

            // Payment Receipt mappings
            CreateMap<PaymentReceipt, PaymentReceiptResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerName, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentMethodName, opt => opt.Ignore())
                .ForMember(dest => dest.BankName, opt => opt.Ignore());

            CreateMap<CreatePaymentReceiptDto, PaymentReceipt>()
                .ForMember(dest => dest.ReceiptId, opt => opt.Ignore())
                .ForMember(dest => dest.ReceiptDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnit, opt => opt.Ignore())
                .ForMember(dest => dest.Invoice, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentMethodNavigation, opt => opt.Ignore())
                .ForMember(dest => dest.BankNavigation, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerTransactions, opt => opt.Ignore());

            CreateMap<UpdatePaymentReceiptDto, PaymentReceipt>()
                .ForMember(dest => dest.ReceiptId, opt => opt.Ignore())
                .ForMember(dest => dest.ReceiptDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnit, opt => opt.Ignore())
                .ForMember(dest => dest.Invoice, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentMethodNavigation, opt => opt.Ignore())
                .ForMember(dest => dest.BankNavigation, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerTransactions, opt => opt.Ignore());

            // Corporate Customer mappings
            CreateMap<CorporateCustomer, CorporateCustomerResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null));

            CreateMap<CreateCorporateCustomerDto, CorporateCustomer>()
                .ForMember(dest => dest.CorporateId, opt => opt.Ignore())
                .ForMember(dest => dest.ZaaerId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore());

            CreateMap<UpdateCorporateCustomerDto, CorporateCustomer>()
                .ForMember(dest => dest.CorporateId, opt => opt.Ignore())
                .ForMember(dest => dest.ZaaerId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Reservations, opt => opt.Ignore());

            // Reservation Unit mappings
            CreateMap<ReservationUnit, ReservationUnitResponseDto>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.StatusEnum))
                .ForMember(dest => dest.StatusWord, opt => opt.MapFrom(src => src.StatusWord))
                .ForMember(dest => dest.ReservationNo, opt => opt.MapFrom(src => src.Reservation != null ? src.Reservation.ReservationNo : null))
                .ForMember(dest => dest.ApartmentName, opt => opt.MapFrom(src => src.Apartment != null ? src.Apartment.ApartmentName : null))
                .ForMember(dest => dest.ApartmentCode, opt => opt.MapFrom(src => src.Apartment != null ? src.Apartment.ApartmentCode : null))
                .ForMember(dest => dest.BuildingName, opt => opt.MapFrom(src => src.Apartment != null && src.Apartment.Building != null ? src.Apartment.Building.BuildingName : null))
                .ForMember(dest => dest.FloorName, opt => opt.MapFrom(src => src.Apartment != null && src.Apartment.Floor != null ? src.Apartment.Floor.FloorName : null))
                .ForMember(dest => dest.RoomTypeName, opt => opt.MapFrom(src => src.Apartment != null && src.Apartment.RoomType != null ? src.Apartment.RoomType.RoomTypeName : null))
                .ForMember(dest => dest.CustomerName, opt => opt.Ignore())
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.Reservation != null && src.Reservation.HotelSettings != null ? src.Reservation.HotelSettings.HotelName : null));

            CreateMap<CreateReservationUnitDto, ReservationUnit>()
                .ForMember(dest => dest.UnitId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.Apartment, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore());

            CreateMap<UpdateReservationUnitDto, ReservationUnit>()
                .ForMember(dest => dest.UnitId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Reservation, opt => opt.Ignore())
                .ForMember(dest => dest.Apartment, opt => opt.Ignore())
                .ForMember(dest => dest.Invoices, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentReceipts, opt => opt.Ignore())
                .ForMember(dest => dest.Refunds, opt => opt.Ignore());

            // Apartment mappings
            CreateMap<Apartment, ApartmentResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null))
                .ForMember(dest => dest.BuildingName, opt => opt.MapFrom(src => src.Building != null ? src.Building.BuildingName : null))
                .ForMember(dest => dest.FloorName, opt => opt.MapFrom(src => src.Floor != null ? src.Floor.FloorName : null))
                .ForMember(dest => dest.RoomTypeName, opt => opt.MapFrom(src => src.RoomType != null ? src.RoomType.RoomTypeName : null))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.ReservationUnits != null ? src.ReservationUnits.Count : 0))
                .ForMember(dest => dest.TotalRevenue, opt => opt.MapFrom(src => src.ReservationUnits != null ? src.ReservationUnits.Sum(ru => ru.TotalAmount) : 0))
                .ForMember(dest => dest.IsAvailable, opt => opt.MapFrom(src => src.Status == "available"));

            CreateMap<CreateApartmentDto, Apartment>()
                .ForMember(dest => dest.ApartmentId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Building, opt => opt.Ignore())
                .ForMember(dest => dest.Floor, opt => opt.Ignore())
                .ForMember(dest => dest.RoomType, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnits, opt => opt.Ignore());

            CreateMap<UpdateApartmentDto, Apartment>()
                .ForMember(dest => dest.ApartmentId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Building, opt => opt.Ignore())
                .ForMember(dest => dest.Floor, opt => opt.Ignore())
                .ForMember(dest => dest.RoomType, opt => opt.Ignore())
                .ForMember(dest => dest.ReservationUnits, opt => opt.Ignore());

            // Building mappings
            CreateMap<Building, BuildingResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null))
                .ForMember(dest => dest.TotalFloors, opt => opt.MapFrom(src => src.Floors != null ? src.Floors.Count : 0))
                .ForMember(dest => dest.TotalApartments, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Count : 0))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Count : 0) : 0))
                .ForMember(dest => dest.TotalRevenue, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Sum(ru => ru.TotalAmount) : 0) : 0));

            CreateMap<CreateBuildingDto, Building>()
                .ForMember(dest => dest.BuildingId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Floors, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            CreateMap<UpdateBuildingDto, Building>()
                .ForMember(dest => dest.BuildingId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Floors, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            // Floor mappings
            CreateMap<Floor, FloorResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null))
                .ForMember(dest => dest.TotalApartments, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Count : 0))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Count : 0) : 0))
                .ForMember(dest => dest.TotalRevenue, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Sum(ru => ru.TotalAmount) : 0) : 0));

            CreateMap<CreateFloorDto, Floor>()
                .ForMember(dest => dest.FloorId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            CreateMap<UpdateFloorDto, Floor>()
                .ForMember(dest => dest.FloorId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            CreateMap<CreateFloorItemDto, Floor>()
                .ForMember(dest => dest.FloorId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            CreateMap<UpdateFloorItemDto, Floor>()
                .ForMember(dest => dest.FloorId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            // RoomType mappings
            CreateMap<RoomType, RoomTypeResponseDto>()
                .ForMember(dest => dest.HotelName, opt => opt.MapFrom(src => src.HotelSettings != null ? src.HotelSettings.HotelName : null))
                .ForMember(dest => dest.TotalApartments, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Count : 0))
                .ForMember(dest => dest.TotalReservations, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Count : 0) : 0))
                .ForMember(dest => dest.TotalRevenue, opt => opt.MapFrom(src => src.Apartments != null ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Sum(ru => ru.TotalAmount) : 0) : 0))
                .ForMember(dest => dest.AverageRevenue, opt => opt.MapFrom(src => src.Apartments != null && src.Apartments.Any() ? src.Apartments.Sum(a => a.ReservationUnits != null ? a.ReservationUnits.Sum(ru => ru.TotalAmount) : 0) / src.Apartments.Count : 0))
                .ForMember(dest => dest.OccupancyRate, opt => opt.MapFrom(src => 0)) // Will be calculated separately
                .ForMember(dest => dest.AverageStayDuration, opt => opt.MapFrom(src => 0)); // Will be calculated separately

            CreateMap<CreateRoomTypeDto, RoomType>()
                .ForMember(dest => dest.RoomTypeId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

            CreateMap<UpdateRoomTypeDto, RoomType>()
                .ForMember(dest => dest.RoomTypeId, opt => opt.Ignore())
                .ForMember(dest => dest.HotelSettings, opt => opt.Ignore())
                .ForMember(dest => dest.Apartments, opt => opt.Ignore());

        }
    }
}
