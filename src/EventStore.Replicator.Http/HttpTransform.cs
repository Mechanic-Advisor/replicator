using System.Net;
using System.Text;
using System.Text.Json;
using EventStore.Replicator.Shared.Contracts;

namespace EventStore.Replicator.Http; 

public class HttpTransform {
    readonly HttpClient _client;

    public HttpTransform(string? url) {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url), "HTTP Transform must have a valid URL");
            
        _client = new HttpClient {BaseAddress = new Uri(url)};
    }

    public async ValueTask<BaseProposedEvent> Transform(
        OriginalEvent originalEvent, CancellationToken cancellationToken
    ) {
        var httpEvent = new HttpEvent(
            originalEvent.EventDetails.EventType,
            originalEvent.EventDetails.Stream,
            Encoding.UTF8.GetString(originalEvent.Data)
        );

        try {
            var response = await _client.PostAsync(
                "",
                new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(httpEvent)),
                cancellationToken
            ).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Transformation request failed: {response.ReasonPhrase}");

            if (response.StatusCode == HttpStatusCode.NoContent)
                return new IgnoredEvent(
                    originalEvent.EventDetails,
                    originalEvent.Position,
                    originalEvent.SequenceNumber
                );

            HttpEvent httpResponse = (await JsonSerializer.DeserializeAsync<HttpEvent>(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken
            ).ConfigureAwait(false))!;

            return new ProposedEvent(
                originalEvent.EventDetails with {
                    EventType = httpResponse.EventType, Stream = httpResponse.StreamName
                },
                Encoding.UTF8.GetBytes(httpResponse.Payload),
                AddOriginalMetadata(originalEvent),
                originalEvent.Position,
                originalEvent.SequenceNumber
            );
        }
        catch (OperationCanceledException) {
            return new NoEvent(originalEvent.EventDetails, originalEvent.Position, originalEvent.SequenceNumber);
        }
    }

    record HttpEvent(string EventType, string StreamName, string Payload);

    static byte[] AddOriginalMetadata(OriginalEvent originalEvent) {
        if (originalEvent.Metadata == null || originalEvent.Metadata.Length == 0) {
            var eventMeta = new EventMetadata {
                OriginalEventNumber = originalEvent.Position.EventNumber,
                OriginalPosition    = originalEvent.Position.EventPosition,
                OriginalCreatedDate = originalEvent.Created
            };
            return JsonSerializer.SerializeToUtf8Bytes(eventMeta);
        }

        var originalMeta = JsonSerializer.Deserialize<Dictionary<string, object>>(originalEvent.Metadata);

        originalMeta ??= new Dictionary<string, object>();

        if (!originalMeta.ContainsKey(EventMetadata.EventNumberPropertyName))
            originalMeta.Add(EventMetadata.EventNumberPropertyName, originalEvent.Position.EventNumber);

        if (!originalMeta.ContainsKey(EventMetadata.PositionPropertyName))
            originalMeta.Add(EventMetadata.PositionPropertyName, originalEvent.Position.EventPosition);

        if (!originalMeta.ContainsKey(EventMetadata.CreatedDate))
            originalMeta.Add(EventMetadata.CreatedDate, originalEvent.Created);

        return JsonSerializer.SerializeToUtf8Bytes(originalMeta);
    }
}