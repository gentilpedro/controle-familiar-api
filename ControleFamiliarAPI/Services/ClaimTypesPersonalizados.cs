namespace ControleFamiliarAPI.Services
{
    /// <summary>
    /// Nomes das claims customizadas incluídas no JWT (ver
    /// AuthService.GerarToken) e lidas de volta em CurrentUserService.
    /// Centralizados aqui pra evitar dois literais de string independentes e
    /// potencialmente desalinhados entre os dois arquivos.
    /// </summary>
    public static class ClaimTypesPersonalizados
    {
        public const string FamiliaId = "familiaId";

        public const string Nome = "nome";
    }
}
