using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using KnightFight.Api.Infrastructure.ApiMethods;
using KnightFight.Api.Infrastructure.EndpointHandlers;

namespace KnightFight.Api.Infrastructure.EndpointsMethods;

/// <summary>
/// Represents an abstract base class for a GET endpoint that streams Server-Sent Events.
/// The endpoint writes the response body itself, so the client receives frames as they
/// are produced instead of waiting for the whole response.
/// </summary>
/// <typeparam name="TEvent">The type of the streamed events.</typeparam>
/// <remarks>
/// Frames are written as <c>data: {json}</c> without an <c>event:</c> line, which is what
/// <c>EventSource.onmessage</c> consumes. Set <see cref="EventName"/> to emit named events instead.
/// </remarks>
public abstract class StreamEndpoint<TEvent>(string uriPattern) : ApiMethod(uriPattern), IStreamHandler
{
    /// <summary>
    /// Name of the SSE event. When null (default) the frames carry no event name.
    /// </summary>
    protected virtual string? EventName => null;

    /// <summary>
    /// Type information used to serialize every event. A source generated
    /// <c>JsonTypeInfo</c> from <c>AppJsonContext</c> is preferred: it keeps the
    /// camelCase casing the rest of the API uses and avoids reflection based serialization.
    /// </summary>
    protected abstract JsonTypeInfo<TEvent> EventTypeInfo { get; }

    /// <summary>
    /// Produces the events to stream. Implementations must honour the cancellation token.
    /// </summary>
    protected abstract IAsyncEnumerable<TEvent> ReadEvents(CancellationToken cancellationToken);

    public override void MapEndpoint(WebApplication app)
        => app.MapGet(UriPattern, async (HttpContext context) => await Stream(context, context.RequestAborted))
            .WithGroupName("StreamEndpoints");

    public async Task Stream(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        // stops nginx from buffering the response, which would defeat the streaming
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await context.Response.Body.FlushAsync(cancellationToken);

        try
        {
            await foreach (var evt in ReadEvents(cancellationToken))
            {
                var json = JsonSerializer.Serialize(evt, EventTypeInfo);
                var frame = EventName is null
                    ? $"data: {json}\n\n"
                    : $"event: {EventName}\ndata: {json}\n\n";

                await context.Response.WriteAsync(frame, cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // the client went away - nothing to report
        }
    }
}
