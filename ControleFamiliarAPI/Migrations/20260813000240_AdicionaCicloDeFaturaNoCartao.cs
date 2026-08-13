using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <summary>
    /// Transforma uma forma de pagamento da família em cartão de crédito:
    /// dia de fechamento, dia de vencimento e a categoria onde o pagamento da
    /// fatura é lançado.
    /// </summary>
    // As três colunas são anuláveis e continuam assim: forma de pagamento
    // comum (Pix, dinheiro, débito) não tem ciclo nenhum, e é a maioria. Não
    // há tabela de Fatura — ela é calculada a partir das transações
    // (FaturaService), então esta migration não cria nada além das colunas.
    public partial class AdicionaCicloDeFaturaNoCartao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaFaturaId",
                table: "FormasPagamento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaFechamento",
                table: "FormasPagamento",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaVencimento",
                table: "FormasPagamento",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FormasPagamento_CategoriaFaturaId",
                table: "FormasPagamento",
                column: "CategoriaFaturaId");

            migrationBuilder.AddForeignKey(
                name: "FK_FormasPagamento_Categorias_CategoriaFaturaId",
                table: "FormasPagamento",
                column: "CategoriaFaturaId",
                principalTable: "Categorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FormasPagamento_Categorias_CategoriaFaturaId",
                table: "FormasPagamento");

            migrationBuilder.DropIndex(
                name: "IX_FormasPagamento_CategoriaFaturaId",
                table: "FormasPagamento");

            migrationBuilder.DropColumn(
                name: "CategoriaFaturaId",
                table: "FormasPagamento");

            migrationBuilder.DropColumn(
                name: "DiaFechamento",
                table: "FormasPagamento");

            migrationBuilder.DropColumn(
                name: "DiaVencimento",
                table: "FormasPagamento");
        }
    }
}
