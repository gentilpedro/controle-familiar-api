using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDivisaoPercentualNaCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AceitaDivisaoPercentual",
                table: "Categorias",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Mesmo padrão de SeedCategoriasDoSistema: só a categoria de
            // sistema "Salário" (FamiliaId nulo) libera o fluxo de divisão
            // percentual — categoria de família nunca ganha isso, e como
            // categoria de sistema é imutável, essa trava não depende de
            // comparar nome de categoria em runtime, só desta atualização
            // pontual no cadastro.
            migrationBuilder.Sql(
                "UPDATE Categorias SET AceitaDivisaoPercentual = 1 WHERE Descricao = 'Salário' AND FamiliaId IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AceitaDivisaoPercentual",
                table: "Categorias");
        }
    }
}
