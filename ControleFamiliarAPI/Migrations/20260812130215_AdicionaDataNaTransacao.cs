using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDataNaTransacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Três passos, não um AddColumn direto com defaultValue: uma
            // transação existente nunca teve data — não há dado de origem
            // pra copiar. A escolha de valor pras linhas antigas é decisão
            // de produto, não mecânica de migration (avaliado explicitamente
            // antes de implementar): a data do deploy desta migration é a
            // única opção honesta — não inventa uma ordem cronológica que
            // nunca existiu, só marca "o sistema passou a saber a partir de
            // agora". Artefato conhecido, documentado aqui de propósito.
            migrationBuilder.AddColumn<DateOnly>(
                name: "Data",
                table: "Transacoes",
                type: "date",
                nullable: true);

            migrationBuilder.Sql("UPDATE Transacoes SET Data = '2026-08-12' WHERE Data IS NULL;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "Data",
                table: "Transacoes",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_FamiliaId_Data",
                table: "Transacoes",
                columns: new[] { "FamiliaId", "Data" })
                .Annotation("SqlServer:Include", new[] { "Valor", "Tipo", "CategoriaId", "PessoaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transacoes_FamiliaId_Data",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "Data",
                table: "Transacoes");
        }
    }
}
