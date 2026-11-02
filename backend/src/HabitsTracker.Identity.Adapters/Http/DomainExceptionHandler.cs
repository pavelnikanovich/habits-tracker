using System.Diagnostics;
using HabitsTracker.Identity.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>
/// Turns an exception into a problem at the edge (data-model.md, "Error model"): a <see cref="DomainException"/> into
/// the status and code of its <see cref="DomainError"/>; anything else into <c>500</c> <c>internal-error</c>, with no
/// detail of the failure beyond <c>traceId</c> (research R19, I5). The detail goes to the log: ASP.NET Core does not log
/// an exception a handler answers, so every <c>500</c> is logged here, with its exception and the request's trace id.
/// </summary>
public sealed partial class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    private static readonly (int Status, string Code) InternalError = (StatusCodes.Status500InternalServerError, ProblemCodes.InternalError);

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var (status, code) = exception is DomainException domain ? ProblemOf(domain.Error) : InternalError;
        if (status == StatusCodes.Status500InternalServerError)
            LogUnexpectedFailure(logger, exception);

        await Problems.Create(status, code, retryAt: null).ExecuteAsync(httpContext);
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "The request failed unexpectedly.")]
    private static partial void LogUnexpectedFailure(ILogger logger, Exception exception);

    // A stored record the core rejects is a failure of the system, not of the request. InvalidConfiguration stops the
    // host before any request; raised during one, it is an unexpected failure. A new member must be mapped here, so the
    // default is unreachable rather than a silent 500.
    private static (int Status, string Code) ProblemOf(DomainError error) => error switch
    {
        DomainError.InvalidEmail => (StatusCodes.Status400BadRequest, ProblemCodes.InvalidEmail),
        DomainError.InvalidLocale => (StatusCodes.Status400BadRequest, ProblemCodes.InvalidLocale),
        DomainError.InvalidOneTimeCode => (StatusCodes.Status400BadRequest, ProblemCodes.InvalidCodeFormat),
        DomainError.InvalidSessionToken => (StatusCodes.Status401Unauthorized, ProblemCodes.NotSignedIn),
        DomainError.CorruptRecord => InternalError,
        DomainError.InvalidConfiguration => InternalError,
        _ => throw new UnreachableException(),
    };
}
