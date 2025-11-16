using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listo.Notification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PostInitialModelUpdates_20251020 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "NotificationQueue",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NotificationQueue",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "AuditLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AuditLog",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RateLimiting",
                columns: table => new
                {
                    ConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceOrigin = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PerUserWindowSeconds = table.Column<int>(type: "int", nullable: false),
                    PerUserMax = table.Column<int>(type: "int", nullable: false),
                    PerUserMaxCap = table.Column<int>(type: "int", nullable: false),
                    PerServiceWindowSeconds = table.Column<int>(type: "int", nullable: false),
                    PerServiceMax = table.Column<int>(type: "int", nullable: false),
                    PerServiceMaxCap = table.Column<int>(type: "int", nullable: false),
                    BurstSize = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RateLimiting", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "RetryPolicy",
                columns: table => new
                {
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceOrigin = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    BaseDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    BackoffFactor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    JitterMs = table.Column<int>(type: "int", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetryPolicy", x => x.PolicyId);
                });

            migrationBuilder.CreateIndex(
                name: "UX_RateLimiting_TenantId_ServiceOrigin_Channel",
                table: "RateLimiting",
                columns: new[] { "TenantId", "ServiceOrigin", "Channel" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_RetryPolicy_ServiceOrigin_Channel",
                table: "RetryPolicy",
                columns: new[] { "ServiceOrigin", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RateLimiting");

            migrationBuilder.DropTable(
                name: "RetryPolicy");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "NotificationQueue");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NotificationQueue");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AuditLog");
        }
    }
}
