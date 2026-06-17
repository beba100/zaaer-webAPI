namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Maps global BIGINT Zaaer IDs from Master numbering to tenant column storage.
    /// Tenant tables may still use INT until a BIGINT migration is applied.
    /// </summary>
    public static class ZaaerIdMapper
    {
        public static int? ToNullableInt32(long? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return ToInt32(value.Value);
        }

        public static int ToInt32(long value)
        {
            if (value is < int.MinValue or > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"ZaaerId value {value} exceeds Int32 range. Run zaaer_id BIGINT migration on tenant tables.");
            }

            return (int)value;
        }
    }
}
