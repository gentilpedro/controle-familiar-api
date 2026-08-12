using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaNomeAlvoNaAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeAlvo",
                table: "RegistrosAuditoria",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NomeAlvo",
                table: "RegistrosAuditoria");
        }
    }
}
