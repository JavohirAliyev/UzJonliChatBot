namespace UzJonliChatBot.Application.Services;

using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

public class MatchmakingService : IMatchmakingService
{
    private readonly IMatchmakingQueueRepository _queueRepository;
    private readonly IUserRepository _userRepository;

    public MatchmakingService(IMatchmakingQueueRepository queueRepository, IUserRepository userRepository)
    {
        _queueRepository = queueRepository;
        _userRepository = userRepository;
    }

    public async Task<bool> IsWaitingAsync(long userId)
    {
        return await _queueRepository.IsInQueueAsync(userId);
    }

    public async Task EnqueueUserAsync(long userId, string? genderPreference = null)
    {
        await _queueRepository.EnqueueAsync(userId, genderPreference);
    }

    public async Task<long?> DequeueUserAsync()
    {
        return await _queueRepository.DequeueAsync();
    }

    public async Task<long?> DequeueUserByGenderAsync(string preferredGender)
    {
        return await _queueRepository.DequeueByPartnerGenderAsync(preferredGender);
    }

    public async Task<MatchResultDto> FindOrEnqueuePartnerAsync(long userId, string? genderPreference = null)
    {
        var user = await _userRepository.GetByTelegramIdAsync(userId);
        if (user == null)
        {
            return new MatchResultDto { Status = MatchStatus.Enqueued };
        }

        long? partner;
        var hasPremiumPreference = user.IsPremium && !string.IsNullOrWhiteSpace(genderPreference);

        if (hasPremiumPreference)
        {
            partner = await _queueRepository.DequeueByPartnerGenderAsync(genderPreference!);
        }
        else
        {
            partner = await _queueRepository.DequeueAsync();
        }

        if (partner.HasValue && partner.Value == userId)
        {
            await _queueRepository.EnqueueAsync(userId, hasPremiumPreference ? genderPreference : null);
            return new MatchResultDto { Status = MatchStatus.SelfMatched };
        }

        if (partner.HasValue)
        {
            return new MatchResultDto
            {
                Status = MatchStatus.PartnerFound,
                PartnerId = partner.Value
            };
        }

        await _queueRepository.EnqueueAsync(userId, hasPremiumPreference ? genderPreference : null);
        return new MatchResultDto { Status = MatchStatus.Enqueued };
    }

    public async Task RemoveFromQueueAsync(long userId)
    {
        await _queueRepository.RemoveFromQueueAsync(userId);
    }
}
