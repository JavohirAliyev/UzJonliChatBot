namespace UzJonliChatBot.Application.Interfaces;

using UzJonliChatBot.Application.Models;

public interface IMatchmakingService
{
    Task<bool> IsWaitingAsync(long userId);
    Task EnqueueUserAsync(long userId, string? genderPreference = null);
    Task<long?> DequeueUserAsync();
    Task<long?> DequeueUserByGenderAsync(string preferredGender);
    Task<MatchResultDto> FindOrEnqueuePartnerAsync(long userId, string? genderPreference = null);
    Task RemoveFromQueueAsync(long userId);
}
