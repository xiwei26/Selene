namespace SeleneNative.Core.Services;

public sealed class ApiException : Exception
{
    public int? StatusCode { get; }

    public bool FeatureDisabled { get; }

    public ApiException(string message, int? statusCode = null, bool featureDisabled = false)
        : base(message)
    {
        StatusCode = statusCode;
        FeatureDisabled = featureDisabled;
    }

    public static ApiException FeatureDisabledError(string message, int? statusCode = null)
        => new(message, statusCode, featureDisabled: true);
}
