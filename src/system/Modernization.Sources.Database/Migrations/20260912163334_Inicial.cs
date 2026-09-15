using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modernization.Sources.Database.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "apim_onboarding",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApplicationShortName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApiType = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_apim_onboarding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "architectural_value",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ModernCount = table.Column<int>(type: "INTEGER", nullable: true),
                    HeritageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ObsoleteCount = table.Column<int>(type: "INTEGER", nullable: true),
                    NotApprovedForUseCount = table.Column<int>(type: "INTEGER", nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_architectural_value", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "eai_master",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AppId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApplicationStatus = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ApplicationDisposition = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Roadmap = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Cio = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Cre = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Lob = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    SubLob = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RegionName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CountryName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CriticalApplicationIndicator = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    InternetFacing = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CloudIndicator = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CloudType = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CloudVendor = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CodingLanguage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    DrInScope = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    UtilizesMfa = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PrimaryBusinessCapability = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    L1BusinessCapabilityCount = table.Column<int>(type: "INTEGER", nullable: true),
                    RecoveryPointObjectiveRange = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    RecoveryTimeObjectiveRange = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    UserCount = table.Column<int>(type: "INTEGER", nullable: true),
                    KennaScore = table.Column<int>(type: "INTEGER", nullable: true),
                    RawTechScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    OperationalScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eai_master", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "monitoring",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Agent = table.Column<bool>(type: "INTEGER", nullable: true),
                    Apm = table.Column<bool>(type: "INTEGER", nullable: true),
                    Monitoring = table.Column<bool>(type: "INTEGER", nullable: true),
                    Synthetics = table.Column<bool>(type: "INTEGER", nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "oss_score",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OssScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oss_score", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_strategy",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EliminateCount = table.Column<int>(type: "INTEGER", nullable: true),
                    InvestCount = table.Column<int>(type: "INTEGER", nullable: true),
                    MigrateCount = table.Column<int>(type: "INTEGER", nullable: true),
                    TolerateCount = table.Column<int>(type: "INTEGER", nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_strategy", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sca_flaw",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlawIndexNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    EaiCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ApplicationName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    AppId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SeverityDescription = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FindingStatus = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    FirstFoundDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastFoundDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ResolvedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FindingAge = table.Column<int>(type: "INTEGER", nullable: true),
                    ScanType = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ReopenFlag = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CveId = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Cvssv3Score = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    VulnerabilityName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    LibraryName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    LibraryVendor = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    PublishedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Accessibility = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sca_flaw", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "snapshot_version",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ApplicationCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_snapshot_version", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "source_rejection",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_rejection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tech_debt",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CombinedDenialScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    CombinedAcceptedScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    CombinedAppScore = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: true),
                    LastUpdatedDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    VersionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tech_debt", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_apim_onboarding_VersionId",
                table: "apim_onboarding",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_architectural_value_VersionId",
                table: "architectural_value",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_eai_master_VersionId",
                table: "eai_master",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_eai_master_VersionId_EaiCode",
                table: "eai_master",
                columns: new[] { "VersionId", "EaiCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_VersionId",
                table: "monitoring",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_oss_score_VersionId",
                table: "oss_score",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_product_strategy_VersionId",
                table: "product_strategy",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sca_flaw_VersionId",
                table: "sca_flaw",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_snapshot_version_CapturedAtUtc",
                table: "snapshot_version",
                column: "CapturedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_source_rejection_VersionId",
                table: "source_rejection",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_tech_debt_VersionId",
                table: "tech_debt",
                column: "VersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "apim_onboarding");

            migrationBuilder.DropTable(
                name: "architectural_value");

            migrationBuilder.DropTable(
                name: "eai_master");

            migrationBuilder.DropTable(
                name: "monitoring");

            migrationBuilder.DropTable(
                name: "oss_score");

            migrationBuilder.DropTable(
                name: "product_strategy");

            migrationBuilder.DropTable(
                name: "sca_flaw");

            migrationBuilder.DropTable(
                name: "snapshot_version");

            migrationBuilder.DropTable(
                name: "source_rejection");

            migrationBuilder.DropTable(
                name: "tech_debt");
        }
    }
}
