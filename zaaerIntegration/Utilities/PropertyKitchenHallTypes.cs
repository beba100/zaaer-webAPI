namespace zaaerIntegration.Utilities
{
    public static class PropertyTypes
    {
        public const string Hotel = "hotel";
        public const string Resort = "resort";
        public const string Hall = "hall";

        public static readonly string[] All = { Hotel, Resort, Hall };

        public static bool IsResort(string? value) =>
            string.Equals(value?.Trim(), Resort, StringComparison.OrdinalIgnoreCase);

        public static bool IsHall(string? value) =>
            string.Equals(value?.Trim(), Hall, StringComparison.OrdinalIgnoreCase);

        public static bool IsHotel(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(normalized) || normalized == Hotel;
        }

        public static string ResolveMode(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant() ?? Hotel;
            if (IsResort(normalized))
            {
                return Resort;
            }

            if (IsHall(normalized))
            {
                return Hall;
            }

            return Hotel;
        }
    }

    public static class HallGenderTypes
    {
        public const string Men = "men";
        public const string Women = "women";
        public const string Mixed = "mixed";
        public const string Vip = "vip";

        public static readonly string[] All = { Men, Women, Mixed, Vip };
    }

    public static class HallVenueKinds
    {
        public const string Indoor = "indoor";
        public const string Outdoor = "outdoor";
        public const string Luxury = "luxury";
        public const string CeremonySmall = "ceremony_small";

        public static readonly string[] All = { Indoor, Outdoor, Luxury, CeremonySmall };
    }

    public static class HallPreparationStatuses
    {
        public const string Cleaning = "cleaning";
        public const string Preparing = "preparing";
        public const string Ready = "ready";
        public const string Occupied = "occupied";
        public const string Maintenance = "maintenance";
        public const string Blocked = "blocked";

        public static readonly string[] All = { Cleaning, Preparing, Ready, Occupied, Maintenance, Blocked };
    }

    public static class HallEventTypes
    {
        public const string Wedding = "wedding";
        public const string Engagement = "engagement";
        public const string Graduation = "graduation";
        public const string FamilyGathering = "family_gathering";
        public const string CorporateEvent = "corporate_event";
        public const string Conference = "conference";
        public const string Exhibition = "exhibition";
        public const string RamadanEvent = "ramadan_event";
        public const string EidEvent = "eid_event";

        public static readonly string[] All =
        {
            Wedding, Engagement, Graduation, FamilyGathering, CorporateEvent,
            Conference, Exhibition, RamadanEvent, EidEvent
        };
    }

    public static class PackagePriceTypes
    {
        public const string Fixed = "fixed";
        public const string PerGuest = "per_guest";
        public const string PerHour = "per_hour";

        public static readonly string[] All = { Fixed, PerGuest, PerHour };
    }

    public static class PackageCategories
    {
        public const string Food = "food";
        public const string Hospitality = "hospitality";
        public const string Desserts = "desserts";
        public const string Service = "service";
        public const string Rental = "rental";

        public static readonly string[] All = { Food, Hospitality, Desserts, Service, Rental };
    }

    public static class ResortAreaTypes
    {
        public const string Internal = "internal";
        public const string External = "external";

        public static readonly string[] All = { Internal, External };
    }

    public static class PropertyKitchenTypes
    {
        public const string None = "none";
        public const string Small = "small";
        public const string Standard = "standard";
        public const string Medium = "medium";
        public const string Large = "large";

        public static readonly string[] All = { None, Small, Standard, Medium, Large };
    }

    public static class PropertyHallTypes
    {
        public const string None = "none";
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
        public const string Deluxe = "deluxe";

        public static readonly string[] All = { None, Small, Medium, Large, Deluxe };
    }

    public static class PropertyRoomCategories
    {
        public const string Other = "other";
        public const string Room = "room";
        public const string Suite = "suite";
        public const string Apartment = "apartment";
        public const string Villa = "villa";
        public const string Chalet = "chalet";

        public static readonly string[] All = { Other, Room, Suite, Apartment, Villa, Chalet };
    }

    public static class ResortTicketCategories
    {
        public const string Entry = "entry";
        public const string Games = "games";
        public const string Pool = "pool";
        public const string Other = "other";

        public static readonly string[] All = { Entry, Games, Pool, Other };

        public static string Normalize(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized! : Other;
        }
    }
}
