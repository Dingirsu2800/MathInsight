using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathInsight.Modules.Learning_Lecture.Migrations
{
    /// <inheritdoc />
    public partial class AddNextLectureId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NextLectureId",
                table: "Lecture",
                type: "varchar(36)",
                unicode: false,
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lecture_NextLectureId",
                table: "Lecture",
                column: "NextLectureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lecture_Lecture_NextLectureId",
                table: "Lecture",
                column: "NextLectureId",
                principalTable: "Lecture",
                principalColumn: "LectureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lecture_Lecture_NextLectureId",
                table: "Lecture");

            migrationBuilder.DropIndex(
                name: "IX_Lecture_NextLectureId",
                table: "Lecture");

            migrationBuilder.DropColumn(
                name: "NextLectureId",
                table: "Lecture");
        }
    }
}
