using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DentalClinic.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "Appointments",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Appointments that already exist get their own random token before the unique index is built
            // (128 random bits as hex, created by SQLite itself), otherwise they would all share the empty default.
            migrationBuilder.Sql("UPDATE Appointments SET AccessToken = lower(hex(randomblob(16))) WHERE AccessToken = '';");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_AccessToken",
                table: "Appointments",
                column: "AccessToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_AccessToken",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "Appointments");
        }
    }
}
