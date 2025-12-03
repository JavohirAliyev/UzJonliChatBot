namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

public class ChatService : IChatService
{
    private readonly Dictionary<long, ChatState> _activeChats = new();

    public bool IsInChat(long userId) => _activeChats.ContainsKey(userId);

    public long? GetPartner(long userId) =>
        _activeChats.TryGetValue(userId, out var state)
            ? state.PartnerId
            : (long?)null;

    public void CreateChat(long user1, long user2)
    {
        _activeChats[user1] = new ChatState { PartnerId = user2 };
        _activeChats[user2] = new ChatState { PartnerId = user1 };
    }

    public void EndChat(long userId)
    {
        if (!_activeChats.TryGetValue(userId, out var state))
            return;

        long partnerId = state.PartnerId;

        _activeChats.Remove(userId);
        _activeChats.Remove(partnerId);
    }
}
