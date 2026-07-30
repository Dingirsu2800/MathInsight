using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathInsight.Modules.Learning_Lecture.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "DiscussionQuestion",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                table: "DiscussionAnswer",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "DiscussionQuestion");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                table: "DiscussionAnswer");
        }
    }
}
