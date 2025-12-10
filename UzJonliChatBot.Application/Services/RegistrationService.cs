using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Services;

/// <summary>
/// Service for managing user registration and verification.
/// Handles gender selection and age confirmation.
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly IUserRepository _userRepository;

    public RegistrationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public UserRegistrationStatus GetRegistrationStatus(long userId)
    {
        var user = _userRepository.GetByTelegramIdAsync(userId).Result;
        return user?.RegistrationStatus ?? UserRegistrationStatus.NotStarted;
    }

    public void SetGender(long userId, Gender gender)
    {
        var user = _userRepository.GetByTelegramIdAsync(userId).Result;

        if (user == null)
        {
            user = new User { TelegramId = userId };
        }

        user.Gender = gender;
        user.RegistrationStatus = UserRegistrationStatus.AgeVerificationPending;
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.AddOrUpdateAsync(user).Wait();
    }

    public void ConfirmAge(long userId)
    {
        var user = _userRepository.GetByTelegramIdAsync(userId).Result;

        if (user == null)
        {
            user = new User { TelegramId = userId };
        }

        user.IsAgeVerified = true;
        user.RegistrationStatus = UserRegistrationStatus.Registered;
        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.AddOrUpdateAsync(user).Wait();
    }

    public User? GetUser(long userId)
    {
        return _userRepository.GetByTelegramIdAsync(userId).Result;
    }

    public bool IsRegistered(long userId)
    {
        var user = _userRepository.GetByTelegramIdAsync(userId).Result;
        return user != null && user.RegistrationStatus == UserRegistrationStatus.Registered && user.IsAgeVerified;
    }
}
