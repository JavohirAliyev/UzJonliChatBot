namespace UzJonliChatBot.Application.Interfaces;

public interface IAdminService
{
    Task<string?> LoginAsync(string username, string password);
    Task InitializeAdminAsync(string username, string password);
    bool ValidateToken(string token);
}
