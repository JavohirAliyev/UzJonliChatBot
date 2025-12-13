using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var userId = message.Chat.Id;
        var text = message.Text ?? string.Empty;

        if (text.StartsWith("/start"))
        {
            await HandleStartAsync(userId, registrationService, scope);
        }
        else if (text.StartsWith("/keyingi") || text == BotMessages.MenuButtonFindPartner)
        {
            await HandleNextAsync(userId, registrationService, matchmakingService, chatService);
        }
        else if (text.StartsWith("/stop") || text == BotMessages.MenuButtonStopChat)
        {
            await HandleStopAsync(userId, chatService, matchmakingService);
        }
        else if (text == BotMessages.MenuButtonProfile)
        {
            await HandleProfileAsync(userId, registrationService);
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
    private async Task HandleStartAsync(long userId, IRegistrationService registrationService, IServiceScope scope)
    {
        var status = registrationService.GetRegistrationStatus(userId);

        if (status == UserRegistrationStatus.Registered)
        {
            await HandleStartAsync(userId);
            return;
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
        registrationService.SetGender(userId, gender);

        // Show age verification prompt
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotMessages.AgeVerificationButton, "age_verified")
            }
        });

        await _botClient.SendMessage(userId, BotMessages.AgeVerificationPrompt, replyMarkup: keyboard);
        await _botClient.AnswerCallbackQuery(callbackId);
    }

    /// <summary>
    /// Handles age verification confirmation.
    /// </summary>
    private async Task HandleAgeVerificationAsync(long userId, string callbackId, IRegistrationService registrationService)
    {
        registrationService.ConfirmAge(userId);
        await _botClient.SendMessage(userId, BotMessages.RegistrationComplete, replyMarkup: GetMainKeyboard());
        await _botClient.AnswerCallbackQuery(callbackId);
    }

    /// <summary>
    /// Handles /keyingi command - starts searching for a chat partner.
    /// </summary>
    private async Task HandleNextAsync(long userId, IRegistrationService registrationService, IMatchmakingService matchmakingService, IChatService chatService)
    {
        // Check if user is registered
        if (!registrationService.IsRegistered(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotRegistered);
            return;
        }

        // Check if user is already in a chat
        if (chatService.IsInChat(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.AlreadyInChat);
            return;
        }

        // Check if user is already waiting
        if (await matchmakingService.IsWaitingAsync(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.WaitingForPartner);
            return;
        }

        // Try to find a partner
        var partner = await matchmakingService.DequeueUserAsync();

        if (partner.HasValue)
        {
            // Found a partner - create chat
            chatService.CreateChat(userId, partner.Value);
            
            var replyMarkup = new ReplyKeyboardRemove();
            await _botClient.SendMessage(userId, BotMessages.FoundPartner, replyMarkup: replyMarkup);
            await _botClient.SendMessage(partner.Value, BotMessages.FoundPartner, replyMarkup: replyMarkup);
        }
        else
        {
            // No partner available - add to queue
            await matchmakingService.EnqueueUserAsync(userId);
            await _botClient.SendMessage(userId, BotMessages.WaitingForPartner);
        }
    }

    /// <summary>
    /// Handles /stop command - ends the current chat or stops the search.
    /// </summary>
    private async Task HandleStopAsync(long userId, IChatService chatService, IMatchmakingService matchmakingService)
    {
        var replyMarkup = GetMainKeyboard();

        // Check if user is in an active chat
        if (chatService.IsInChat(userId))
        {
            var partnerId = chatService.GetPartner(userId);
            chatService.EndChat(userId);

            await _botClient.SendMessage(userId, BotMessages.ChatEnded, replyMarkup: replyMarkup);
            
            if (partnerId.HasValue)
            {
                await _botClient.SendMessage(partnerId.Value, BotMessages.PartnerLeft, replyMarkup: replyMarkup);
            }
            return;
        }

        // Check if user is waiting in the matchmaking queue
        if (await matchmakingService.IsWaitingAsync(userId))
        {
            await matchmakingService.RemoveFromQueueAsync(userId);
            await _botClient.SendMessage(userId, BotMessages.SearchStopped, replyMarkup: replyMarkup);
            return;
        }

        // User is neither in chat nor in queue
        await _botClient.SendMessage(userId, BotMessages.NotInChat, replyMarkup: replyMarkup);
    }

    /// <summary>
    /// Handles regular messages - forwards to chat partner.
    /// </summary>
    private async Task HandleChatMessageAsync(long userId, string text, IRegistrationService registrationService, IChatService chatService)
    {
        // Check registration
        if (!registrationService.IsRegistered(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotRegistered);
            return;
        }

        if (!chatService.IsInChat(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotInChat);
            return;
        }

        var partnerId = chatService.GetPartner(userId);
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
    /// <summary>
    /// Gets the persistent keyboard menu for registered users.
    /// </summary>
    private ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonFindPartner),
                new KeyboardButton(BotMessages.MenuButtonProfile)
            },
            new[]
            {
                new KeyboardButton(BotMessages.MenuButtonStopChat)
            }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false,
            Selective = false
        };
    }

    /// <summary>
    /// Handles profile request - shows user information.
    /// </summary>
    private async Task HandleProfileAsync(long userId, IRegistrationService registrationService)
    {
        var user = registrationService.GetUser(userId);
        if (user == null)
        {
            await _botClient.SendMessage(userId, BotMessages.Error);
            return;
        }

        var genderText = user.Gender == Gender.Male ? "👨 Erkak" : "👩 Ayol";
        var profileMessage = $"👤 Sizning Profilingiz\n\n" +
            $"Jins: {genderText}\n" +
            $"Ro'yxatga olindi: {user.CreatedAt:dd.MM.yyyy HH:mm}";

        await _botClient.SendMessage(userId, profileMessage, replyMarkup: GetMainKeyboard());
    }

    /// <summary>
    /// Handles /start command - shows welcome message.
    /// </summary>
    private async Task HandleStartAsync(long userId)
    {
        await _botClient.SendMessage(userId, BotMessages.Welcome, replyMarkup: GetMainKeyboard());
    }
}
