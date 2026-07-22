namespace Api.Exceptions;

/// <summary>
/// The requested resource does not exist, or does not belong to the caller.
/// </summary>
/// <remarks>
/// <b>Both cases raise this, and that is deliberate.</b> Answering 403 for "exists but is
/// someone else's" confirms the resource exists — so an attacker can enumerate ids by
/// reading status codes. Own-resource routes therefore scope the lookup to the caller and
/// treat a miss as absence (Authorization.md §11).
/// </remarks>
public sealed class ResourceNotFoundException(string resource)
    : DomainException("not_found", $"The requested {resource} was not found.");
