namespace zaaerIntegration.DTOs.Rbac
{
    public class RbacUserListItemDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public string UserType { get; set; } = "employee";
        public bool IsActive { get; set; }
        public string? RoleSummary { get; set; }
        public string? HotelsSummary { get; set; }
    }

    public class RbacUserSaveDto
    {
        public string Username { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string? Password { get; set; }
        public string UserType { get; set; } = "employee";
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>Primary role (Zaaer-style single role on create).</summary>
        public int? RoleId { get; set; }
        /// <summary>Hotels / branches the user can access (no default hotel).</summary>
        public List<int> TenantIds { get; set; } = new();
    }

    public class RbacUserDetailDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string UserType { get; set; } = "employee";
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public bool IsActive { get; set; }
        public List<int> RoleIds { get; set; } = new();
        public List<int> TenantIds { get; set; } = new();
    }

    public class RbacRolePermissionSaveDto
    {
        public int RoleId { get; set; }
        public int PermissionId { get; set; }
        public bool Granted { get; set; } = true;
    }

    public class RbacRoleSaveDto
    {
        public string RoleNameAr { get; set; } = string.Empty;
        public string RoleNameEn { get; set; } = string.Empty;
        public string? RoleCode { get; set; }
        public string? RoleDescription { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RbacPermissionCatalogModuleDto
    {
        public string ModuleCode { get; set; } = string.Empty;
        public string ModuleNameAr { get; set; } = string.Empty;
        public string ModuleNameEn { get; set; } = string.Empty;
        public List<RbacPermissionCatalogSubmoduleDto> Submodules { get; set; } = new();
    }

    public class RbacPermissionCatalogSubmoduleDto
    {
        public string SubmoduleCode { get; set; } = string.Empty;
        public string SubmoduleNameAr { get; set; } = string.Empty;
        public string SubmoduleNameEn { get; set; } = string.Empty;
        public List<RbacPermissionCatalogItemDto> Permissions { get; set; } = new();
    }

    public class RbacPermissionCatalogItemDto
    {
        public int PermissionId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public bool Granted { get; set; }
    }

    public class RbacRolePermissionMatrixDto
    {
        public int RoleId { get; set; }
        public string RoleNameAr { get; set; } = string.Empty;
        public string RoleNameEn { get; set; } = string.Empty;
        public string? RoleCode { get; set; }
        public List<RbacPermissionCatalogModuleDto> Modules { get; set; } = new();
    }

    public class RbacProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? EmployeeNumber { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneCountryCode { get; set; } = "+966";
        public string? PhoneLocal { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public string? FullName { get; set; }
        public string? Initials { get; set; }
    }

    public class RbacProfileUpdateDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneCountryCode { get; set; } = "+966";
        public string? PhoneLocal { get; set; }
    }

    public class RbacChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class RbacUserLockDto
    {
        public string? Reason { get; set; }
    }
}
