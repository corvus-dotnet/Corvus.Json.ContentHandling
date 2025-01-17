using Corvus.Json.ContentHandling;
using ExampleBaseHandler.Model;

namespace ExampleBaseHandler;

public class ExampleHandler : IHandler<BaseMedia, BaseResult>
{
    /// <summary>
    /// Gets the media type for the ExampleHandler
    /// </summary>
    public ReadOnlySpan<byte> MediaType => "application/example+json"u8;

    public bool TryHandle(in BaseMedia content, out BaseResult result)
    {
        if (TryProcessRefData(content.ReferencedData, out int count))
        {
            result = BaseResult.Create(particleCount: count, handledBy: nameof(ExampleHandler));
            return true;
        }

        result = default;
        return false;
    }

    private static bool TryProcessRefData(BaseMedia.ReferencedDataEntity referencedData, out int count)
    {
        if (referencedData.TryGetUri(out Uri? uri))
        {
            // Pretend to process the data at the URI
            count = uri.AbsoluteUri.Length;
            return true;
        }

        count = -1;
        return false;
    }
}
