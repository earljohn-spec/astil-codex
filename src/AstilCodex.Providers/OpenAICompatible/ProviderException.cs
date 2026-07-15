using System.Net;

namespace AstilCodex.Providers.OpenAICompatible;

public sealed class ProviderException : Exception
{
    public ProviderException(
        string code,
        string message,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public HttpStatusCode? StatusCode { get; }
}
