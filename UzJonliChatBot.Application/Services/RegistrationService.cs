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

    public async Task SetGenderAsync(long userId, Gender gender)
    {
        var user = await _userRepository.GetByTelegramIdAsync(userId);

        if (user == null)
        {
            user = new User { TelegramId = userId };
        }

        user.Gender = gender;
        user.RegistrationStatus = UserRegistrationStatus.AgeVerificationPending;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.AddOrUpdateAsync(user);
    }

    public async Task SetUserInfoAsync(long userId, string? fullName, string? username)
    {
        var user = await _userRepository.GetByTelegramIdAsync(userId);

        if (user == null)
        {
            user = new User { TelegramId = userId };
        }

        user.FullName = fullName;
        user.Username = username;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.AddOrUpdateAsync(user);
    }

    public async Task UpdateGenderAsync(long userId, Gender gender)
    {
        var user = await _userRepository.GetByTelegramIdAsync(userId);

        if (user != null)
        {
            user.Gender = gender;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.AddOrUpdateAsync(user);
        }
    }

    public async Task ConfirmAgeAsync(long userId)
    {
        var user = await _userRepository.GetByTelegramIdAsync(userId);

        if (user == null)
        {
            user = new User { TelegramId = userId };
        }

        user.IsAgeVerified = true;
        user.RegistrationStatus = UserRegistrationStatus.Registered;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.AddOrUpdateAsync(user);
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
