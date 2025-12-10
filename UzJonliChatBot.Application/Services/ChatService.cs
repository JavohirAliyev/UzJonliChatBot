namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

/// <summary>
/// Service for managing active chats between users.
/// </summary>
public class ChatService : IChatService
{
    private readonly IChatRepository _chatRepository;

    public ChatService(IChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public bool IsInChat(long userId)
    {
        return _chatRepository.IsInChatAsync(userId).Result;
    }

    public long? GetPartner(long userId)
    {
        var chat = _chatRepository.GetActiveChatAsync(userId).Result;
        if (chat == null)
            return null;

        return chat.User1Id == userId ? chat.User2Id : chat.User1Id;
    }

    public void CreateChat(long user1, long user2)
    {
        var chat = new Chat
        {
            User1Id = user1,
            User2Id = user2
        };
        _chatRepository.AddAsync(chat).Wait();
    }

    public void EndChat(long userId)
    {
        var chat = _chatRepository.GetActiveChatAsync(userId).Result;
        if (chat != null)
        {
            _chatRepository.RemoveAsync(chat).Wait();
        }
    }
}
