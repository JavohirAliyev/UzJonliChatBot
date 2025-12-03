namespace UzJonliChatBot.Application.Interfaces;

public interface IUserService
{
    bool Exists(long userId);
    void EnsureCreated(long userId);
}
