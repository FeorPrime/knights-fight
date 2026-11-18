using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnightFight.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChallengerAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AvatarBlob",
                table: "Challengers",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultAvatarIndex",
                table: "Challengers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Challengers_Avatar_SingleSource",
                table: "Challengers",
                sql: "(\"AvatarBlob\" IS NOT NULL AND \"DefaultAvatarIndex\" IS NULL) OR (\"AvatarBlob\" IS NULL AND \"DefaultAvatarIndex\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Challengers_Avatar_SingleSource",
                table: "Challengers");

            migrationBuilder.DropColumn(
                name: "AvatarBlob",
                table: "Challengers");

            migrationBuilder.DropColumn(
                name: "DefaultAvatarIndex",
                table: "Challengers");
        }
    }
}
