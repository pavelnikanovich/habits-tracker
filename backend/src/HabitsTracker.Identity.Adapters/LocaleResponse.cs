namespace HabitsTracker.Identity.Adapters;

/// <summary>An available locale as <c>GET /api/locales</c> answers it: the code and the language's own name.</summary>
/// <remarks>
/// It sits outside the adapter folders because two adapters share it: the translation files produce it and the
/// HTTP adapter answers with it; neither names the other (adapters meet only in the core and in the registration).
/// </remarks>
public sealed record LocaleResponse(string Code, string Name);
