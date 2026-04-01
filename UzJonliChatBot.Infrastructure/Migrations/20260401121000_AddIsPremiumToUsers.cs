using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UzJonliChatBot.Infrastructure.Persistence;

#nullable disable

namespace UzJonliChatBot.Infrastructure.Migrations
{
    [DbContext(typeof(ChatBotDbContext))]
    [Migration("20260401121000_AddIsPremiumToUsers")]
    public partial class AddIsPremiumToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GenderPreference",
                table: "MatchmakingQueue",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPremium",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GenderPreference",
                table: "MatchmakingQueue");

            migrationBuilder.DropColumn(
                name: "IsPremium",
                table: "Users");
        }
    }
}
