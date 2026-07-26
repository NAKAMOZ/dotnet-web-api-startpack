namespace Api.Exceptions;

public sealed class ForbiddenOperationException()
    : DomainException(ErrorCodes.Forbidden, "The operation is not permitted.");
