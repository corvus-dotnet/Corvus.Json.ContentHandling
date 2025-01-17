using Corvus.Json.ContentHandling;
using ExampleExtensionHandler.Model;

namespace ExampleExtensionHandler;

public class ExtensionHandler : IHandler<ExtensionMedia, BaseResult>
{
    /// <summary>
    /// Gets the media type for the ExtensionHandler
    /// </summary>
    public ReadOnlySpan<byte> MediaType => "application/extension+json"u8;

    public bool TryHandle(in ExtensionMedia content, out BaseResult result)
    {
        int bufSize = content.Data.GetDecodedBufferSize();
        byte[] buffer = new byte[bufSize];
        if (content.Data.TryGetDecodedBase64Bytes(buffer, out int written))
        {
            result = BaseResult.Create(particleCount: written, handledBy: nameof(ExtensionHandler));
            return true;
        }

        result = default;
        return false;
    }
}
