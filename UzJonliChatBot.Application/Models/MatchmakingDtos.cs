namespace UzJonliChatBot.Application.Models;

public enum UserNextState
{
    NotRegistered,
    InChat,
    AlreadyWaiting,
    ReadyToMatch
}

public enum MatchStatus
{
    SelfMatched,
    PartnerFound,
    Enqueued
}

public class UserNextStatusDto
{
    public UserNextState State { get; set; }
    public long? PartnerId { get; set; }
}

public class MatchResultDto
{
    public MatchStatus Status { get; set; }
    public long? PartnerId { get; set; }
}