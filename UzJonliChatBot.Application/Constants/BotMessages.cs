namespace UzJonliChatBot.Application.Constants;

/// <summary>
/// Uzbek language messages for the chatbot.
/// </summary>
public static class BotMessages
{
    // Welcome & Registration Messages
    public const string Welcome = "👋 Tanishuv chatga xush kelibsiz!\n\n" +
        "Anonim suhbatlar orqali yangi do'stlar toping.\n\n" +
        "/keyingi - Yangi suhbat\n" +
        "/stop - Hozirgi suhbatni tugatish";

    public const string MenuButtonFindPartner = "🔍 Suhbatdosh topish";
    public const string MenuButtonStopChat = "⏹️ Hozirgi suhbatni tugatish";
    public const string MenuButtonProfile = "👤 Profil";

    public const string GenderSelectionPrompt = "👋 Boshladik!\n\n" +
        "Jinsingizni tanlang:";

    public const string GenderButtonMale = "👨 Erkak";
    public const string GenderButtonFemale = "👩 Ayol";

    public const string AgeVerificationPrompt = "⚠️ Davom etish uchun kamida 18 yosh ekanligingizni tasdiqlang.";

    public const string AgeVerificationButton = "✅ Tasdiqlash";

    public const string RegistrationComplete = "✅ Tayyor!\n\n" +
        "Suhbatdosh topish uchun /keyingi tugmasini bosing.";

    // Chat Messages
    public const string FoundPartner = "🎉 Suhbatdosh topildi!\n\n" +
        "Suhbatni boshlang 👇\n\n" +
        "Suhbatni tugatish uchun /stop tugmasini bosing.\n\n" +
        "Yangi suhbat uchun /keyingi tugmasini bosing.";

    public const string WaitingForPartner = "⏳ Suhbatdosh qidirilmoqda...\n\n" +
        "Iltimos kuting 😊\n\n" + 
        "Qidiruvni to'xtatish uchun /stop tugmasini bosing.";

    public const string ChatEnded = "👋 Suhbat tugatildi.\n\n" +
        "Yangi suhbat uchun /keyingi tugmasini bosing.";

    public const string SearchStopped = "⏹️ Qidiruv to'xtatildi.";

    public const string AlreadyInChat = "💬 Siz allaqachon suhbatdasiz!\n\n" +
        "Suhbatni tugatish uchun /stop tugmasini bosing.\n\n" +
        "Yangi suhbat uchun /keyingi tugmasini bosing.";

    public const string NotInChat = "❌ Hozir suhbatda emassiz.";

    public const string PartnerLeft = "💔 Suhbatdosh ketdi.\n\n" +
        "Yangi suhbat uchun \"🔍 Suhbatdosh topish\" yoki /keyingi tugmasini bosing.";

    public const string NotRegistered = "❌ Avval ro'yxatdan o'tish kerak.\n\n" +
        "/start buyrug'ini ishlating.";

    public const string UserBanned = "🚫 Sizning hisobingiz bloklangan.\n\n" +
        "Ma'lumot uchun administrator bilan bog'laning.";

    public const string Error = "❌ Xatolik! Qayta urinib ko'ring.";
}
