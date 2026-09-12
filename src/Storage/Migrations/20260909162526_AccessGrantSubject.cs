using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FifthBox.ServerManager.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AccessGrantSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessGrants_UserId_Scope_TargetId",
                table: "AccessGrants");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "AccessGrants",
                newName: "SubjectId");

            migrationBuilder.AddColumn<string>(
                name: "SubjectType",
                table: "AccessGrants",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                // Hand-set: every existing grant is a user grant, and SubjectType is stored as its
                // enum name. The scaffolded "" would not read back as a value of AccessSubject.
                defaultValue: "User");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_SubjectType_SubjectId_Scope_TargetId",
                table: "AccessGrants",
                columns: new[] { "SubjectType", "SubjectId", "Scope", "TargetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessGrants_SubjectType_SubjectId_Scope_TargetId",
                table: "AccessGrants");

            migrationBuilder.DropColumn(
                name: "SubjectType",
                table: "AccessGrants");

            migrationBuilder.RenameColumn(
                name: "SubjectId",
                table: "AccessGrants",
                newName: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessGrants_UserId_Scope_TargetId",
                table: "AccessGrants",
                columns: new[] { "UserId", "Scope", "TargetId" },
                unique: true);
        }
    }
}
