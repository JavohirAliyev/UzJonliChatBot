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
    private readonly IRegistrationService _registrationService;
    private readonly IMatchmakingService _matchmakingService;
    private readonly IChatService _chatService;
    private readonly ITelegramBotClient _botClient;

    public TelegramUpdateHandler(
        IRegistrationService registrationService,
        IMatchmakingService matchmakingService,
        IChatService chatService,
        ITelegramBotClient botClient)
    {
        _registrationService = registrationService;
        _matchmakingService = matchmakingService;
        _chatService = chatService;
        _botClient = botClient;
    }

    /// <summary>
    /// Handles incoming updates from Telegram.
    /// </summary>
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Message?.Type == MessageType.Text)
            {
                await HandleMessageAsync(update.Message);
            }
            else if (update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling update: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles text messages and commands.
    /// </summary>
    private async Task HandleMessageAsync(Message message)
    {
        var userId = message.Chat.Id;
        var text = message.Text ?? string.Empty;

        if (text.StartsWith("/start"))
        {
            await HandleStartAsync(userId);
        }
        else if (text.StartsWith("/keyingi") || text == BotMessages.MenuButtonFindPartner)
        {
            await HandleNextAsync(userId);
        }
        else if (text.StartsWith("/stop") || text == BotMessages.MenuButtonStopChat)
        {
            await HandleStopAsync(userId);
        }
        else if (text == BotMessages.MenuButtonProfile)
        {
            await HandleProfileAsync(userId);
        }
        else
        {
            await HandleChatMessageAsync(userId, text);
        }
    }

    /// <summary>
    /// Handles callback queries from buttons.
    /// </summary>
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        var userId = callbackQuery.From.Id;
        var data = callbackQuery.Data ?? string.Empty;
        var callbackId = callbackQuery.Id;

        try
        {
            if (data.StartsWith("gender_"))
            {
                await HandleGenderSelectionAsync(userId, data, callbackId);
            }
            else if (data == "age_verified")
            {
                await HandleAgeVerificationAsync(userId, callbackId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling callback: {ex.Message}");
            await _botClient.AnswerCallbackQuery(callbackId, BotMessages.Error, showAlert: true);
        }
    }

    /// <summary>
    /// Handles /start command - begins registration flow.
    /// </summary>
    private async Task HandleStartAsync(long userId)
    {
        var status = _registrationService.GetRegistrationStatus(userId);

        if (status == UserRegistrationStatus.Registered)
        {
            await HandleMenuAsync(userId);
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
    private async Task HandleGenderSelectionAsync(long userId, string data, string callbackId)
    {
        var gender = data == "gender_male" ? Gender.Male : Gender.Female;
        _registrationService.SetGender(userId, gender);

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
    private async Task HandleAgeVerificationAsync(long userId, string callbackId)
    {
        _registrationService.ConfirmAge(userId);
        await _botClient.SendMessage(userId, BotMessages.RegistrationComplete, replyMarkup: GetMainKeyboard());
        await _botClient.AnswerCallbackQuery(callbackId);
    }

    /// <summary>
    /// Handles /keyingi command - starts searching for a chat partner.
    /// </summary>
    private async Task HandleNextAsync(long userId)
    {
        // Check if user is registered
        if (!_registrationService.IsRegistered(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotRegistered);
            return;
        }

        // Check if user is already in a chat
        if (_chatService.IsInChat(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.AlreadyInChat);
            return;
        }

        // Check if user is already waiting
        if (_matchmakingService.IsWaiting(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.WaitingForPartner);
            return;
        }

        // Try to find a partner
        var partner = _matchmakingService.DequeueUser();

        if (partner.HasValue)
        {
            // Found a partner - create chat
            _chatService.CreateChat(userId, partner.Value);
            
            var replyMarkup = new ReplyKeyboardRemove();
            await _botClient.SendMessage(userId, BotMessages.FoundPartner, replyMarkup: replyMarkup);
            await _botClient.SendMessage(partner.Value, BotMessages.FoundPartner, replyMarkup: replyMarkup);
        }
        else
        {
            // No partner available - add to queue
            _matchmakingService.EnqueueUser(userId);
            await _botClient.SendMessage(userId, BotMessages.WaitingForPartner);
        }
    }

    /// <summary>
    /// Handles /stop command - ends the current chat or stops the search.
    /// </summary>
    private async Task HandleStopAsync(long userId)
    {
        var replyMarkup = GetMainKeyboard();

        // Check if user is in an active chat
        if (_chatService.IsInChat(userId))
        {
            var partnerId = _chatService.GetPartner(userId);
            _chatService.EndChat(userId);

            await _botClient.SendMessage(userId, BotMessages.ChatEnded, replyMarkup: replyMarkup);
            
            if (partnerId.HasValue)
            {
                await _botClient.SendMessage(partnerId.Value, BotMessages.PartnerLeft, replyMarkup: replyMarkup);
            }
            return;
        }

        // Check if user is waiting in the matchmaking queue
        if (_matchmakingService.IsWaiting(userId))
        {
            _matchmakingService.RemoveFromQueue(userId);
            await _botClient.SendMessage(userId, BotMessages.SearchStopped, replyMarkup: replyMarkup);
            return;
        }

        // User is neither in chat nor in queue
        await _botClient.SendMessage(userId, BotMessages.NotInChat, replyMarkup: replyMarkup);
    }

    /// <summary>
    /// Handles regular messages - forwards to chat partner.
    /// </summary>
    private async Task HandleChatMessageAsync(long userId, string text)
    {
        // Check registration
        if (!_registrationService.IsRegistered(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotRegistered);
            return;
        }

        if (!_chatService.IsInChat(userId))
        {
            await _botClient.SendMessage(userId, BotMessages.NotInChat);
            return;
        }

        var partnerId = _chatService.GetPartner(userId);
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
            Console.WriteLine($"Error forwarding message: {ex.Message}");
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
    private async Task HandleProfileAsync(long userId)
    {
        var user = _registrationService.GetUser(userId);
        if (user == null)
        {
            await _botClient.SendMessage(userId, BotMessages.Error);
            return;
        }

        var genderText = user.Gender == Gender.Male ? "👨 Erkak" : "👩 Ayol";
        var profileMessage = $"👤 Sizning Profilingiz\n\n" +
            $"Jinsiyat: {genderText}\n" +
            $"Yosh: 18+\n" +
            $"Ro'yxatga olindi: {user.CreatedAt:dd.MM.yyyy}";

        await _botClient.SendMessage(userId, profileMessage, replyMarkup: GetMainKeyboard());
    }

    /// <summary>
    /// Shows the main menu to the user.
    /// </summary>
    private async Task HandleMenuAsync(long userId)
    {
        await _botClient.SendMessage(userId, BotMessages.MainMenu, replyMarkup: GetMainKeyboard());
    }
}
