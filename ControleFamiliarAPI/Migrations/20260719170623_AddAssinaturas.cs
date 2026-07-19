using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControleFamiliarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAssinaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssinaturaFamiliaValidaAte",
                table: "Familias",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusAssinaturaFamilia",
                table: "Familias",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Familias",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionIdFamilia",
                table: "Familias",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssinaturaIndividualValidaAte",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusAssinaturaIndividual",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionIdIndividual",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrialIndividualUsado",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
                
            migrationBuilder.Sql(@"
                UPDATE f
                SET f.StatusAssinaturaFamilia = 2 -- Ativa
                FROM Familias f
                INNER JOIN AspNetUsers u ON u.FamiliaId = f.Id
                WHERE u.Id IN (1, 2, 3, 4, 5);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssinaturaFamiliaValidaAte",
                table: "Familias");

            migrationBuilder.DropColumn(
                name: "StatusAssinaturaFamilia",
                table: "Familias");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Familias");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionIdFamilia",
                table: "Familias");

            migrationBuilder.DropColumn(
                name: "AssinaturaIndividualValidaAte",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StatusAssinaturaIndividual",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionIdIndividual",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TrialIndividualUsado",
                table: "AspNetUsers");
        }
    }
}
