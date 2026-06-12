using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonitorApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataQualityRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleName = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    ViolationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataQualityRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceHealthRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceName = table.Column<string>(type: "TEXT", nullable: false),
                    Environment = table.Column<string>(type: "TEXT", nullable: false),
                    OverallStatus = table.Column<string>(type: "TEXT", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: true),
                    DbServer = table.Column<string>(type: "TEXT", nullable: true),
                    ActualDatabase = table.Column<string>(type: "TEXT", nullable: true),
                    ExpectedDatabase = table.Column<string>(type: "TEXT", nullable: true),
                    DbMatch = table.Column<bool>(type: "INTEGER", nullable: true),
                    DbConnected = table.Column<bool>(type: "INTEGER", nullable: true),
                    RawJson = table.Column<string>(type: "TEXT", nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceHealthRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataQualityRecords_RuleName_Environment_CheckedAt",
                table: "DataQualityRecords",
                columns: new[] { "RuleName", "Environment", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceHealthRecords_ServiceName_Environment_CheckedAt",
                table: "ServiceHealthRecords",
                columns: new[] { "ServiceName", "Environment", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataQualityRecords");

            migrationBuilder.DropTable(
                name: "ServiceHealthRecords");
        }
    }
}
