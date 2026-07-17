namespace ControleFamiliarAPI.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Message { get; set; }

        // Construtor sem parâmetros: necessário só para desserialização
        // (System.Text.Json exige um construtor parameterless ou um único
        // construtor parametrizado — esta classe já tinha dois). Não é usado
        // em nenhum código de produção, que continua usando os construtores
        // abaixo.
        public ApiResponse()
        {
        }

        public ApiResponse(T data)
        {
            Success = true;
            Data = data;
        }
        public ApiResponse(string message)
        {
            Success = false;
            Message = message;
        }
    }
}