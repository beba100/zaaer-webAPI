using FinanceLedgerAPI.Models;
using zaaerIntegration.DTOs.Rbac;

namespace zaaerIntegration.Services.Interfaces
{
    public interface IRbacUserService
    {
        Task<MasterRbacUser?> ValidateLoginAsync(string login, string password);
        Task<RbacUserDetailDto?> GetDetailAsync(int userId);
        Task<MasterRbacUser?> GetByIdAsync(int userId);
        Task<MasterRbacUser?> GetByEmployeeNumberAsync(string employeeNumber);
        Task<MasterRbacUser?> GetByEmailAsync(string email);
        Task<IReadOnlyList<RbacUserListItemDto>> GetAllAsync();
        Task<MasterRbacUser> CreateAsync(RbacUserSaveDto dto);
        Task<MasterRbacUser> UpdateAsync(int userId, RbacUserSaveDto dto);
        Task<IReadOnlyList<string>> GetUserRoleCodesAsync(int userId);
        Task AssignRoleAsync(int userId, int roleId);
        Task AssignHotelsAsync(int userId, IEnumerable<int> tenantIds);
        Task<int> GetRecentResetRequestsAsync(int userId, TimeSpan window);
        Task<string> CreatePasswordResetTokenAsync(int userId, string? ipAddress = null);
        Task<int?> ValidateResetTokenAsync(string token);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<RbacProfileDto?> GetProfileAsync(int userId);
        Task<RbacProfileDto> UpdateProfileAsync(int userId, RbacProfileUpdateDto dto);
        Task ChangePasswordAsync(int userId, RbacChangePasswordDto dto);
        string HashPassword(string password);
    }
}
