using System.Net;
using System.Text;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Search;

public sealed class SSESearchClientTests
{
    [Fact]
    public async Task StartSearch_ShouldInvokeIncrementalResults_OnSourceResult()
    {
        var payload = "event: start\r\ndata: {\"totalSources\":2}\r\n\r\nevent: sourceResult\r\ndata: {\"sourceName\":\"src1\",\"results\":[{\"id\":\"1\",\"title\":\"T\",\"source\":\"s\",\"source_name\":\"sn\"}]}\r\n\r\nevent: complete\r\ndata: {}\r\n\r\n";
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "text/event-stream"),
        });
        var client = new SSESearchClient(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });

        var batches = new List<int>();
        var complete = false;
        client.IncrementalResults += r => batches.Add(r.Count);
        client.Progress += p => complete = p.IsComplete;

        await client.StartSearchAsync("test", "https://example.com");

        Assert.NotEmpty(batches);
        Assert.True(complete);
    }

    [Fact]
    public async Task StartSearch_ShouldStopMidStream()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            // Return a single event with no blank-line flush — the reader will block on ReadLineAsync.
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("event: start\ndata: {}\n", Encoding.UTF8, "text/event-stream"),
            };
            return response;
        });
        var client = new SSESearchClient(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });

        var t = client.StartSearchAsync("test", "https://example.com");
        await Task.Delay(100);
        client.Stop();
        await t; // should not throw
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
