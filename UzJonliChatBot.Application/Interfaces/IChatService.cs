namespace UzJonliChatBot.Application.Interfaces;

public interface IChatService
{
    Task<bool> IsInChatAsync(long userId);
    Task<(bool InChat, long? PartnerId)> GetChatStatusAsync(long userId);
    Task<long?> GetPartnerAsync(long userId);
    Task CreateChatAsync(long user1, long user2);
    Task EndChatAsync(long userId);
}
