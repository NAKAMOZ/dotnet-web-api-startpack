namespace Api.Exceptions;

/// <summary>
/// Base for failures that are part of the domain rather than faults — an expected outcome
/// the caller needs told about.
/// </summary>
/// <remarks>
/// Exceptions rather than <c>(bool ok, string error)</c> tuples: a tuple's error arm is
/// ignorable, and the compiler never notices when a caller drops it. §14 translates every
/// subclass centrally into RFC 9457 Problem Details, so a service never constructs an HTTP
/// response and a controller never maps an error.
/// <para>
/// <see cref="ErrorCode"/> is the stable machine-readable identity, mirroring the validation
/// codes in §10. Messages are prose and may be reworded; codes are the contract.
/// </para>
/// </remarks>
public abstract class DomainException(string errorCode, string message) : Exception(message)
{
    /// <summary>Stable snake_case identifier, surfaced in the Problem Details payload.</summary>
    public string ErrorCode { get; } = errorCode;
}
