using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssambleaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddedTimeParameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InterventionAcceptDeadline",
                table: "Attendees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InterventionAccepted",
                table: "Attendees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "InterventionStartTime",
                table: "Attendees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpeaking",
                table: "Attendees",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InterventionAcceptDeadline",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "InterventionAccepted",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "InterventionStartTime",
                table: "Attendees");

            migrationBuilder.DropColumn(
                name: "IsSpeaking",
                table: "Attendees");
        }
    }
}
