namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// A request error with an HTTP status: 404 unknown app or record, 400 bad request, 403 access denied
/// (spec 07, 4.3; spec 06, 4.3). The App filter changes it to a problem response. No null returns.
/// </summary>
public sealed class AtlasProblemException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static AtlasProblemException NotFound(string message) => new(404, message);

    public static AtlasProblemException BadRequest(string message) => new(400, message);

    public static AtlasProblemException Forbidden(string message) => new(403, message);

    /// <summary>
    /// A defect of the app config that the start-up check also reports (review SV-11): 500, logged as Atlas.ConfigError.
    /// The request is not the cause, so it is not a 400.
    /// </summary>
    public static AtlasProblemException Config(string message) => new(500, message) { IsConfigError = true };

    public bool IsConfigError { get; private init; }
}
