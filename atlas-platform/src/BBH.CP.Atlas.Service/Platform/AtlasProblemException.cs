namespace BBH.CP.Atlas.Service.Platform;

/// <summary>
/// A request error with an HTTP status.
/// </summary>
/// <remarks>
/// The App filter changes this exception to a problem response.
/// The platform services throw this exception and do not return null for an unknown app or record.
/// </remarks>
public sealed class AtlasProblemException(int statusCode, string message) : Exception(message)
{
    /// <summary>The HTTP status code of the response.</summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>Creates a 404 error for an unknown app or record.</summary>
    public static AtlasProblemException NotFound(string message) => new(404, message);

    /// <summary>Creates a 400 error for a bad request.</summary>
    public static AtlasProblemException BadRequest(string message) => new(400, message);

    /// <summary>Creates a 403 error for a refused access.</summary>
    public static AtlasProblemException Forbidden(string message) => new(403, message);

    /// <summary>
    /// Creates a 500 error for a defect in the app config. The App filter logs it as Atlas.ConfigError.
    /// </summary>
    /// <remarks>
    /// The start-up check also reports this defect. The request is not the cause, so the status is not 400.
    /// </remarks>
    public static AtlasProblemException Config(string message) => new(500, message) { IsConfigError = true };

    /// <summary>True when the cause is a defect in the app config.</summary>
    public bool IsConfigError { get; private init; }
}
