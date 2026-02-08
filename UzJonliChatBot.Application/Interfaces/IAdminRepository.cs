using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Repository interface for admin persistence operations.
/// </summary>
public interface IAdminRepository
{
    /// <summary>
    /// Gets an admin by username.
    /// </summary>
    Task<Admin?> GetByUsernameAsync(string username);

    /// <summary>
    /// Creates a new admin account.
    /// </summary>
    Task AddAsync(Admin admin);

    /// <summary>
    /// Updates admin's last login time.
    /// </summary>
    Task UpdateLastLoginAsync(int adminId);

    /// <summary>
    /// Checks if any admin exists in the system.
    /// </summary>
    Task<bool> AnyAdminExistsAsync();
}
