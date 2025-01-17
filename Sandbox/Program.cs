using Corvus.Json;
using Corvus.Json.ContentHandling;
using ExampleBaseHandler;
using ExampleExtensionHandler;
using Sandbox.Model;

internal class Program
{
    private static void Main(string[] args)
    {
        // This is the setup code.
        // Do you want a global registry?
        // Or do you need one per endpoint?
        // Or family of endpoints?
        ContentHandlerRegistry registry = new();
        DiscoverHandlers(registry);

        MakeImaginaryRequests(registry);
    }

    private static ParsedValue<JsonAny> MakeImaginaryRequests(ContentHandlerRegistry registry)
    {
        // Note that our endpoint is not coupled to the code in the handlers
        string requestContentType;
        ParsedValue<JsonAny> requestParsedValue;

        CreateFakeRequest(out requestContentType, out requestParsedValue);

        try
        {
            EndpointHandler(registry, requestContentType, requestParsedValue);
        }
        finally
        {
            requestParsedValue.Dispose();
        }

        CreateSecondFakeRequest(out requestContentType, out requestParsedValue);

        try
        {
            EndpointHandler(registry, requestContentType, requestParsedValue);
        }
        finally
        {
            requestParsedValue.Dispose();
        }

        CreateThirdFakeRequest(out requestContentType, out requestParsedValue);

        try
        {
            EndpointHandler(registry, requestContentType, requestParsedValue);
        }
        finally
        {
            requestParsedValue.Dispose();
        }

        return requestParsedValue;
    }

    private static void EndpointHandler(ContentHandlerRegistry registry, string requestContentType, ParsedValue<JsonAny> requestParsedValue)
    {
        // Notice how our endpoint is not coupled directly to the handlers for the media types we can
        // receive here.
        // We will use our own instance of the BaseResult schema so there is zero coupling to the handlers themselves.
        if (registry.TryHandle(requestParsedValue.Instance, requestContentType, out BaseResult result))
        {
            Console.WriteLine(result);
        }
        else
        {
            // Deal with our bad data / poison message handling
            Console.WriteLine($"Unable to process: {requestContentType}");
            Console.WriteLine(requestParsedValue.Instance);
        }
    }

    private static void CreateFakeRequest(out string requestContentType, out ParsedValue<JsonAny> requestParsedValue)
    {
        // Here we are sort-of pretending this has come via some kind of HTTP request.
        // But maybe we dequeued this payload off some kind of message bus instead.
        requestContentType = "application/extension+json";
        string body =
            """
            {
                "id": "SomeId",
                "data": "VGhpcyBpcyBqdXN0IHNvbWUgdGVzdCBkYXRhLiBJdCdzIG5pY2UgdGhhdCB5b3UgZGVjb2RlZCBpdCwgdGhvdWdoLg=="
            }            
            """;

        // We are just parsing to JsonAny - we will defer the explicit conversions to the handlers themselves
        // using our pay-to-play pattern.
        requestParsedValue = ParsedValue<JsonAny>.Parse(body);
    }

    private static void CreateSecondFakeRequest(out string requestContentType, out ParsedValue<JsonAny> requestParsedValue)
    {
        // Here we are sort-of pretending this has come via some kind of HTTP request.
        // But maybe we dequeued this payload off some kind of message bus instead.
        requestContentType = "application/example+json";
        string body =
            """
            {
                "id": "SomeId",
                "referencedData": "https://endjin.com/some/fake/iri"
            }            
            """;

        // We are just parsing to JsonAny - we will defer the explicit conversions to the handlers themselves
        // using our pay-to-play pattern.
        requestParsedValue = ParsedValue<JsonAny>.Parse(body);
    }

    private static void CreateThirdFakeRequest(out string requestContentType, out ParsedValue<JsonAny> requestParsedValue)
    {
        // Here we are sort-of pretending this has come via some kind of HTTP request.
        // But maybe we dequeued this payload off some kind of message bus instead.
        requestContentType = "application/some-other-media-type+json";
        string body =
            """
            {
                "unexpected": "data in the bagging area"
            }            
            """;

        // We are just parsing to JsonAny - we will defer the explicit conversions to the handlers themselves
        // using our pay-to-play pattern.
        requestParsedValue = ParsedValue<JsonAny>.Parse(body);
    }

    private static void DiscoverHandlers(ContentHandlerRegistry registry)
    {
        // Maybe you register the handlers using plugin code of some kind
        registry.Register(new ExampleHandler());
        registry.Register(new ExtensionHandler());
    }
}