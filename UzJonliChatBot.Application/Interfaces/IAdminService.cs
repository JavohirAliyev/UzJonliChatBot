using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Service interface for admin operations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Authenticates an admin and returns a JWT token.
    /// </summary>
    Task<string?> LoginAsync(string username, string password);

    /// <summary>
    /// Creates the initial admin account if no admin exists.
    /// </summary>
    Task InitializeAdminAsync(string username, string password);

    /// <summary>
    /// Validates a JWT token.
    /// </summary>
    bool ValidateToken(string token);
}
