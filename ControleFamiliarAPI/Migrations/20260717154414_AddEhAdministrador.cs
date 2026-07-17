using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEhAdministrador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EhAdministrador",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Backfill: para contas criadas antes desse recurso existir, o
            // usuario mais antigo (menor Id) de cada familia vira admin --
            // na pratica, quem criou a familia primeiro.
            migrationBuilder.Sql(@"
                UPDATE u
                SET u.EhAdministrador = 1
                FROM AspNetUsers u
                INNER JOIN (
                    SELECT FamiliaId, MIN(Id) AS MinId
                    FROM AspNetUsers
                    GROUP BY FamiliaId
                ) primeiros ON u.FamiliaId = primeiros.FamiliaId AND u.Id = primeiros.MinId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EhAdministrador",
                table: "AspNetUsers");
        }
    }
}
