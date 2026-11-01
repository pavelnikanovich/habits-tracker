namespace HabitsTracker.Identity.Adapters.Tests.CodeEmail;

// An HttpMessageHandler answering from a function, which may also throw; the requests it saw are kept.
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
        return answer(request);
    }
}
