using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssambleaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddReadyForVoteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReadyForFirstVote",
                table: "Attendees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReadyForSecondVote",
                table: "Attendees",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadyForFirstVote",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "ReadyForSecondVote",
                table: "Attendees");
        }
    }
}
