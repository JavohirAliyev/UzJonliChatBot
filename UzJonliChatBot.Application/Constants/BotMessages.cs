namespace UzJonliChatBot.Application.Constants;

/// <summary>
/// Uzbek language messages for the chatbot.
/// </summary>
public static class BotMessages
{
    // Welcome & Registration Messages
    public const string Welcome = "👋 Uzjonli chatbotiga xush kelibsiz!\n\n" +
        "Anonim tarzda boshqa foydalanuvchilar bilan suhbat qiling.\n\n" +
        "Buyruqlar:\n" +
        "/keyingi - Yangi suhbatni boshlash\n" +
        "/stop - Suhbatni tugatish";

    public const string MenuButtonFindPartner = "🔍 Topish";
    public const string MenuButtonStopChat = "❌ Tugatish";
    public const string MenuButtonProfile = "👤 Profil";

    public const string GenderSelectionPrompt = "🚀 Boshlash uchun jinsiyatingizni tanlang:\n\n" +
        "Ushbu ma'lumot faqat suhbat sherikin tanlash uchun kerak.";

    public const string GenderButtonMale = "👨 Erkak";
    public const string GenderButtonFemale = "👩 Ayol";

    public const string AgeVerificationPrompt = "⚠️ MUHIM: Siz 18 yoki undan katta yoshdasiz ekanligini tasdiqlamoqchimiz.\n\n" +
        "Davom etish uchun pastdagi tugmani bosing.";

    public const string AgeVerificationButton = "✅ Ha, men 18+ yoshdaman";

    public const string RegistrationComplete = "✅ Registratsiya tugallandi!\n\n" +
        "Suhbatni boshlash uchun tugmalarni ishlating.";

    // Chat Messages
    public const string FoundPartner = "✅ Suhbat sherigi topildi!\n\n" +
        "Suhbatni tugatish uchun \"❌ Tugatish\" tugmasini bosing.";

    public const string WaitingForPartner = "⏳ Suhbat sheriği izlanmoqda...\n\n" +
        "Iltimos kuting yoki \"❌ Tugatish\" tugmasini bosing.";

    public const string ChatEnded = "👋 Suhbat tugatildi.\n\n" +
        "Yangi suhbatni boshlash uchun \"🔍 Topish\" tugmasini bosing.";

    public const string SearchStopped = "⏹️ Qidirish to'xtatildi.\n\n" +
        "Yangi suhbatni boshlash uchun \"🔍 Topish\" tugmasini bosing.";

    public const string AlreadyInChat = "❌ Siz allaqachon suhbatdasiz!\n\n" +
        "Suhbatni tugatish uchun \"❌ Tugatish\" tugmasini bosing.";

    public const string NotInChat = "❌ Siz hozir suhbatda emasiz.\n\n" +
        "Yangi suhbatni boshlash uchun \"🔍 Topish\" tugmasini bosing.";

    public const string PartnerLeft = "⚠️ Suhbat sherik ketdi.\n\n" +
        "Yangi suhbatni boshlash uchun \"🔍 Topish\" tugmasini bosing.";

    public const string NotRegistered = "❌ Avval ro'yxatdan o'tishingiz kerak.\n\n" +
        "/start buyrug'ini ishlating.";

    public const string Error = "❌ Xatolik yuz berdi. Iltimos qayta urinib ko'ring.";

    public static string MainMenu => "Asosiy menyu: Hamkor topish, Profil, Chatni to'xtatish.";
}
