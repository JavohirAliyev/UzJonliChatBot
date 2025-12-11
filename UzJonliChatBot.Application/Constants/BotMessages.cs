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
        "/stop - Chiqish";

    public const string MenuButtonFindPartner = "🔍 Suhbatdosh topish";
    public const string MenuButtonStopChat = "⏹️ Tugatish";
    public const string MenuButtonProfile = "👤 Profil";

    public const string GenderSelectionPrompt = "👋 Boshladik!\n\n" +
        "Jinsingizni tanlang:";

    public const string GenderButtonMale = "👨 Erkak";
    public const string GenderButtonFemale = "👩 Ayol";

    public const string AgeVerificationPrompt = "⚠️ Davom etish uchun kamida 18 yosh ekanligingizni tasdiqlang.";

    public const string AgeVerificationButton = "✅ Tasdiqlash";

    public const string RegistrationComplete = "✅ Tayyor!\n\n" +
        "Suhbatdosh topish uchun \"🔍 Suhbatdosh topish\" yoki /keyingi tugmasini bosing.";

    // Chat Messages
    public const string FoundPartner = "🎉 Suhbatdosh topildi!\n\n" +
        "Suhbatni boshlang 👇";

    public const string WaitingForPartner = "⏳ Suhbatdosh qidirilmoqda...\n\n" +
        "Iltimos kuting 😊";

    public const string ChatEnded = "👋 Suhbat tugatildi.\n\n" +
        "Yangi suhbat uchun \"🔍 Suhbatdosh topish\" yoki /keyingi tugmasini bosing.";

    public const string SearchStopped = "⏹️ Qidiruv to'xtatildi.";

    public const string AlreadyInChat = "💬 Siz allaqachon suhbatdasiz!";

    public const string NotInChat = "❌ Hozir suhbatda emasiz.";

    public const string PartnerLeft = "💔 Suhbatdosh ketdi.\n\n" +
        "Yangi suhbat uchun \"🔍 Suhbatdosh topish\" yoki /keyingi tugmasini bosing.";

    public const string NotRegistered = "❌ Avval ro'yxatdan o'tish kerak.\n\n" +
        "/start buyrug'ini ishlating.";

    public const string Error = "❌ Xatolik! Qayta urinib ko'ring.";

    public static string MainMenu => "📱 Menyu:\n\n" +
    "Buyruqlar:\n" +
    "/keyingi - Yangi suhbat\n" +
    "/stop - Chiqish\n\n" +
    "Tugmalar:\n" +
    "🔍 Suhbatdosh topish\n" +
    "⏹️ Tugatish\n" +
    "👤 Profil";
}
