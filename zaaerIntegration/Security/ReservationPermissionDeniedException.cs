namespace zaaerIntegration.Security
{
    /// <summary>Thrown when a reservation operation requires a permission the user does not have.</summary>
    public sealed class ReservationPermissionDeniedException : Exception
    {
        public ReservationPermissionDeniedException(string permissionCode)
            : base($"Missing permission: {permissionCode}")
        {
            PermissionCode = permissionCode;
        }

        public string PermissionCode { get; }
    }
}
