using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSerieNaTransacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroParcela",
                table: "Transacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerieId",
                table: "Transacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalParcelas",
                table: "Transacoes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_SerieId_NumeroParcela",
                table: "Transacoes",
                columns: new[] { "SerieId", "NumeroParcela" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transacoes_SerieId_NumeroParcela",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "NumeroParcela",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "SerieId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "TotalParcelas",
                table: "Transacoes");
        }
    }
}
