using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace ControleFamiliarAPI.OpenApi
{
    /// <summary>
    /// Leva os comentários <c>///</c> do código para as descrições do schema
    /// OpenAPI (tipos e propriedades).
    /// </summary>
    // Existe porque o AddOpenApi nativo do ASP.NET Core 9 não lê o arquivo de
    // documentação XML — diferente do Swashbuckle, ele não tem um
    // IncludeXmlComments. Sem isto, o que se escreve em /// <summary> fica só no
    // código-fonte e o Scalar mostra os campos como "string required", sem
    // explicar o que cada um aceita.
    internal sealed class ComentariosXmlSchemaTransformer : IOpenApiSchemaTransformer
    {
        // Chave no formato do compilador: "T:Namespace.Tipo" e
        // "P:Namespace.Tipo.Propriedade" -> texto do <summary>.
        private readonly Lazy<IReadOnlyDictionary<string, string>> _resumos =
            new(CarregarResumos, LazyThreadSafetyMode.ExecutionAndPublication);

        // Reflection por propriedade é caro para rodar a cada schema gerado.
        private static readonly ConcurrentDictionary<PropertyInfo, string?> CacheChaves = new();

        public Task TransformAsync(
            OpenApiSchema schema,
            OpenApiSchemaTransformerContext context,
            CancellationToken cancellationToken)
        {
            if (_resumos.Value.Count == 0)
                return Task.CompletedTask;

            // Propriedade de um DTO...
            var propriedade = context.JsonPropertyInfo?.AttributeProvider as PropertyInfo;
            if (propriedade is not null)
            {
                var chave = CacheChaves.GetOrAdd(propriedade, ChaveDaPropriedade);

                if (chave is not null
                    && string.IsNullOrEmpty(schema.Description)
                    && _resumos.Value.TryGetValue(chave, out var resumoPropriedade))
                {
                    schema.Description = resumoPropriedade;
                }

                return Task.CompletedTask;
            }

            // ...ou o tipo em si (inclusive os enums).
            var tipo = context.JsonTypeInfo.Type;
            if (tipo.FullName is not null
                && string.IsNullOrEmpty(schema.Description)
                && _resumos.Value.TryGetValue($"T:{tipo.FullName}", out var resumoTipo))
            {
                schema.Description = resumoTipo;
            }

            return Task.CompletedTask;
        }

        private static string? ChaveDaPropriedade(PropertyInfo propriedade)
        {
            var declarante = propriedade.DeclaringType?.FullName;
            return declarante is null ? null : $"P:{declarante}.{propriedade.Name}";
        }

        private static IReadOnlyDictionary<string, string> CarregarResumos()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var caminho = Path.Combine(
                AppContext.BaseDirectory,
                $"{assembly.GetName().Name}.xml");

            // Ausência do XML não é erro: em algumas publicações o arquivo pode
            // não ser copiado. A API sobe igual, só sem as descrições.
            if (!File.Exists(caminho))
                return new Dictionary<string, string>();

            var resumos = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var membro in XDocument.Load(caminho).Descendants("member"))
            {
                var nome = membro.Attribute("name")?.Value;
                var resumo = membro.Element("summary")?.Value;

                if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(resumo))
                    continue;

                // O XML preserva a indentação do código-fonte; vira uma linha só.
                resumos[nome] = string.Join(
                    " ",
                    resumo.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            return resumos;
        }
    }
}
