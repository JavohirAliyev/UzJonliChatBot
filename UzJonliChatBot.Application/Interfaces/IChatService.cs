namespace UzJonliChatBot.Application.Interfaces;

public interface IChatService
{
    bool IsInChat(long userId);
    long? GetPartner(long userId);
    void CreateChat(long user1, long user2);
    void EndChat(long userId);
}
