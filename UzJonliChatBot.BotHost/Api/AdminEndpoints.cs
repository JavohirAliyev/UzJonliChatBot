using Microsoft.AspNetCore.Mvc;
using UzJonliChatBot.Application.Interfaces;

namespace UzJonliChatBot.BotHost.Api;

/// <summary>
/// Admin API endpoints for user management.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>
    /// Maps all admin-related endpoints.
    /// </summary>
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/admin")
            .WithTags("Admin");

        adminGroup.MapPost("/login", LoginAsync)
            .WithName("AdminLogin");

        adminGroup.MapGet("/users", GetUsersAsync)
            .RequireAuthorization("AdminOnly")
            .WithName("GetUsers");

        adminGroup.MapPost("/users/{telegramId}/ban", BanUserAsync)
            .RequireAuthorization("AdminOnly")
            .WithName("BanUser");

        adminGroup.MapPost("/users/{telegramId}/unban", UnbanUserAsync)
            .RequireAuthorization("AdminOnly")
            .WithName("UnbanUser");
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] IAdminService adminService,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            var token = await adminService.LoginAsync(request.Username, request.Password);
            if (token == null)
                return Results.Unauthorized();

            return Results.Ok(new { token });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during admin login");
            return Results.Problem("An error occurred during login");
        }
    }

    private static async Task<IResult> GetUsersAsync(
        [FromServices] IUserRepository userRepository,
        [FromServices] ILogger<Program> logger,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        try
        {
            var (users, totalCount) = await userRepository.GetAllUsersAsync(page, pageSize, search);

            var response = new
            {
                users = users.Select(u => new
                {
                    telegramId = u.TelegramId,
                    fullName = u.FullName,
                    username = u.Username,
                    gender = u.Gender?.ToString(),
                    isAgeVerified = u.IsAgeVerified,
                    registrationStatus = u.RegistrationStatus.ToString(),
                    isBanned = u.IsBanned,
                    createdAt = u.CreatedAt,
                    updatedAt = u.UpdatedAt
                }),
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching users");
            return Results.Problem("An error occurred while fetching users");
        }
    }

    private static async Task<IResult> BanUserAsync(
        [FromRoute] long telegramId,
        [FromServices] IUserRepository userRepository,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            await userRepository.BanUserAsync(telegramId);
            return Results.Ok(new { message = "User banned successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error banning user {TelegramId}", telegramId);
            return Results.Problem("An error occurred while banning the user");
        }
    }

    private static async Task<IResult> UnbanUserAsync(
        [FromRoute] long telegramId,
        [FromServices] IUserRepository userRepository,
        [FromServices] ILogger<Program> logger)
    {
        try
        {
            await userRepository.UnbanUserAsync(telegramId);
            return Results.Ok(new { message = "User unbanned successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unbanning user {TelegramId}", telegramId);
            return Results.Problem("An error occurred while unbanning the user");
        }
    }
}

/// <summary>
/// Login request model.
/// </summary>
public record LoginRequest(string Username, string Password);
