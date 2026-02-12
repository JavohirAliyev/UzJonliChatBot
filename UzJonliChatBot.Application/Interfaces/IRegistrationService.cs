using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IRegistrationService
{
    Task<UserRegistrationStatus> GetRegistrationStatus(long userId);
    Task SetGenderAsync(long userId, Gender gender);
    Task SetUserInfoAsync(long userId, string? fullName, string? username);
    Task UpdateGenderAsync(long userId, Gender gender);
    Task ConfirmAgeAsync(long userId);
    Task<User?> GetUser(long userId);
    Task<bool> IsRegistered(long userId);
}
