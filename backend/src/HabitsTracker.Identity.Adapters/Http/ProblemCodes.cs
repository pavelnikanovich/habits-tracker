namespace HabitsTracker.Identity.Adapters.Http;

/// <summary>
/// Every <c>code</c> a problem of the API carries (contracts/http-api.md): the shared error table, then each endpoint's
/// own. The page keeps its own list (<c>knownCodes</c> in <c>frontend/src/app/api/problem.ts</c>), and a Host test
/// keeps the two equal.
/// </summary>
public static class ProblemCodes
{
    public const string InvalidRequest = "invalid-request";
    public const string NotFound = "not-found";
    public const string InternalError = "internal-error";
    public const string NotSignedIn = "not-signed-in";
    public const string InvalidEmail = "invalid-email";
    public const string InvalidLocale = "invalid-locale";
    public const string InvalidCodeFormat = "invalid-code-format";
    public const string EmailLimit = "email-limit";
    public const string IpLimit = "ip-limit";
    public const string EmailServiceLimit = "email-service-limit";
    public const string EmailServiceUnavailable = "email-service-unavailable";
    public const string CodeIncorrect = "code-incorrect";
    public const string NewCodeRequired = "new-code-required";
}
