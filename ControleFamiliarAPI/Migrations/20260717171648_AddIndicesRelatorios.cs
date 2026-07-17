using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicesRelatorios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transacoes_FamiliaId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_PessoaId",
                table: "Transacoes");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_FamiliaId_Tipo",
                table: "Transacoes",
                columns: new[] { "FamiliaId", "Tipo" })
                .Annotation("SqlServer:Include", new[] { "Valor", "CategoriaId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_PessoaId_Tipo",
                table: "Transacoes",
                columns: new[] { "PessoaId", "Tipo" })
                .Annotation("SqlServer:Include", new[] { "Valor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transacoes_FamiliaId_Tipo",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_PessoaId_Tipo",
                table: "Transacoes");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_FamiliaId",
                table: "Transacoes",
                column: "FamiliaId");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_PessoaId",
                table: "Transacoes",
                column: "PessoaId");
        }
    }
}
