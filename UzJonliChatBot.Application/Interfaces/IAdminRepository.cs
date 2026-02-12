using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Application.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByUsernameAsync(string username);
    Task AddAsync(Admin admin);
    Task UpdateLastLoginAsync(int adminId);
    Task<bool> AnyAdminExistsAsync();
}
