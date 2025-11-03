using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DNDProject.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Containers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Containers",
                keyColumn: "Id",
                keyValue: 1,
                column: "ExternalId",
                value: null);

            migrationBuilder.UpdateData(
                table: "Containers",
                keyColumn: "Id",
                keyValue: 2,
                column: "ExternalId",
                value: null);

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 1,
                column: "Timestamp",
                value: new DateTime(2025, 10, 13, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 2,
                column: "Timestamp",
                value: new DateTime(2025, 10, 27, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 3,
                column: "Timestamp",
                value: new DateTime(2025, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Containers");

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 1,
                column: "Timestamp",
                value: new DateTime(2025, 9, 8, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 2,
                column: "Timestamp",
                value: new DateTime(2025, 9, 22, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "PickupEvents",
                keyColumn: "Id",
                keyValue: 3,
                column: "Timestamp",
                value: new DateTime(2025, 9, 15, 0, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}
