using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssambleaApi.Migrations
{
    /// <inheritdoc />
    public partial class addedsecondvote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecondVote",
                table: "Attendees",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecondVote",
                table: "Attendees");
        }
    }
}
