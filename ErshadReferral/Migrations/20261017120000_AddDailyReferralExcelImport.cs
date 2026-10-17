using ErshadReferral.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErshadReferral.Migrations;

public partial class AddDailyReferralExcelImport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "NationalCode",
            table: "People",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(10)",
            oldMaxLength: 10);

        migrationBuilder.AddColumn<bool>(
            name: "IsVisibleToBranches",
            table: "Referrals",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "ImportFingerprint",
            table: "Referrals",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ReferralImportBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "int").Annotation("SqlServer:Identity", "1, 1"),
                FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UploadedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                TotalRows = table.Column<int>(type: "int", nullable: false),
                ImportedRows = table.Column<int>(type: "int", nullable: false),
                SkippedDuplicateRows = table.Column<int>(type: "int", nullable: false),
                NewPeople = table.Column<int>(type: "int", nullable: false),
                ReusedPeople = table.Column<int>(type: "int", nullable: false),
                MappedBranchRows = table.Column<int>(type: "int", nullable: false),
                UnmappedBranchRows = table.Column<int>(type: "int", nullable: false),
                MappedSubjectRows = table.Column<int>(type: "int", nullable: false),
                UnmappedSubjectRows = table.Column<int>(type: "int", nullable: false),
                InvalidNationalCodeRows = table.Column<int>(type: "int", nullable: false),
                MissingLastNameRows = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ReferralImportBatches", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ReferralImportAliases",
            columns: table => new
            {
                Id = table.Column<int>(type: "int").Annotation("SqlServer:Identity", "1, 1"),
                Type = table.Column<int>(type: "int", nullable: false),
                SourceValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                NormalizedSourceValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                TargetBranchId = table.Column<int>(type: "int", nullable: true),
                TargetSubject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReferralImportAliases", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReferralImportAliases_Branches_TargetBranchId",
                    column: x => x.TargetBranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Referrals_ImportFingerprint",
            table: "Referrals",
            column: "ImportFingerprint",
            unique: true,
            filter: "[ImportFingerprint] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ReferralImportAliases_TargetBranchId",
            table: "ReferralImportAliases",
            column: "TargetBranchId");

        migrationBuilder.CreateIndex(
            name: "IX_ReferralImportAliases_Type_NormalizedSourceValue",
            table: "ReferralImportAliases",
            columns: new[] { "Type", "NormalizedSourceValue" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReferralImportBatches_FileSha256",
            table: "ReferralImportBatches",
            column: "FileSha256",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReferralImportAliases");
        migrationBuilder.DropTable(name: "ReferralImportBatches");
        migrationBuilder.DropIndex(name: "IX_Referrals_ImportFingerprint", table: "Referrals");
        migrationBuilder.DropColumn(name: "ImportFingerprint", table: "Referrals");
        migrationBuilder.DropColumn(name: "IsVisibleToBranches", table: "Referrals");

        migrationBuilder.AlterColumn<string>(
            name: "NationalCode",
            table: "People",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20);
    }
}
