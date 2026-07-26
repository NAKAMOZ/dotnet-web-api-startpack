namespace Api.Exceptions;

public sealed class UnsupportedProviderException()
    : DomainException("unsupported_provider", "The social authentication provider is not supported.");
