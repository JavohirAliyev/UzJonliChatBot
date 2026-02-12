using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using UzJonliChatBot.Application.Constants;
using UzJonliChatBot.Application.Interfaces;
using UzJonliChatBot.Application.Models;

namespace UzJonliChatBot.Infrastructure.Telegram;

/// <summary>
/// Handles incoming updates from Telegram Bot API.
/// Manages user matching, message forwarding, and registration.
/// </summary>
public class TelegramUpdateHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramUpdateHandler> _logger;

    public TelegramUpdateHandler(
        IServiceProvider serviceProvider,
        ITelegramBotClient botClient,
        ILogger<TelegramUpdateHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _botClient = botClient;
        _logger = logger;
    }

    /// <summary>
    /// Handles incoming updates from Telegram.
    /// </summary>
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            // Create a scope for this update to resolve scoped services
            using var scope = _serviceProvider.CreateScope();

            if (update.Message?.Type == MessageType.Text)
            {
                await HandleMessageAsync(update.Message, scope);
            }
            else if (update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery, scope);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId} from user {UserId}",
                update.Id, update.Message?.Chat.Id ?? update.CallbackQuery?.From.Id ?? 0);
        }
    }

    /// <summary>
    /// Handles text messages and commands.
    /// </summary>
    private async Task HandleMessageAsync(Message message, IServiceScope scope)
    {
        var registrationService = scope.ServiceProvider.GetRequiredService<IRegistrationService>();
        var matchmakingService = scope.ServiceProvider.GetRequiredService<IMatchmakingService>();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var userId = message.Chat.Id;
        var text = message.Text ?? string.Empty;

        // Check if user is banned
        if (await IsUserBannedAsync(userId, userRepository))
        {
            await _botClient.SendMessage(userId, BotMessages.UserBanned);
            return;
        }

        if (text.StartsWith("/start"))
        {
            await HandleStartAsync(userId, registrationService);
        }
        else if (text.StartsWith("/keyingi") || text == BotMessages.MenuButtonFindPartner || text == BotMessages.MenuButtonNextPartner)
        {
            await HandleNextAsync(userId, registrationService, matchmakingService, chatService);
        }
        else if (text.StartsWith("/stop") || text == BotMessages.MenuButtonStopChat || text == BotMessages.MenuButtonStopSearch)
        {
            await HandleStopAsync(userId, chatService, matchmakingService);
        }
        else if (text == BotMessages.MenuButtonProfile)
        {
            await HandleProfileAsync(userId, registrationService, chatService, matchmakingService);
        }
        else
        {
            await HandleChatMessageAsync(userId, text, registrationService, chatService);
        }
    }

    /// <summary>
    /// Handles callback queries from buttons.
    /// </summary>
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, IServiceScope scope)
    {
        var registrationService = scope.ServiceProvider.GetRequiredService<IRegistrationService>();
        var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        var matchmakingService = scope.ServiceProvider.GetRequiredService<IMatchmakingService>();
        var userId = callbackQuery.From.Id;
        var data = callbackQuery.Data ?? string.Empty;
        var callbackId = callbackQuery.Id;

        try
        {
            if (data.StartsWith("gender_"))
            {
                await HandleGenderSelectionAsync(userId, data, callbackId, registrationService);
            }
            else if (data == "age_verified")
            {
                await HandleAgeVerificationAsync(userId, callbackId, registrationService);
            }
            else if (data == "change_gender")
            {
                await HandleChangeGenderRequestAsync(userId, callbackId);
            }
            else if (data.StartsWith("update_gender_"))
            {
                await HandleGenderUpdateAsync(userId, data, callbackId, registrationService, chatService, matchmakingService);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback query {CallbackId} from user {UserId}", callbackId, userId);
            await _botClient.AnswerCallbackQuery(callbackId, BotMessages.Error, showAlert: true);
        }
    }

    /// <summary>
    /// Handles /start command - begins registration flow.
    /// </summary>
    private async Task HandleStartAsync(long userId, IRegistrationService registrationService)
    {
        var status = await registrationService.GetRegistrationStatus(userId);

        if (status == UserRegistrationStatus.Registered)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var chat = await _botClient.GetChat(userId);
                    var fullName = $"{chat.FirstName ?? ""} {chat.LastName ?? ""}".Trim();
                    var username = chat.Username;

                    if (!string.IsNullOrEmpty(fullName) || !string.IsNullOrEmpty(username))
                    {
                        await registrationService.SetUserInfoAsync(userId, string.IsNullOrEmpty(fullName) ? null : fullName, username);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not update user info for user {UserId}", userId);
                }
            });

            await HandleStartAsync(userId);
            return;
        }

        // Get user info from Telegram for new users only
        try
        {
            var chat = await _botClient.GetChat(userId);
            var fullName = $"{chat.FirstName ?? ""} {chat.LastName ?? ""}".Trim();
            var username = chat.Username;

            // Save user info
            await registrationService.SetUserInfoAsync(userId, string.IsNullOrEmpty(fullName) ? null : fullName, username);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not get user info for user {UserId}", userId);
        }

        // Send gender selection message with inline buttons
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotMessages.GenderButtonMale, "gender_male"),
                InlineKeyboardButton.WithCallbackData(BotMessages.GenderButtonFemale, "gender_female")
            }
        });

        await _botClient.SendMessage(userId, BotMessages.GenderSelectionPrompt, replyMarkup: keyboard);
    }

    /// <summary>
    /// Handles gender selection from buttons.
    /// </summary>
    private async Task HandleGenderSelectionAsync(long userId, string data, string callbackId, IRegistrationService registrationService)
    {
        var gender = data == "gender_male" ? Gender.Male : Gender.Female;
        await registrationService.SetGenderAsync(userId, gender);

        // Show age verification prompt
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotMessages.AgeVerificationButton, "age_verified")
            }
        });

        // Answer the callback first to stop the client's loading spinner, then send the next message.
        await _botClient.AnswerCallbackQuery(callbackId);
        await _botClient.SendMessage(userId, BotMessages.AgeVerificationPrompt, replyMarkup: keyboard);
    }

    /// <summary>
    /// Handles age verification confirmation.
    /// </summary>
    private async Task HandleAgeVerificationAsync(long userId, string callbackId, IRegistrationService registrationService)
    {
        await registrationService.ConfirmAgeAsync(userId);
        await _botClient.AnswerCallbackQuery(callbackId);
        await _botClient.SendMessage(userId, BotMessages.RegistrationComplete, replyMarkup: GetMainKeyboard());
    }

    /// <summary>
    /// Handles /keyingi command - starts searching for a chat partner.
    /// </summary>
    private async Task HandleNextAsync(long userId, IRegistrationService registrationService, IMatchmakingService matchmakingService, IChatService chatService)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var swTotal = Stopwatch.StartNew();
        _logger.LogInformation("HandleNextAsync started for user {UserId} CorrelationId {CorrelationId}", userId, correlationId);

        try
        {
            // 1. BATCH STATUS CHECK - Single query instead of 3 separate ones
            var sw = Stopwatch.StartNew();
            var userStatus = await GetUserStatusForNextAsync(userId, registrationService, matchmakingService, chatService);
            sw.Stop();
            _logger.LogDebug("GetUserStatusForNextAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            if (userStatus.State == UserNextState.NotRegistered)
            {
                await _botClient.SendMessage(userId, BotMessages.NotRegistered);
                _logger.LogInformation("HandleNextAsync finished early (NotRegistered) for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
                return;
            }

            if (userStatus.State == UserNextState.InChat)
            {
                // End current chat and notify partner in parallel
                sw.Restart();
                await EndCurrentChatAndNotifyPartnerAsync(userId, userStatus.PartnerId!.Value, chatService, matchmakingService);
                sw.Stop();
                _logger.LogDebug("EndCurrentChatAndNotifyPartnerAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);
            }

            if (userStatus.State == UserNextState.AlreadyWaiting)
            {
                await _botClient.SendMessage(userId, BotMessages.WaitingForPartner, replyMarkup: GetSearchingKeyboard());
                _logger.LogInformation("HandleNextAsync finished early (AlreadyWaiting) for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
                return;
            }

            // 2. ATOMIC DEQUEUE + ENQUEUE operation
            sw.Restart();
            var matchResult = await FindOrEnqueuePartnerAsync(userId, matchmakingService, chatService);
            sw.Stop();
            _logger.LogDebug("FindOrEnqueuePartnerAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs} status {Status}", userId, correlationId, sw.ElapsedMilliseconds, matchResult.Status);

            switch (matchResult.Status)
            {
                case MatchStatus.SelfMatched:
                    await _botClient.SendMessage(userId, BotMessages.WaitingForPartner, replyMarkup: GetSearchingKeyboard());
                    break;

                case MatchStatus.PartnerFound:
                    // 3. PARALLEL MESSAGE SENDING - Send both messages at once instead of sequentially
                    sw.Restart();
                    await NotifyMatchedUsersAsync(userId, matchResult.PartnerId!.Value);
                    sw.Stop();
                    _logger.LogDebug("NotifyMatchedUsersAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);
                    break;

                case MatchStatus.Enqueued:
                    await _botClient.SendMessage(userId, BotMessages.WaitingForPartner, replyMarkup: GetSearchingKeyboard());
                    break;
            }

            _logger.LogInformation("HandleNextAsync finished for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleNextAsync for user {UserId} CorrelationId {CorrelationId}", userId, correlationId);
            await _botClient.SendMessage(userId, BotMessages.Error, replyMarkup: GetIdleKeyboard());
        }
    }

    /// <summary>
    /// Gets user status in a single query instead of multiple round trips.
    /// </summary>
    private async Task<UserNextStatusDto> GetUserStatusForNextAsync(
        long userId,
        IRegistrationService registrationService,
        IMatchmakingService matchmakingService,
        IChatService chatService)
    {
        // Check registration status
        if (!await registrationService.IsRegistered(userId))
        {
            return new UserNextStatusDto { State = UserNextState.NotRegistered };
        }

        // Check if in chat (Priority: Chat > Queue)
        var (inChat, partnerId) = await chatService.GetChatStatusAsync(userId);
        if (inChat)
        {
            return new UserNextStatusDto
            {
                State = UserNextState.InChat,
                PartnerId = partnerId
            };
        }

        // Check if already waiting
        var isWaiting = await matchmakingService.IsWaitingAsync(userId);
        if (isWaiting)
        {
            return new UserNextStatusDto { State = UserNextState.AlreadyWaiting };
        }

        return new UserNextStatusDto { State = UserNextState.ReadyToMatch };
    }

    /// <summary>
    /// End current chat and notify partner in parallel.
    /// </summary>
    private async Task EndCurrentChatAndNotifyPartnerAsync(long userId, long partnerId, IChatService chatService, IMatchmakingService matchmakingService)
    {
        await chatService.EndChatAsync(userId);

        // Send messages in parallel instead of sequentially
        await Task.WhenAll(
            _botClient.SendMessage(userId, BotMessages.ChatEndedWithSearching, replyMarkup: GetSearchingKeyboard()),
            _botClient.SendMessage(partnerId, BotMessages.PartnerLeft, replyMarkup: await GetKeyboardAsync(partnerId, chatService, matchmakingService))
        );
    }

    /// <summary>
    /// Atomic dequeue + enqueue operation with race condition handling.
    /// </summary>
    private async Task<MatchResultDto> FindOrEnqueuePartnerAsync(long userId, IMatchmakingService matchmakingService, IChatService chatService)
    {
        var partner = await matchmakingService.DequeueUserAsync();

        // Self-match prevention
        if (partner.HasValue && partner.Value == userId)
        {
            // Re-queue immediately - atomic operation
            try
            {
                await matchmakingService.EnqueueUserAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-queue user {UserId} after self-match", userId);
            }
            return new MatchResultDto { Status = MatchStatus.SelfMatched };
        }

        if (partner.HasValue)
        {
            // Partner found - create chat atomically
            try
            {
                await chatService.CreateChatAsync(userId, partner.Value);
                return new MatchResultDto
                {
                    Status = MatchStatus.PartnerFound,
                    PartnerId = partner.Value
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create chat between {UserId} and {PartnerId}", userId, partner.Value);
                // Fallback: enqueue current user
                try
                {
                    await matchmakingService.EnqueueUserAsync(userId);
                }
                catch { }
                return new MatchResultDto { Status = MatchStatus.Enqueued };
            }
        }

        // No partner - enqueue user
        try
        {
            await matchmakingService.EnqueueUserAsync(userId);
            return new MatchResultDto { Status = MatchStatus.Enqueued };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue user {UserId}", userId);
            return new MatchResultDto { Status = MatchStatus.Enqueued }; // User likely already in queue
        }
    }

    /// <summary>
    /// Send match notification to both users in parallel.
    /// </summary>
    private async Task NotifyMatchedUsersAsync(long userId, long partnerId)
    {
        var inChatKeyboard = GetInChatKeyboard();

        // Send both messages in parallel instead of sequential await
        await Task.WhenAll(
            _botClient.SendMessage(userId, BotMessages.FoundPartner, replyMarkup: inChatKeyboard),
            _botClient.SendMessage(partnerId, BotMessages.FoundPartner, replyMarkup: inChatKeyboard)
        );
    }
    /// <summary>
    /// Handles /stop command - ends the current chat or stops the search.
    /// </summary>
    private async Task HandleStopAsync(long userId, IChatService chatService, IMatchmakingService matchmakingService)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var swTotal = Stopwatch.StartNew();
        _logger.LogInformation("HandleStopAsync started for user {UserId} CorrelationId {CorrelationId}", userId, correlationId);

        var sw = Stopwatch.StartNew();
        // Check if user is in an active chat
        if (await chatService.IsInChatAsync(userId))
        {
            sw.Stop();
            _logger.LogDebug("IsInChatAsync returned true for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            sw.Restart();
            var partnerId = await chatService.GetPartnerAsync(userId);
            sw.Stop();
            _logger.LogDebug("GetPartnerAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            sw.Restart();
            await chatService.EndChatAsync(userId);
            sw.Stop();
            _logger.LogDebug("EndChatAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            await _botClient.SendMessage(userId, BotMessages.ChatEnded, replyMarkup: GetIdleKeyboard());

            if (partnerId.HasValue)
            {
                sw.Restart();
                var partnerKeyboard = await GetKeyboardAsync(partnerId.Value, chatService, matchmakingService);
                sw.Stop();
                _logger.LogDebug("GetKeyboardAsync for partner {PartnerId} completed CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", partnerId.Value, correlationId, sw.ElapsedMilliseconds);
                await _botClient.SendMessage(partnerId.Value, BotMessages.PartnerLeft, replyMarkup: partnerKeyboard);
            }

            _logger.LogInformation("HandleStopAsync finished for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
            return;
        }

        // Check if user is waiting in the matchmaking queue
        sw.Restart();
        if (await matchmakingService.IsWaitingAsync(userId))
        {
            sw.Stop();
            _logger.LogDebug("IsWaitingAsync returned true for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            sw.Restart();
            await matchmakingService.RemoveFromQueueAsync(userId);
            sw.Stop();
            _logger.LogDebug("RemoveFromQueueAsync completed for user {UserId} CorrelationId {CorrelationId} elapsedMs {ElapsedMs}", userId, correlationId, sw.ElapsedMilliseconds);

            await _botClient.SendMessage(userId, BotMessages.SearchStopped, replyMarkup: GetIdleKeyboard());
            _logger.LogInformation("HandleStopAsync finished for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
            return;
        }

        sw.Stop();
        // User is neither in chat nor in queue
        await _botClient.SendMessage(userId, BotMessages.NotInChat, replyMarkup: GetIdleKeyboard());
        _logger.LogInformation("HandleStopAsync finished (NotInChat) for user {UserId} CorrelationId {CorrelationId} totalMs {TotalMs}", userId, correlationId, swTotal.ElapsedMilliseconds);
    }

    /// <summary>
    /// Handles regular messages - forwards to chat partner.
    /// </summary>
    private async Task HandleChatMessageAsync(long userId, string text, IRegistrationService registrationService, IChatService chatService)
    {
        // Check registration
        if (!await registrationService.IsRegistered(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotRegistered);
            return;
        }

        if (!await chatService.IsInChatAsync(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotInChat);
            return;
        }

        var partnerId = await chatService.GetPartnerAsync(userId);
        if (!partnerId.HasValue)
        {
            await _botClient.SendMessage(userId, BotMessages.Error);
            return;
        }

        try
        {
            await _botClient.SendMessage(partnerId.Value, text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding message from user {UserId} to partner {PartnerId}", userId, partnerId.Value);
            await _botClient.SendMessage(userId, BotMessages.Error);
        }
    }

    /// <summary>
    /// Gets the keyboard based on user state (idle, searching, or in chat).
    /// </summary>
    private async Task<ReplyKeyboardMarkup> GetKeyboardAsync(long userId, IChatService chatService, IMatchmakingService matchmakingService)
    {
        // Check if user is in an active chat
        if (await chatService.IsInChatAsync(userId))
        {
            return GetInChatKeyboard();
        }

        // Check if user is searching
        if (await matchmakingService.IsWaitingAsync(userId))
        {
            return GetSearchingKeyboard();
        }

        // Default idle state
        return GetIdleKeyboard();
    }

    /// <summary>
    /// Gets the idle keyboard (no search, no chat).
    /// </summary>
    private ReplyKeyboardMarkup GetIdleKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonFindPartner),
                new KeyboardButton(BotMessages.MenuButtonProfile)
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
            Selective = false
        };
    }

    /// <summary>
    /// Gets the searching keyboard (user is waiting for partner).
    /// </summary>
    private ReplyKeyboardMarkup GetSearchingKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonStopSearch),
                new KeyboardButton(BotMessages.MenuButtonProfile)
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
            Selective = false
        };
    }

    /// <summary>
    /// Gets the in-chat keyboard (user is chatting with someone).
    /// </summary>
    private ReplyKeyboardMarkup GetInChatKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonStopChat),
                new KeyboardButton(BotMessages.MenuButtonNextPartner)
            },
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonProfile)
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
            Selective = false
        };
    }

    /// <summary>
    /// Gets the persistent keyboard menu for registered users (backward compatibility).
    /// </summary>
    private ReplyKeyboardMarkup GetMainKeyboard()
    {
        return GetIdleKeyboard();
    }

    /// <summary>
    /// Handles profile request - shows user information.
    /// </summary>
    private async Task HandleProfileAsync(long userId, IRegistrationService registrationService, IChatService chatService, IMatchmakingService matchmakingService)
    {
        var user = await registrationService.GetUser(userId);
        if (user == null)
        {
            await _botClient.SendMessage(userId, BotMessages.Error);
            return;
        }

        // Silently update user info from Telegram to keep data fresh
        // This happens in the background and doesn't affect the user experience
        _ = Task.Run(async () =>
        {
            try
            {
                var chat = await _botClient.GetChat(userId);
                var fullName = $"{chat.FirstName ?? ""} {chat.LastName ?? ""}".Trim();
                var username = chat.Username;

                // Update if there's any new info
                if (!string.IsNullOrEmpty(fullName) || !string.IsNullOrEmpty(username))
                {
                    await registrationService.SetUserInfoAsync(userId, string.IsNullOrEmpty(fullName) ? null : fullName, username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not update user info for user {UserId}", userId);
            }
        });

        var genderText = user!.Gender == Gender.Male ? "👨 Erkak" : "👩 Ayol";

        // Convert UTC to GMT+5 (Uzbekistan Time)
        var uzbekistanTime = user!.CreatedAt.AddHours(5);

        var profileMessage = $"👤 Sizning Profilingiz\n\n" +
            $"Jins: {genderText}\n" +
            $"Ro'yxatga olindi: {uzbekistanTime:dd.MM.yyyy HH:mm}";

        // Send inline keyboard for gender change in a separate message
        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Jinsni o'zgartirish", "change_gender")
            }
        });

        await _botClient.SendMessage(userId, profileMessage, replyMarkup: inlineKeyboard);
    }

    /// <summary>
    /// Handles /start command - shows welcome message.
    /// </summary>
    private async Task HandleStartAsync(long userId)
    {
        await _botClient.SendMessage(userId, BotMessages.Welcome, replyMarkup: GetMainKeyboard());
    }

    /// <summary>
    /// Checks if a user is banned.
    /// </summary>
    private async Task<bool> IsUserBannedAsync(long userId, IUserRepository userRepository)
    {
        var user = await userRepository.GetByTelegramIdAsync(userId);
        return user?.IsBanned ?? false;
    }

    /// <summary>
    /// Handles request to change gender.
    /// </summary>
    private async Task HandleChangeGenderRequestAsync(long userId, string callbackId)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotMessages.GenderButtonMale, "update_gender_male"),
                InlineKeyboardButton.WithCallbackData(BotMessages.GenderButtonFemale, "update_gender_female")
            }
        });

        await _botClient.AnswerCallbackQuery(callbackId);
        await _botClient.SendMessage(userId, "Yangi jinsingizni tanlang:", replyMarkup: keyboard);
    }

    /// <summary>
    /// Handles gender update from profile.
    /// </summary>
    private async Task HandleGenderUpdateAsync(long userId, string data, string callbackId, IRegistrationService registrationService, IChatService chatService, IMatchmakingService matchmakingService)
    {
        var gender = data == "update_gender_male" ? Gender.Male : Gender.Female;
        await registrationService.UpdateGenderAsync(userId, gender);

        await _botClient.AnswerCallbackQuery(callbackId, "✅ Jins muvaffaqiyatli o'zgartirildi!");

        // Show updated profile
        await HandleProfileAsync(userId, registrationService, chatService, matchmakingService);
    }
}
