using Microsoft.EntityFrameworkCore;
using FinanceLedgerAPI.Models;
using zaaerIntegration.Models;

namespace zaaerIntegration.Data
{
    /// <summary>
    /// Application Database Context
    /// سياق قاعدة البيانات للتطبيق
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Apartment> Apartments { get; set; }
        public DbSet<Building> Buildings { get; set; }
        public DbSet<Floor> Floors { get; set; }
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<RoomType> RoomTypes { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<PaymentReceipt> PaymentReceipts { get; set; }
        public DbSet<PromissoryNote> PromissoryNotes { get; set; }
        public DbSet<InvoiceReceiptMapping> InvoiceReceiptMappings { get; set; }
        public DbSet<CorporateCustomer> CorporateCustomers { get; set; }
        public DbSet<GuestType> GuestTypes { get; set; }
        public DbSet<GuestCategory> GuestCategories { get; set; }
        public DbSet<CustomerRelation> CustomerRelations { get; set; }
        public DbSet<Nationality> Nationalities { get; set; }
        public DbSet<IdType> IdTypes { get; set; }
        public DbSet<VisitPurpose> VisitPurposes { get; set; }
        public DbSet<ReservationSource> ReservationSources { get; set; }
        public DbSet<RoomCardColorSetting> RoomCardColorSettings { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Bank> Banks { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<CashLedgerEntry> CashLedgerEntries { get; set; }
        public DbSet<CashOpeningBalance> CashOpeningBalances { get; set; }
        public DbSet<ExpenseRoom> ExpenseRooms { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<ExpenseImage> ExpenseImages { get; set; }
        public DbSet<DepositImage> DepositImages { get; set; }
        public DbSet<ExpenseCompany> ExpenseCompanies { get; set; }
        public DbSet<ExpenseApprovalHistory> ExpenseApprovalHistories { get; set; }
        public DbSet<ReservationUnit> ReservationUnits { get; set; }
        /// <summary>Pricing periods planned for flexible mixed rental reservations.</summary>
        public DbSet<ReservationPeriod> ReservationPeriods { get; set; }
        public DbSet<ReservationCompanion> ReservationCompanions { get; set; }
        public DbSet<ReservationExtra> ReservationExtras { get; set; }
        public DbSet<ReservationPackage> ReservationPackages { get; set; }
        public DbSet<CustomerAccount> CustomerAccounts { get; set; }
        public DbSet<CustomerTransaction> CustomerTransactions { get; set; }
        public DbSet<CustomerIdentification> CustomerIdentifications { get; set; }
        public DbSet<Discount> Discounts { get; set; }
        public DbSet<ReservationNote> ReservationNotes { get; set; }
        public DbSet<Penalty> Penalties { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<CreditNote> CreditNotes { get; set; }
        public DbSet<HotelSettings> HotelSettings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<RoomTypeRate> RoomTypeRates { get; set; }
        public DbSet<RoomTypeDailyRate> RoomTypeDailyRates { get; set; }
        public DbSet<SeasonalRate> SeasonalRates { get; set; }
        public DbSet<SeasonalRateItem> SeasonalRateItems { get; set; }
        public DbSet<ZatcaDetails> ZatcaDetails { get; set; }
        public DbSet<ZatcaDevice> ZatcaDevices { get; set; }
        public DbSet<ZatcaInvoiceHashHistory> ZatcaInvoiceHashHistory { get; set; }
        public DbSet<DebitNote> DebitNotes { get; set; }
        public DbSet<NtmpDetails> NtmpDetails { get; set; }
        public DbSet<ShomoosDetails> ShomoosDetails { get; set; }
        public DbSet<PartnerQueue> PartnerRequestQueue { get; set; }
        public DbSet<PartnerRequestLog> PartnerRequestLog { get; set; }
        public DbSet<IntegrationResponse> IntegrationResponses { get; set; }
        public DbSet<ReservationUnitDayRate> ReservationUnitDayRates { get; set; }
        public DbSet<ReservationUnitSwitch> ReservationUnitSwitches { get; set; }
        public DbSet<ReservationUnitSwitch> ReservationUnitSwaps { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<RateType> RateTypes { get; set; }
        public DbSet<RateTypeUnitItem> RateTypeUnitItems { get; set; }
        public DbSet<Maintenance> Maintenances { get; set; }
        public DbSet<BookingEngineSettings> BookingEngineSettings { get; set; }
        public DbSet<BookingEngineMedia> BookingEngineMedia { get; set; }
        public DbSet<BookingEngineCoupon> BookingEngineCoupons { get; set; }
        public DbSet<BookingEngineAvailabilityOverride> BookingEngineAvailabilityOverrides { get; set; }
        public DbSet<ResortTicketType> ResortTicketTypes { get; set; }
        public DbSet<ResortTicketOrder> ResortTicketOrders { get; set; }
        public DbSet<ResortTicket> ResortTickets { get; set; }
        public DbSet<ResortTicketEvent> ResortTicketEvents { get; set; }
        public DbSet<ResortTicketConfig> ResortTicketConfigs { get; set; }
        public DbSet<ReservationEventProfile> ReservationEventProfiles { get; set; }
        public DbSet<EventFunctionSheet> EventFunctionSheets { get; set; }
        public DbSet<EventFunctionSheetItem> EventFunctionSheetItems { get; set; }
        public DbSet<HallEventAlert> HallEventAlerts { get; set; }
        public DbSet<Tax> Taxes { get; set; }
        public DbSet<TaxCategory> TaxCategories { get; set; }
        public DbSet<InvoiceJournalEntry> InvoiceJournalEntries { get; set; }
        public DbSet<PaymentReceiptJournalEntry> PaymentReceiptJournalEntries { get; set; }
        public DbSet<CreditNoteJournalEntry> CreditNoteJournalEntries { get; set; }
        
        // Orders and Outlets related DbSets
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Outlet> Outlets { get; set; }
        public DbSet<OutletCategory> OutletCategories { get; set; }
        public DbSet<OutletItem> OutletItems { get; set; }
        public DbSet<OutletTable> OutletTables { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ignore all problematic navigation properties globally
            IgnoreProblematicNavigationProperties(modelBuilder);

            // Configure relationships and constraints
            ConfigureCustomerRelationships(modelBuilder);
            ConfigureReservationRelationships(modelBuilder);
            ConfigureReservationPeriods(modelBuilder);
            ConfigureReservationUnitRelationships(modelBuilder);
            ConfigureReservationCompanionRelationships(modelBuilder);
            ConfigureReservationExtraRelationships(modelBuilder);
            ConfigureReservationPackageRelationships(modelBuilder);
            ConfigureInvoiceRelationships(modelBuilder);
            ConfigurePaymentRelationships(modelBuilder);
            ConfigureRefundRelationships(modelBuilder);
            ConfigureCreditNoteRelationships(modelBuilder);
            ConfigureApartmentRelationships(modelBuilder);
            ConfigureBuildingRelationships(modelBuilder);
            ConfigureFloorRelationships(modelBuilder);
            ConfigureFacilityRelationships(modelBuilder);
            ConfigureRoomTypeRelationships(modelBuilder);
            ConfigureCorporateCustomerRelationships(modelBuilder);
            ConfigureUserRelationships(modelBuilder);
            ConfigureRoleRelationships(modelBuilder);
            ConfigurePermissionRelationships(modelBuilder);
            ConfigureRoomTypeRateRelationships(modelBuilder);
            ConfigureRoomTypeDailyRateRelationships(modelBuilder);
            ConfigureBookingEngineAvailabilityOverride(modelBuilder);
            ConfigureResortTickets(modelBuilder);
            ConfigureSeasonalRateRelationships(modelBuilder);
            ConfigureZatcaDetails(modelBuilder);
            ConfigureZatcaDevices(modelBuilder);
            ConfigureDebitNotes(modelBuilder);
            ConfigureNtmpDetails(modelBuilder);
            ConfigureShomoosDetails(modelBuilder);
            ConfigureExpenseRelationships(modelBuilder);
            ConfigureIntegrationResponse(modelBuilder);
            ConfigureReservationUnitDayRates(modelBuilder);
            ConfigureReservationUnitSwitches(modelBuilder);
            ConfigureReservationUnitSwaps(modelBuilder);
            ConfigureActivityLogs(modelBuilder);
            ConfigureRateTypes(modelBuilder);
            ConfigureTaxes(modelBuilder);
            ConfigureInvoiceReceiptMappingRelationships(modelBuilder);
            ConfigureInvoiceJournalEntryRelationships(modelBuilder);
            ConfigureOrdersRelationships(modelBuilder);

            // Configure HotelSettings entity to ensure nullable properties are properly configured
            ConfigureHotelSettings(modelBuilder);
            ConfigureSqlServerTriggerAwareTables(modelBuilder);

            // partner queue entities
            modelBuilder.Entity<PartnerQueue>(entity =>
            {
                entity.HasIndex(e => e.RequestRef).HasDatabaseName("IX_partner_request_queue_request_ref");
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.OperationKey).HasMaxLength(150);
                entity.Property(e => e.PayloadType).HasMaxLength(200);
            });

            modelBuilder.Entity<PartnerRequestLog>(entity =>
            {
                entity.HasIndex(e => e.RequestRef).HasDatabaseName("IX_partner_request_log_request_ref");
                entity.Property(e => e.Status).HasMaxLength(50);
            });
        }

        private void ConfigureResortTickets(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ResortTicketType>(entity =>
            {
                entity.HasIndex(e => new { e.HotelId, e.Code }).HasDatabaseName("IX_resort_ticket_types_hotel_code");
                entity.HasIndex(e => new { e.HotelId, e.ZaaerId }).HasDatabaseName("IX_resort_ticket_types_hotel_zaaer");
            });

            modelBuilder.Entity<ResortTicketOrder>(entity =>
            {
                entity.HasIndex(e => new { e.HotelId, e.OrderDate }).HasDatabaseName("IX_resort_ticket_orders_hotel_date");
                entity.HasIndex(e => new { e.HotelId, e.ReservationId }).HasDatabaseName("IX_resort_ticket_orders_reservation");
                entity.HasIndex(e => new { e.HotelId, e.ZaaerId }).HasDatabaseName("IX_resort_ticket_orders_hotel_zaaer");
            });

            modelBuilder.Entity<ResortTicket>(entity =>
            {
                entity.HasIndex(e => new { e.HotelId, e.TicketOrderId }).HasDatabaseName("IX_resort_tickets_order");
                entity.HasIndex(e => new { e.HotelId, e.QrCode }).HasDatabaseName("IX_resort_tickets_qr");
                entity.HasIndex(e => new { e.HotelId, e.ZaaerId }).HasDatabaseName("IX_resort_tickets_hotel_zaaer");
            });

            modelBuilder.Entity<ResortTicketEvent>(entity =>
            {
                entity.HasIndex(e => new { e.HotelId, e.TicketOrderId, e.CreatedAt }).HasDatabaseName("IX_resort_ticket_events_order");
                entity.HasIndex(e => new { e.HotelId, e.TicketId, e.CreatedAt }).HasDatabaseName("IX_resort_ticket_events_ticket");
            });
        }

        private void ConfigureTaxes(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tax>()
                .Property(t => t.TaxIncluded)
                .HasDefaultValue(true);

            // Tax -> TaxCategory (optional) FK on tax_id
            modelBuilder.Entity<Tax>()
                .HasOne(t => t.TaxCategory)
                .WithMany()
                .HasForeignKey(t => t.TaxId)
                .HasConstraintName("FK_Taxes_TaxCategories")
                .OnDelete(DeleteBehavior.SetNull);

            // Useful indexes
            modelBuilder.Entity<Tax>()
                .HasIndex(t => t.TaxId)
                .HasDatabaseName("IX_Taxes_TaxId");
        }

        private void ConfigureReservationPeriods(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservationPeriod>(entity =>
            {
                entity.Property(e => e.TaxIncluded).HasDefaultValue(true);
                entity.Property(e => e.Status).HasMaxLength(30);
                entity.Property(e => e.RentalType).HasMaxLength(30);

                entity.HasIndex(e => new { e.ReservationId, e.UnitId, e.FromDate, e.ToDate })
                    .HasDatabaseName("IX_ReservationPeriods_Range");
            });
        }

        private void ConfigureHotelSettings(ModelBuilder modelBuilder)
        {
            // Explicitly configure HotelSettings nullable properties
            // This ensures EF Core knows these columns should allow NULL values
            modelBuilder.Entity<HotelSettings>(entity =>
            {
                // All string properties that are nullable in the model
                entity.Property(e => e.HotelCode).IsRequired(false);
                entity.Property(e => e.HotelName).IsRequired(false);
                entity.Property(e => e.DefaultCurrency).IsRequired(false);
                entity.Property(e => e.CompanyName).IsRequired(false);
                entity.Property(e => e.LogoUrl).IsRequired(false); // CRITICAL: This must be nullable
                entity.Property(e => e.Phone).IsRequired(false);
                entity.Property(e => e.Email).IsRequired(false);
                entity.Property(e => e.TaxNumber).IsRequired(false);
                entity.Property(e => e.CrNumber).IsRequired(false);
                entity.Property(e => e.CountryCode).IsRequired(false);
                entity.Property(e => e.City).IsRequired(false);
                entity.Property(e => e.ContactPerson).IsRequired(false);
                entity.Property(e => e.Address).IsRequired(false);
                entity.Property(e => e.Latitude).IsRequired(false);
                entity.Property(e => e.Longitude).IsRequired(false);
                entity.Property(e => e.PropertyType).IsRequired(false);
                entity.Property(e => e.CreatedAt).IsRequired(false);
                entity.Property(e => e.ZaaerId).IsRequired(false);
            });
        }

        /// <summary>
        /// Tenant databases may have audit/numbering triggers on property tables.
        /// Tell EF Core so SaveChanges avoids SQL Server OUTPUT (incompatible with triggers).
        /// Trigger name is a placeholder; any name satisfies EF Core's requirement.
        /// </summary>
        private static void ConfigureSqlServerTriggerAwareTables(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RoomType>()
                .ToTable("room_types", tb => tb.HasTrigger("TR_room_types"));

            modelBuilder.Entity<Building>()
                .ToTable("buildings", tb => tb.HasTrigger("TR_buildings"));

            modelBuilder.Entity<Floor>()
                .ToTable("floors", tb => tb.HasTrigger("TR_floors"));

            modelBuilder.Entity<Apartment>()
                .ToTable("apartments", tb => tb.HasTrigger("TR_apartments"));

            modelBuilder.Entity<Facility>()
                .ToTable("facilities", tb => tb.HasTrigger("TR_facilities"));

            modelBuilder.Entity<HotelSettings>()
                .ToTable("hotel_settings", tb => tb.HasTrigger("TR_hotel_settings"));
        }

        private void IgnoreProblematicNavigationProperties(ModelBuilder modelBuilder)
        {
            // Ignore all navigation properties that cause shadow property issues
            modelBuilder.Entity<Invoice>()
                .Ignore(i => i.Reservation)
                .Ignore(i => i.ReservationUnit)
                .Ignore(i => i.PaymentReceipts)
                .Ignore(i => i.Refunds)
                .Ignore(i => i.CreditNotes)
                .Ignore(i => i.CustomerTransactions);

            modelBuilder.Entity<Reservation>()
                .Ignore(r => r.VisitPurpose)
                .Ignore(r => r.CorporateCustomer)
                .Ignore(r => r.Invoices)
                .Ignore(r => r.PaymentReceipts)
                .Ignore(r => r.Refunds);

            modelBuilder.Entity<ReservationUnit>()
                .Ignore(ru => ru.Apartment)
                .Ignore(ru => ru.Invoices)
                .Ignore(ru => ru.PaymentReceipts)
                .Ignore(ru => ru.Refunds);

            modelBuilder.Entity<PaymentReceipt>()
                .Ignore(pr => pr.Reservation)
                .Ignore(pr => pr.ReservationUnit)
                .Ignore(pr => pr.PaymentMethodNavigation)
                .Ignore(pr => pr.BankNavigation)
                .Ignore(pr => pr.CustomerTransactions);

            modelBuilder.Entity<Refund>()
                .Ignore(r => r.Reservation)
                .Ignore(r => r.ReservationUnit)
                .Ignore(r => r.PaymentMethodNavigation)
                .Ignore(r => r.BankNavigation)
                .Ignore(r => r.CustomerTransactions);

            modelBuilder.Entity<CreditNote>()
                .Ignore(cn => cn.Reservation)
                .Ignore(cn => cn.Invoice); // InvoiceId contains zaaer_id, not invoice_id - cannot use FK navigation

            // unit_id maps to reservation_units.unit_id; navigations cause shadow FK ReservationUnitUnitId
            modelBuilder.Entity<Discount>()
                .Ignore(d => d.Reservation)
                .Ignore(d => d.ReservationUnit);

            modelBuilder.Entity<ReservationNote>()
                .HasIndex(n => new { n.HotelId, n.ReservationId, n.CreatedAt })
                .HasDatabaseName("IX_reservation_notes_reservation");

            modelBuilder.Entity<Penalty>()
                .Ignore(p => p.Reservation)
                .Ignore(p => p.ReservationUnit);

            modelBuilder.Entity<Apartment>()
                .Ignore(a => a.HotelSettings)
                .Ignore(a => a.Building)
                .Ignore(a => a.Floor)
                .Ignore(a => a.RoomType)
                .Ignore(a => a.ReservationUnits);

            modelBuilder.Entity<Building>()
                .Ignore(b => b.HotelSettings)
                .Ignore(b => b.Floors)
                .Ignore(b => b.Apartments);

            modelBuilder.Entity<Floor>()
                .Ignore(f => f.HotelSettings)
                .Ignore(f => f.Apartments);

            modelBuilder.Entity<RoomType>()
                .Ignore(rt => rt.HotelSettings)
                .Ignore(rt => rt.Apartments);

            modelBuilder.Entity<RoomTypeRate>()
                .Ignore(rtr => rtr.RoomType)
                .Ignore(rtr => rtr.HotelSettings);

            // NOTE: RateTypeUnitItem navigation property is NOT ignored here
            // Instead, relationship configuration is removed in ConfigureRateTypeRelationships
            // This allows rate_type_id values that don't exist in rate_types table
            // Manual joins or manual loading can be used in Service if needed

            modelBuilder.Entity<CorporateCustomer>()
                .Ignore(cc => cc.Reservations);

            modelBuilder.Entity<Customer>()
                .Ignore(c => c.GuestType)
                .Ignore(c => c.GuestCategory)
                .Ignore(c => c.Nationality)
                .Ignore(c => c.HotelSettings)
                .Ignore(c => c.Reservations)
                .Ignore(c => c.Invoices)
                .Ignore(c => c.PaymentReceipts)
                .Ignore(c => c.Refunds)
                .Ignore(c => c.CustomerAccounts)
                .Ignore(c => c.CustomerTransactions)
                .Ignore(c => c.Identifications);

        }

        private void ConfigureCustomerRelationships(ModelBuilder modelBuilder)
        {
            // Customer relationships
            modelBuilder.Entity<Customer>()
                .HasOne(c => c.GuestType)
                .WithMany(gt => gt.Customers)
                .HasForeignKey(c => c.GtypeId)
                .HasConstraintName("FK_Customers_GuestTypes")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.Nationality)
                .WithMany(n => n.Customers)
                .HasForeignKey(c => c.NId)
                .HasConstraintName("FK_Customers_Nationalities")
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<Customer>()
                .HasOne(c => c.GuestCategory)
                .WithMany(gc => gc.Customers)
                .HasForeignKey(c => c.GuestCategoryId)
                .HasConstraintName("FK_Customers_GuestCategories")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Customer>()
                .HasOne(c => c.HotelSettings)
                .WithMany(hs => hs.Customers)
                .HasForeignKey(c => c.HotelId)
                .HasConstraintName("FK_Customers_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigureReservationRelationships(ModelBuilder modelBuilder)
        {
            // Reservation relationships
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.HotelSettings)
                .WithMany(hs => hs.Reservations)
                .HasForeignKey(r => r.HotelId)
                .HasConstraintName("FK_Reservations_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne<Customer>()
                .WithMany(c => c.Reservations)
                .HasForeignKey(r => r.CustomerId)
                .IsRequired()
                .HasConstraintName("FK_Reservations_Customers")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .Property(r => r.NtmpSyncedStages)
                .HasDefaultValue(0);

            // Companions use reservation_id / unit_id as Zaaer or internal refs — not FK to reservations / reservation_units.
            modelBuilder.Entity<Reservation>().Ignore(r => r.ReservationCompanions);

            // Note: VisitPurpose and CorporateCustomer relationships are not configured
            // because their navigation properties are ignored to prevent shadow property issues
        }

        private void ConfigureReservationUnitRelationships(ModelBuilder modelBuilder)
        {
            // ReservationUnit relationships
            modelBuilder.Entity<ReservationUnit>()
                .HasOne(ru => ru.Reservation)
                .WithMany(r => r.ReservationUnits)
                .HasForeignKey(ru => ru.ReservationId)
                .HasConstraintName("FK_ReservationUnits_Reservations")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReservationUnit>()
                .HasOne<Apartment>()
                .WithMany(a => a.ReservationUnits)
                .HasForeignKey(ru => ru.ApartmentId)
                .HasConstraintName("FK_ReservationUnits_Apartments")
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureReservationCompanionRelationships(ModelBuilder modelBuilder)
        {
            // reservation_id / customer_id / unit_id hold Zaaer or internal ids — no FK to reservations / customers / reservation_units.

            modelBuilder.Entity<ReservationCompanion>()
                .HasOne<Apartment>()
                .WithMany()
                .HasForeignKey(rc => rc.ApartmentId)
                .HasConstraintName("FK_reservation_companions_apartments")
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ReservationCompanion>()
                .HasOne<CustomerRelation>()
                .WithMany()
                .HasForeignKey(rc => rc.RelationId)
                .HasPrincipalKey(cr => cr.CrId)
                .HasConstraintName("FK_reservation_companions_relations")
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureReservationExtraRelationships(ModelBuilder modelBuilder)
        {
            // reservation_extras.reservation_id is not FK-bound to reservations (stores Zaaer id when set, else internal PK).

            modelBuilder.Entity<ReservationExtra>()
                .HasOne<ReservationUnit>()
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .HasConstraintName("FK_reservation_extras_units")
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ReservationExtra>()
                .HasOne<ReservationPackage>()
                .WithMany()
                .HasForeignKey(e => e.PackageId)
                .HasConstraintName("FK_reservation_extras_packages")
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureReservationPackageRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservationPackage>(entity =>
            {
                entity.HasIndex(e => new { e.HotelId, e.IsActive, e.SortOrder, e.Name })
                    .HasDatabaseName("IX_packages_hotel_active_sort");
            });
        }

        private void ConfigureInvoiceRelationships(ModelBuilder modelBuilder)
        {
            // Invoice relationships
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.HotelSettings)
                .WithMany(hs => hs.Invoices)
                .HasForeignKey(i => i.HotelId)
                .HasConstraintName("FK_Invoices_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Reservation)
                .WithMany()
                .HasForeignKey(i => i.ReservationId)
                .HasConstraintName("FK_Invoices_Reservations")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.ReservationUnit)
                .WithMany()
                .HasForeignKey(i => i.UnitId)
                .HasConstraintName("FK_Invoices_ReservationUnits")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigurePaymentRelationships(ModelBuilder modelBuilder)
        {
            // Payment Receipt relationships
            modelBuilder.Entity<PaymentReceipt>()
                .HasOne(pr => pr.HotelSettings)
                .WithMany(hs => hs.PaymentReceipts)
                .HasForeignKey(pr => pr.HotelId)
                .HasConstraintName("FK_PaymentReceipts_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaymentReceipt>()
                .HasOne(pr => pr.Reservation)
                .WithMany()
                .HasForeignKey(pr => pr.ReservationId)
                .HasConstraintName("FK_PaymentReceipts_Reservations")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PaymentReceipt>()
                .HasOne(pr => pr.Invoice)
                .WithMany(i => i.PaymentReceipts)
                .HasForeignKey(pr => pr.InvoiceId)
                .HasConstraintName("FK_PaymentReceipts_Invoices")
                .OnDelete(DeleteBehavior.Restrict);

            // Note: PaymentMethodNavigation and BankNavigation relationships are not configured
            // because their navigation properties are ignored to prevent shadow property issues
        }

        private void ConfigureRefundRelationships(ModelBuilder modelBuilder)
        {
            // Refund relationships
            modelBuilder.Entity<Refund>()
                .HasOne(r => r.HotelSettings)
                .WithMany(hs => hs.Refunds)
                .HasForeignKey(r => r.HotelId)
                .HasConstraintName("FK_Refunds_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Refund>()
                .HasOne(r => r.Reservation)
                .WithMany()
                .HasForeignKey(r => r.ReservationId)
                .HasConstraintName("FK_Refunds_Reservations")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Refund>()
                .HasOne(r => r.Invoice)
                .WithMany(i => i.Refunds)
                .HasForeignKey(r => r.InvoiceId)
                .HasConstraintName("FK_Refunds_Invoices")
                .OnDelete(DeleteBehavior.Restrict);

            // Note: PaymentMethodNavigation and BankNavigation relationships are not configured
            // because their navigation properties are ignored to prevent shadow property issues
        }

        private void ConfigureApartmentRelationships(ModelBuilder modelBuilder)
        {
            // Apartment relationships
            modelBuilder.Entity<Apartment>()
                .HasOne(a => a.HotelSettings)
                .WithMany(hs => hs.Apartments)
                .HasForeignKey(a => a.HotelId)
                .HasConstraintName("FK_Apartments_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // Note: Building relationship is removed to allow building_id = 0 without FK issues
            // modelBuilder.Entity<Apartment>()
            //     .HasOne(a => a.Building)
            //     .WithMany(b => b.Apartments)
            //     .HasForeignKey(a => a.BuildingId)
            //     .HasConstraintName("FK_Apartments_Buildings")
            //     .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Apartment>()
                .HasOne(a => a.Floor)
                .WithMany(f => f.Apartments)
                .HasForeignKey(a => a.FloorId)
                .HasConstraintName("FK_Apartments_Floors")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Apartment>()
                .HasOne(a => a.RoomType)
                .WithMany(rt => rt.Apartments)
                .HasForeignKey(a => a.RoomTypeId)
                .HasConstraintName("FK_Apartments_RoomTypes")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigureExpenseRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.Property(e => e.ExpenseId).ValueGeneratedNever();
                entity.Property(e => e.OldExpenseId).ValueGeneratedOnAdd();
                entity.Property(e => e.LocalExpenseId).ValueGeneratedNever();
            });

            // Expense -> HotelSettings
            // Keep FK on internal HotelId PK; logical mapping by ZaaerId is handled in services.
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.HotelSettings)
                .WithMany()
                .HasForeignKey(e => e.HotelId)
                .HasConstraintName("FK_Expenses_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Expense -> ExpenseCategory
            // ⚠️ NOTE: ExpenseCategoryId refers to Master DB ExpenseCategories, NOT Tenant DB
            // Therefore, we CANNOT create a Foreign Key constraint here
            // The ExpenseCategoryId is just a reference ID to Master DB

            // Expense -> ExpenseRooms (One-to-Many)
            modelBuilder.Entity<Expense>()
                .HasMany(e => e.ExpenseRooms)
                .WithOne(er => er.Expense)
                .HasForeignKey(er => er.ExpenseId)
                .HasConstraintName("FK_ExpenseRooms_Expenses")
                .OnDelete(DeleteBehavior.Cascade);

            // Expense -> ExpenseImages (One-to-Many)
            modelBuilder.Entity<Expense>()
                .HasMany(e => e.ExpenseImages)
                .WithOne(ei => ei.Expense)
                .HasForeignKey(ei => ei.ExpenseId)
                .HasConstraintName("FK_ExpenseImages_Expenses")
                .OnDelete(DeleteBehavior.Cascade);

            // Expense -> ExpenseCompany (One-to-One, optional)
            modelBuilder.Entity<Expense>()
                .HasOne(e => e.ExpenseCompany)
                .WithOne(ec => ec.Expense)
                .HasForeignKey<ExpenseCompany>(ec => ec.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ExpenseCompany>(entity =>
            {
                entity.Property(e => e.OldExpenseId).ValueGeneratedNever();
            });

            modelBuilder.Entity<ExpenseImage>(entity =>
            {
                entity.Property(e => e.OldExpenseId).ValueGeneratedNever();
            });

            modelBuilder.Entity<DepositImage>(entity =>
            {
                entity.HasOne(di => di.PaymentReceipt)
                    .WithMany()
                    .HasForeignKey(di => di.ReceiptId)
                    .HasPrincipalKey(pr => pr.ZaaerId!)
                    .HasConstraintName("FK_DepositImages_PaymentReceipts")
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ExpenseApprovalHistory>(entity =>
            {
                entity.Property(e => e.OldExpenseId).ValueGeneratedNever();
            });

            // ExpenseRoom -> Apartment (optional - nullable for room categories)
            // Foreign Key: expense_rooms.zaaer_id -> apartments.zaaer_id
            modelBuilder.Entity<ExpenseRoom>()
                .HasOne(er => er.Apartment)
                .WithMany()
                .HasForeignKey(er => er.ZaaerId)
                .HasPrincipalKey(a => a.ZaaerId) // ✅ Use zaaer_id as principal key instead of apartment_id
                .HasConstraintName("FK_ExpenseRooms_Apartments_ZaaerId")
                .OnDelete(DeleteBehavior.NoAction) // ✅ SQL Server uses NO ACTION instead of RESTRICT
                .IsRequired(false); // ✅ Allow null for room categories

            // ExpenseCategory -> HotelSettings
            modelBuilder.Entity<ExpenseCategory>()
                .HasOne(ec => ec.HotelSettings)
                .WithMany()
                .HasForeignKey(ec => ec.HotelId)
                .HasConstraintName("FK_ExpenseCategories_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureBuildingRelationships(ModelBuilder modelBuilder)
        {
            // Building relationships
            modelBuilder.Entity<Building>()
                .HasOne(b => b.HotelSettings)
                .WithMany(hs => hs.Buildings)
                .HasForeignKey(b => b.HotelId)
                .HasConstraintName("FK_Buildings_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigureFloorRelationships(ModelBuilder modelBuilder)
        {
            // Floor relationships
            modelBuilder.Entity<Floor>()
                .HasOne(f => f.HotelSettings)
                .WithMany(hs => hs.Floors)
                .HasForeignKey(f => f.HotelId)
                .HasConstraintName("FK_Floors_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // building_id may store buildings.zaaer_id — no EF FK to buildings
            modelBuilder.Entity<Floor>()
                .Ignore(f => f.Building);

        }

        private void ConfigureFacilityRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Facility>()
                .HasOne(f => f.HotelSettings)
                .WithMany(hs => hs.Facilities)
                .HasForeignKey(f => f.HotelId)
                .HasConstraintName("FK_facilities_hotel_settings")
                .OnDelete(DeleteBehavior.Restrict);
        }

        private void ConfigureRoomTypeRelationships(ModelBuilder modelBuilder)
        {
            // RoomType relationships
            modelBuilder.Entity<RoomType>()
                .HasOne(rt => rt.HotelSettings)
                .WithMany(hs => hs.RoomTypes)
                .HasForeignKey(rt => rt.HotelId)
                .HasConstraintName("FK_RoomTypes_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigureCorporateCustomerRelationships(ModelBuilder modelBuilder)
        {
            // CorporateCustomer relationships
            modelBuilder.Entity<CorporateCustomer>()
                .HasOne(cc => cc.HotelSettings)
                .WithMany(hs => hs.CorporateCustomers)
                .HasForeignKey(cc => cc.HotelId)
                .HasConstraintName("FK_CorporateCustomers_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

        }

        private void ConfigureCreditNoteRelationships(ModelBuilder modelBuilder)
        {
            // CreditNote relationships
            modelBuilder.Entity<CreditNote>()
                .HasOne(cn => cn.HotelSettings)
                .WithMany(hs => hs.CreditNotes)
                .HasForeignKey(cn => cn.HotelId)
                .HasConstraintName("FK_CreditNotes_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // IMPORTANT: CreditNote.InvoiceId contains invoices.zaaer_id (NOT invoices.invoice_id)
            // Therefore, we CANNOT use a standard Foreign Key constraint here
            // The relationship is: credit_notes.invoice_id = invoices.zaaer_id
            // This is validated by a Check Constraint in the database (see FixCreditNoteInvoiceConstraint.sql)
            // We do NOT configure the Invoice relationship here to avoid FK constraint violations
            
            // Note: Reservation relationship is not configured to prevent shadow property issues
            // The ReservationId foreign key will still work for data integrity
        }

        private void ConfigureUserRelationships(ModelBuilder modelBuilder)
        {
            // User relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.HotelSettings)
                .WithMany()
                .HasForeignKey(u => u.HotelId)
                .HasConstraintName("FK_Users_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // Note: Role relationship removed - Role model is now simplified for Master DB only
            // Tenant DB users still have RoleId foreign key, but Role navigation is not configured
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany()
                .HasForeignKey(u => u.RoleId)
                .HasConstraintName("FK_Users_Roles")
                .OnDelete(DeleteBehavior.SetNull);
        }

        private void ConfigureRoleRelationships(ModelBuilder modelBuilder)
        {
            // Note: Role relationships removed - Role model is now simplified for Master DB only
            // Role is only used in MasterDbContext, not in ApplicationDbContext (tenant DB)
        }

        private void ConfigurePermissionRelationships(ModelBuilder modelBuilder)
        {
            // Permission relationships - no foreign keys needed as permissions are global
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.ToTable("role_permissions");
                entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
                entity.HasOne(e => e.Permission)
                    .WithMany()
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureRoomTypeRateRelationships(ModelBuilder modelBuilder)
        {
            // RoomTypeRate relationships
            modelBuilder.Entity<RoomTypeRate>()
                .HasOne(rtr => rtr.HotelSettings)
                .WithMany()
                .HasForeignKey(rtr => rtr.HotelId)
                .HasConstraintName("FK_RoomTypeRates_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoomTypeRate>()
                .HasOne(rtr => rtr.RoomType)
                .WithMany()
                .HasForeignKey(rtr => rtr.RoomTypeId)
                .HasConstraintName("FK_RoomTypeRates_RoomTypes")
                .OnDelete(DeleteBehavior.Restrict);

            // NOTE: Unique constraint removed to allow multiple rates with same RoomTypeId and HotelId
            // This is required for Zaaer integration which uses zaaer_id as the primary identifier
            // The unique constraint was: IX_RoomTypeRates_RoomTypeId_HotelId
            // Use RemoveUniqueConstraint_RoomTypeRates.sql to drop it from database if it exists
        }

        private void ConfigureRoomTypeDailyRateRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RoomTypeDailyRate>()
                .HasIndex(d => new { d.HotelId, d.RoomTypeId, d.RateDate })
                .IsUnique()
                .HasDatabaseName("UX_RoomTypeDailyRates_Hotel_RoomType_Date");
        }

        private void ConfigureBookingEngineAvailabilityOverride(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BookingEngineAvailabilityOverride>()
                .HasIndex(o => new { o.HotelId, o.RoomTypeId, o.RateDate })
                .IsUnique()
                .HasDatabaseName("UX_BeAvailOverride_Hotel_RoomType_Date");
        }

        private void ConfigureSeasonalRateRelationships(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SeasonalRate>()
                .HasOne(sr => sr.HotelSettings)
                .WithMany()
                .HasForeignKey(sr => sr.HotelId)
                .HasConstraintName("FK_SeasonalRates_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SeasonalRateItem>()
                .HasOne(i => i.Season)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.SeasonId)
                .HasConstraintName("FK_SeasonalRateItems_SeasonalRates")
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SeasonalRateItem>()
                .HasOne(i => i.RoomType)
                .WithMany()
                .HasForeignKey(i => i.RoomTypeId)
                .HasConstraintName("FK_SeasonalRateItems_RoomTypes")
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure single item per room type per season
            modelBuilder.Entity<SeasonalRateItem>()
                .HasIndex(i => new { i.SeasonId, i.RoomTypeId })
                .IsUnique()
                .HasDatabaseName("IX_SeasonalRateItems_Season_RoomType");
        }

        private void ConfigureZatcaDetails(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ZatcaDetails>()
                .HasOne(z => z.HotelSettings)
                .WithMany()
                .HasForeignKey(z => z.HotelId)
                .HasConstraintName("FK_ZatcaDetails_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            // optional: one record per hotel (unique)
            modelBuilder.Entity<ZatcaDetails>()
                .HasIndex(z => z.HotelId)
                .IsUnique()
                .HasDatabaseName("IX_ZatcaDetails_HotelId");
        }

        private void ConfigureZatcaDevices(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ZatcaDevice>(entity =>
            {
                entity.HasIndex(d => new { d.HotelId, d.Environment, d.DeviceUuid })
                    .IsUnique()
                    .HasDatabaseName("UQ_zatca_devices_hotel_env_uuid");

                entity.HasIndex(d => new { d.HotelId, d.Environment })
                    .HasDatabaseName("IX_zatca_devices_hotel_env");
            });

            modelBuilder.Entity<ZatcaInvoiceHashHistory>(entity =>
            {
                entity.HasOne(h => h.Device)
                    .WithMany(d => d.HashHistory)
                    .HasForeignKey(h => h.DeviceId)
                    .HasConstraintName("FK_zatca_hash_history_device")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(h => new { h.DeviceId, h.Icv })
                    .HasDatabaseName("IX_zatca_hash_history_device_icv");
            });
        }

        private void ConfigureDebitNotes(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DebitNote>(entity =>
            {
                entity.HasOne(d => d.HotelSettings)
                    .WithMany(h => h.DebitNotes)
                    .HasForeignKey(d => d.HotelId)
                    .HasConstraintName("FK_DebitNotes_HotelSettings")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(d => new { d.DebitNoteNo, d.HotelId })
                    .IsUnique()
                    .HasDatabaseName("UQ_debit_notes_no");

                entity.HasIndex(d => new { d.HotelId, d.ZatcaStatus })
                    .HasDatabaseName("IX_debit_notes_hotel_status");
            });
        }

        private void ConfigureNtmpDetails(ModelBuilder modelBuilder)
        {
            // hotel_id stores Zaaer property id (hotel_settings.zaaer_id), not internal hotel_settings.hotel_id
            modelBuilder.Entity<NtmpDetails>()
                .Ignore(n => n.HotelSettings);

            modelBuilder.Entity<NtmpDetails>()
                .HasIndex(n => n.HotelId)
                .IsUnique()
                .HasDatabaseName("IX_NtmpDetails_HotelId");
        }

        private void ConfigureShomoosDetails(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShomoosDetails>()
                .HasOne(n => n.HotelSettings)
                .WithMany()
                .HasForeignKey(n => n.HotelId)
                .HasConstraintName("FK_ShomoosDetails_HotelSettings")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShomoosDetails>()
                .HasIndex(n => n.HotelId)
                .IsUnique()
                .HasDatabaseName("IX_ShomoosDetails_HotelId");
        }

        private void ConfigureIntegrationResponse(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<IntegrationResponse>(entity =>
            {
                // hotel_id stores Zaaer property id (same as expenses / NTMP), not hotel_settings PK
                entity.Ignore(e => e.HotelSettings);

                entity.HasIndex(e => e.HotelId).HasDatabaseName("IX_IntegrationResponses_HotelId");
                entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_IntegrationResponses_CreatedAt");
                entity.HasIndex(e => e.Service).HasDatabaseName("IX_IntegrationResponses_Service");
                entity.Property(e => e.Service).HasMaxLength(50);
                entity.Property(e => e.Status).HasMaxLength(20);
            });
        }

        private void ConfigureReservationUnitDayRates(ModelBuilder modelBuilder)
        {
            // reservation_id / unit_id store Zaaer (global) ids when set — same as reservation_extras / discounts (no FK to reservations).
            modelBuilder.Entity<ReservationUnitDayRate>(entity =>
            {
                entity.Ignore(r => r.Reservation);
                entity.Ignore(r => r.ReservationUnit);

                entity.HasIndex(e => new { e.UnitId, e.NightDate })
                    .IsUnique()
                    .HasDatabaseName("UQ_RUDR_Unit_Date");
            });
        }

        private void ConfigureReservationUnitSwitches(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservationUnitSwitch>(entity =>
            {
                entity.HasOne(s => s.ReservationUnit)
                    .WithMany()
                    .HasForeignKey(s => s.UnitId)
                    .HasConstraintName("FK_RUSwitch_ReservationUnit")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(s => s.ApplyMode).HasMaxLength(30);
                entity.HasIndex(s => s.ReservationId).HasDatabaseName("IX_RUSwitch_Reservation");
                entity.HasIndex(s => s.UnitId).HasDatabaseName("IX_RUSwitch_Unit");
            });
        }

        private void ConfigureReservationUnitSwaps(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReservationUnitSwitch>(entity =>
            {
                entity.HasOne(s => s.ReservationUnit)
                    .WithMany()
                    .HasForeignKey(s => s.UnitId)
                    .HasConstraintName("FK_RUS_ReservationUnit")
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(s => s.ApplyMode).HasMaxLength(30);
                entity.Property(s => s.Comment).HasMaxLength(500);
                entity.HasIndex(s => s.ReservationId).HasDatabaseName("IX_RUS_ReservationId");
            });
        }

        private void ConfigureActivityLogs(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.HasIndex(x => x.HotelId).HasDatabaseName("IX_ActivityLogs_HotelId");
                entity.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_ActivityLogs_CreatedAt");
                entity.HasIndex(x => new { x.HotelId, x.ReservationId, x.CreatedAt })
                    .HasDatabaseName("IX_ActivityLogs_HotelReservationCreatedAt");
                entity.HasIndex(x => new { x.HotelId, x.CreatedAt })
                    .HasDatabaseName("IX_ActivityLogs_HotelCreatedAt");
                entity.HasIndex(x => new { x.HotelId, x.EventKey, x.CreatedAt })
                    .HasDatabaseName("IX_ActivityLogs_HotelEventCreatedAt");
                entity.Property(x => x.EventKey).HasMaxLength(100);
                entity.Property(x => x.RefType).HasMaxLength(50);
                entity.Property(x => x.RefNo).HasMaxLength(100);
                entity.Property(x => x.CreatedBy).HasMaxLength(200);
                entity.Property(x => x.IconKey).HasMaxLength(50);
                entity.Property(x => x.ReservationNo).HasMaxLength(50);
                entity.Property(x => x.Source).HasMaxLength(30).HasDefaultValue("pms");
            });
        }

        private void ConfigureRateTypes(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RateType>(entity =>
            {
                entity.HasOne(r => r.HotelSettings)
                    .WithMany()
                    .HasForeignKey(r => r.HotelId)
                    .HasConstraintName("FK_RateTypes_HotelSettings")
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(r => r.HotelId).HasDatabaseName("IX_RateTypes_HotelId");
                entity.HasIndex(r => new { r.HotelId, r.ShortCode })
                    .IsUnique()
                    .HasDatabaseName("UQ_RateTypes_HotelId_ShortCode");
                entity.Property(r => r.ShortCode).HasMaxLength(50);
                entity.Property(r => r.Title).HasMaxLength(255);
            });

            modelBuilder.Entity<RateTypeUnitItem>(entity =>
            {
                // Remove Foreign Key constraint - data comes from Zaaer system without enforced referential integrity
                // NOTE: Relationship configuration is commented out (similar to Building relationship)
                // This allows rate_type_id values that don't exist in rate_types table
                // Navigation property in Entity model is ignored to prevent FK constraint creation
                // Manual joins can be used in Service if needed
                
                // Keep index for performance but without FK constraint
                entity.HasIndex(i => i.RateTypeId).HasDatabaseName("IX_RateTypeUnitItems_RateTypeId");
                entity.HasIndex(i => new { i.RateTypeId, i.UnitTypeName })
                    .IsUnique()
                    .HasDatabaseName("UQ_RateTypeUnitItems_RateTypeId_UnitTypeName");
                entity.Property(i => i.UnitTypeName).HasMaxLength(100);
                entity.Property(i => i.Rate).HasColumnType("decimal(18,2)");
            });
        }

        private void ConfigureInvoiceReceiptMappingRelationships(ModelBuilder modelBuilder)
        {
            // InvoiceReceiptMapping relationships
            modelBuilder.Entity<InvoiceReceiptMapping>()
                .HasOne(irm => irm.Invoice)
                .WithMany(i => i.InvoiceReceiptMappings)
                .HasForeignKey(irm => irm.InvoiceId)
                .HasConstraintName("FK_InvoiceReceiptMappings_Invoices")
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InvoiceReceiptMapping>()
                .HasOne(irm => irm.PaymentReceipt)
                .WithMany(pr => pr.InvoiceReceiptMappings)
                .HasForeignKey(irm => irm.ReceiptId)
                .HasConstraintName("FK_InvoiceReceiptMappings_PaymentReceipts")
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            modelBuilder.Entity<InvoiceReceiptMapping>()
                .HasIndex(irm => irm.InvoiceId)
                .HasDatabaseName("IX_InvoiceReceiptMappings_InvoiceId");

            modelBuilder.Entity<InvoiceReceiptMapping>()
                .HasIndex(irm => irm.ReceiptId)
                .HasDatabaseName("IX_InvoiceReceiptMappings_ReceiptId");

            // Unique constraint to prevent duplicate mappings (same invoice-receipt combination)
            modelBuilder.Entity<InvoiceReceiptMapping>()
                .HasIndex(irm => new { irm.InvoiceId, irm.ReceiptId })
                .IsUnique()
                .HasDatabaseName("UQ_InvoiceReceiptMappings_InvoiceId_ReceiptId");
        }

        private void ConfigureInvoiceJournalEntryRelationships(ModelBuilder modelBuilder)
        {
            // InvoiceJournalEntry relationships
            modelBuilder.Entity<InvoiceJournalEntry>()
                .HasOne(ije => ije.Invoice)
                .WithMany()
                .HasForeignKey(ije => ije.InvoiceId)
                .HasConstraintName("FK_InvoiceJournalEntries_Invoices")
                .OnDelete(DeleteBehavior.Restrict);

            // IMPORTANT: journal_entry_code is unique (not invoice_id)
            // Because:
            // - For Invoices: journal_entry_code = InvoiceNo (unique per invoice)
            // - For Credit Notes: journal_entry_code = CreditNoteNo (unique per credit note)
            // - Multiple journal entries can share the same invoice_id (e.g., invoice + credit note reverse entry)
            modelBuilder.Entity<InvoiceJournalEntry>()
                .HasIndex(ije => ije.JournalEntryCode)
                .IsUnique()
                .HasDatabaseName("IX_InvoiceJournalEntries_JournalEntryCode");

            // Indexes for performance (non-unique)
            modelBuilder.Entity<InvoiceJournalEntry>()
                .HasIndex(ije => ije.InvoiceId)
                .HasDatabaseName("IX_InvoiceJournalEntries_InvoiceId");

            modelBuilder.Entity<InvoiceJournalEntry>()
                .HasIndex(ije => ije.Status)
                .HasDatabaseName("IX_InvoiceJournalEntries_Status");

            modelBuilder.Entity<InvoiceJournalEntry>()
                .HasIndex(ije => ije.JournalDate)
                .HasDatabaseName("IX_InvoiceJournalEntries_JournalDate");
        }

        /// <summary>
        /// Configure relationships for Orders and Outlets related entities
        /// NOTE: Foreign Key constraints are NOT created to avoid issues when receiving data from Zaaer API
        /// </summary>
        private void ConfigureOrdersRelationships(ModelBuilder modelBuilder)
        {
            // Order relationships
            // Note: No FK constraints - relationships are defined in models but not enforced at DB level
            // This allows data from Zaaer API to arrive in any order without constraint violations

            // Order -> OrderItems (One-to-Many)
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // Cascade delete order items when order is deleted

            // OrderItem -> OutletItem (Many-to-One, optional)
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.OutletItem)
                .WithMany(item => item.OrderItems)
                .HasForeignKey(oi => oi.ItemId)
                .OnDelete(DeleteBehavior.SetNull); // Set null if outlet item is deleted

            // Order -> Outlet (Many-to-One, optional)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Outlet)
                .WithMany(outlet => outlet.Orders)
                .HasForeignKey(o => o.OutletId)
                .OnDelete(DeleteBehavior.SetNull);

            // Order -> OutletTable (Many-to-One, optional)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.OutletTable)
                .WithMany(table => table.Orders)
                .HasForeignKey(o => o.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            // Order -> Customer (Many-to-One, required)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Order -> Reservation (Many-to-One, optional)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Reservation)
                .WithMany()
                .HasForeignKey(o => o.ReservationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Order -> HotelSettings (Many-to-One, required)
            modelBuilder.Entity<Order>()
                .HasOne(o => o.HotelSettings)
                .WithMany()
                .HasForeignKey(o => o.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            // PaymentReceipt -> Order (Many-to-One, optional)
            modelBuilder.Entity<PaymentReceipt>()
                .HasOne(pr => pr.Order)
                .WithMany(o => o.PaymentReceipts)
                .HasForeignKey(pr => pr.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Invoice -> Order (Many-to-One, optional)
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Order)
                .WithMany(o => o.Invoices)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // CreditNote -> Order (Many-to-One, optional)
            modelBuilder.Entity<CreditNote>()
                .HasOne(cn => cn.Order)
                .WithMany(o => o.CreditNotes)
                .HasForeignKey(cn => cn.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Outlet relationships
            // Outlet -> HotelSettings (Many-to-One, required)
            modelBuilder.Entity<Outlet>()
                .HasOne(o => o.HotelSettings)
                .WithMany()
                .HasForeignKey(o => o.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            // OutletItem -> Outlet (Many-to-One, optional)
            modelBuilder.Entity<OutletItem>()
                .HasOne(item => item.Outlet)
                .WithMany(outlet => outlet.OutletItems)
                .HasForeignKey(item => item.OutletId)
                .OnDelete(DeleteBehavior.SetNull);

            // OutletItem -> OutletCategory (Many-to-One, optional)
            modelBuilder.Entity<OutletItem>()
                .HasOne(item => item.OutletCategory)
                .WithMany(cat => cat.OutletItems)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // OutletItem -> HotelSettings (Many-to-One, required)
            modelBuilder.Entity<OutletItem>()
                .HasOne(item => item.HotelSettings)
                .WithMany()
                .HasForeignKey(item => item.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            // OutletCategory -> HotelSettings (Many-to-One, required)
            modelBuilder.Entity<OutletCategory>()
                .HasOne(cat => cat.HotelSettings)
                .WithMany()
                .HasForeignKey(cat => cat.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            // OutletTable -> Outlet (Many-to-One, optional)
            modelBuilder.Entity<OutletTable>()
                .HasOne(table => table.Outlet)
                .WithMany(outlet => outlet.OutletTables)
                .HasForeignKey(table => table.OutletId)
                .OnDelete(DeleteBehavior.SetNull);

            // OutletTable -> HotelSettings (Many-to-One, required)
            modelBuilder.Entity<OutletTable>()
                .HasOne(table => table.HotelSettings)
                .WithMany()
                .HasForeignKey(table => table.HotelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNo)
                .IsUnique()
                .HasDatabaseName("IX_Orders_OrderNo");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.HotelId)
                .HasDatabaseName("IX_Orders_HotelId");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.CustomerId)
                .HasDatabaseName("IX_Orders_CustomerId");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderStatus)
                .HasDatabaseName("IX_Orders_OrderStatus");

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ZaaerId)
                .HasDatabaseName("IX_Orders_ZaaerId");

            modelBuilder.Entity<OrderItem>()
                .HasIndex(oi => oi.OrderId)
                .HasDatabaseName("IX_OrderItems_OrderId");

            modelBuilder.Entity<OrderItem>()
                .HasIndex(oi => oi.ZaaerId)
                .HasDatabaseName("IX_OrderItems_ZaaerId");

            modelBuilder.Entity<Outlet>()
                .HasIndex(o => o.HotelId)
                .HasDatabaseName("IX_Outlets_HotelId");

            modelBuilder.Entity<Outlet>()
                .HasIndex(o => o.ZaaerId)
                .HasDatabaseName("IX_Outlets_ZaaerId");

            modelBuilder.Entity<OutletCategory>()
                .HasIndex(cat => cat.HotelId)
                .HasDatabaseName("IX_OutletCategories_HotelId");

            modelBuilder.Entity<OutletCategory>()
                .HasIndex(cat => cat.ZaaerId)
                .HasDatabaseName("IX_OutletCategories_ZaaerId");

            modelBuilder.Entity<OutletItem>()
                .HasIndex(item => item.HotelId)
                .HasDatabaseName("IX_OutletItems_HotelId");

            modelBuilder.Entity<OutletItem>()
                .HasIndex(item => item.OutletId)
                .HasDatabaseName("IX_OutletItems_OutletId");

            modelBuilder.Entity<OutletItem>()
                .HasIndex(item => item.CategoryId)
                .HasDatabaseName("IX_OutletItems_CategoryId");

            modelBuilder.Entity<OutletItem>()
                .HasIndex(item => item.ZaaerId)
                .HasDatabaseName("IX_OutletItems_ZaaerId");

            modelBuilder.Entity<OutletTable>()
                .HasIndex(table => table.HotelId)
                .HasDatabaseName("IX_OutletTables_HotelId");

            modelBuilder.Entity<OutletTable>()
                .HasIndex(table => table.OutletId)
                .HasDatabaseName("IX_OutletTables_OutletId");

            modelBuilder.Entity<OutletTable>()
                .HasIndex(table => table.ZaaerId)
                .HasDatabaseName("IX_OutletTables_ZaaerId");

            modelBuilder.Entity<PaymentReceipt>()
                .HasIndex(pr => pr.OrderId)
                .HasDatabaseName("IX_PaymentReceipts_OrderId");

            modelBuilder.Entity<PaymentReceipt>()
                .HasIndex(pr => pr.RevenueCategory)
                .HasDatabaseName("IX_PaymentReceipts_RevenueCategory");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.OrderId)
                .HasDatabaseName("IX_Invoices_OrderId");

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.RevenueCategory)
                .HasDatabaseName("IX_Invoices_RevenueCategory");

            modelBuilder.Entity<CreditNote>()
                .HasIndex(cn => cn.OrderId)
                .HasDatabaseName("IX_CreditNotes_OrderId");
        }
    }
}
