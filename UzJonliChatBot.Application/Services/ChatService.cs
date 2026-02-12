namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepository;

    public ChatService(IChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<bool> IsInChatAsync(long userId)
    {
        return await _chatRepository.IsInChatAsync(userId);
    }

    public async Task<long?> GetPartnerAsync(long userId)
    {
        var chat = await _chatRepository.GetActiveChatAsync(userId);
        if (chat == null)
            return null;

        return chat.User1Id == userId ? chat.User2Id : chat.User1Id;
    }

    public async Task CreateChatAsync(long user1, long user2)
    {
        var chat = new Chat
        {
            User1Id = user1,
            User2Id = user2
        };
        await _chatRepository.AddAsync(chat);
    }

    public async Task EndChatAsync(long userId)
    {
        var chat = await _chatRepository.GetActiveChatAsync(userId);
        if (chat != null)
        {
            await _chatRepository.RemoveAsync(chat);
        }
    }
}
