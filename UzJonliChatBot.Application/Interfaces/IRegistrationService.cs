using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

/// <summary>
/// Service for managing user registration and verification.
/// </summary>
public interface IRegistrationService
{
    /// <summary>
    /// Gets the current registration status of a user.
    /// </summary>
    UserRegistrationStatus GetRegistrationStatus(long userId);

    /// <summary>
    /// Sets the user's gender.
    /// </summary>
    void SetGender(long userId, Gender gender);

    /// <summary>
    /// Confirms the user is 18 or older.
    /// </summary>
    void ConfirmAge(long userId);

    /// <summary>
    /// Gets user details.
    /// </summary>
    User? GetUser(long userId);

    /// <summary>
    /// Checks if user is fully registered.
    /// </summary>
    bool IsRegistered(long userId);
}
