using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listo.Notification.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeviceEntity_AddTenantIdAndAppVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_UserId_Active",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "Devices",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceToken",
                table: "Devices",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceInfo",
                table: "Devices",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "Devices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Devices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_Platform_Active",
                table: "Devices",
                columns: new[] { "TenantId", "Platform", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TenantId_UserId_Active",
                table: "Devices",
                columns: new[] { "TenantId", "UserId", "Active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Devices_TenantId_Platform_Active",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_TenantId_UserId_Active",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "Platform",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "DeviceToken",
                table: "Devices",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "DeviceInfo",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_Active",
                table: "Devices",
                columns: new[] { "UserId", "Active" });
        }
    }
}
