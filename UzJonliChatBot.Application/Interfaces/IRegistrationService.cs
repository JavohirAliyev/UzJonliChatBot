using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IRegistrationService
{
    UserRegistrationStatus GetRegistrationStatus(long userId);
    Task SetGenderAsync(long userId, Gender gender);
    Task SetUserInfoAsync(long userId, string? fullName, string? username);
    Task UpdateGenderAsync(long userId, Gender gender);
    Task ConfirmAgeAsync(long userId);
    User? GetUser(long userId);
    bool IsRegistered(long userId);
}
