# Corvus.Json.ContentHandling

Media-type based content handling for JSON-encoded payloads in .NET.

## What does it do?

We often deal with code that receives a JSON payload of some kind, and dispatches it to a handler to process, based on some characteristic of that payload.

One common pattern is to identify the payload using a media type (such as `application/vnd.endjin.some-content+json`).

`Corvus.ContentHandling` allows you to register handlers for JSON payloads described by JSON Schema, and identified by such a media type.

It also uses `Corvus.JsonSchema` to generate the POCO .NET types that represent the JSON Schema payloads, making deserialization and type conversions extremely high performance, and low allocation.

## Why might you take this approach?

This is a particularly useful approach for several common scenarios:

1. HTTP endpoints where the request body is versioned, and identified by a `content-type` header.

   In v1 of your application, you can register a handler for `application/vnd.contoso.some-body-type+json`, expecting an instance of your `some-body-type.json` schema.

   When the API is versioned, you can register a second handler for `application/vnd.contoso.some-body-type.v2+json`, expecting an instance of your `some-body-type-v2.json` schema.

   Your overall request handler dispatches the payload to the correct handler based on its `content-type`, using the `ContentHandlerRegistry`. It is decoupled from the logic both to dispatch to the correct handler, and to validate the payload against the required schema.

1. Message dispatch from a queue

   In a heterogenous message queue, you can register discreet handlers for `application/vnd.contoso.message1+json`, expecting an instance of your `message1.json` schema and `application/vnd.contoso.message2+json`, expecting an instance of your `message2.json` schema, and so forth.

   Your dequeuing and dispatch logic doesn't need any explicit knowledge of the message types that can be handled.

In general, any case where it is useful to be able to register handlers for strongly-typed .NET representations of JSON payloads, and decouple that from the actual dispatch logic, is a good candidate for `Corvus.Json.ContentHandling` - especially if you have a discovery/plugin mechanism for your handler registration.

## What is a handler?

A handler can be as simple as a delegate to a method that takes an input JSON instance, and returns a JSON result.

```csharp
public delegate bool Handler<TContent, TResult>(in TContent content, out TResult result)
```

Here's an example of that.

```csharp
var myDelegate = static (in ExtensionMedia content, out BaseResult result) =>
{
    // You would rent a buffer in production code if you were
    // implementing this kind of handler
    // (Notice that Corvus.JsonSchema generates the Base64 content handling
    // code for you!)
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
```

This is a handler for content of a type called `ExtensionMedia`, producing a result which is an instance of a type called `BaseResult`. These types have been generated from the JSON schema for those instances, and represent the input and response JSON payloads.

Here's the `ExtensionMedia.json` schema:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "Extension media schema",
    "type": "object",
    "required": [
        "id",
        "data"
    ],
    "properties": {
        "id": {
            "type": "string",
            "description": "The unique identifier for the media object"
        },
        "data": {
            "type": "string",
            "contentEncoding": "base64",
            "description": "The base64-encoded byte data for the media object."
        }
    }
}
```

And here's how we get `Corvus.Json.SourceGenerator` to emit the type for that.

```csharp
using Corvus.Json;

namespace ExampleExtensionHandler.Model;

[JsonSchemaTypeGenerator("./ExtensionMedia.json")]
public readonly partial struct ExtensionMedia;
```

(`BaseResult` is defined and code generated in a similar fashion.)

But if you don't want to use a functional approach, you can create a handler class instead, and implement the `IHander<TContent, TResult>` interface.

Here's an example of that - again it takes an instance of a type called `ExtensionMedia` and produces a result of a type called `BaseResult`.

```csharp
public class ExtensionHandler : IHandler<ExtensionMedia, BaseResult>
{
    /// <summary>
    /// Gets the media type for the ExtensionHandler
    /// </summary>
    public static ReadOnlySpan<byte> MediaType => "application/extension+json"u8;

    public bool TryHandle(in ExtensionMedia content, out BaseResult result)
    {
        // You would rent a buffer in production code if you were
        // implementing this kind of handler
        // (Notice that Corvus.JsonSchema generates the Base64 content handling
        // code for you!)
        int bufSize = content.Data.GetDecodedBufferSize();
        byte[] buffer = new byte[bufSize];
        if (content.Data.TryGetDecodedBase64Bytes(buffer, out int written))
        {
            result = BaseResult.Create(
              particleCount: written,
              handledBy: nameof(ExtensionHandler));
            return true;
        }

        result = default;
        return false;
    }
}
```

Notice that our `TryHandle()` implementation is strongly typed. The framework uses `Corvus.JsonSchema`'s view-over-JSON-data approach to convert from the raw JSON to the strongly typed .NET instance, without any additional serialization or conversion overhead.

We then return `true` if we succesfully handle the payload, otherwise `false`.

## Registering a handler

It is simple to register a handler on an instance of the `ContentHandlerRegistry`.

```csharp
ContentHandlerRegistry registry = new();

registry.Register(new ExtensionHandler());
```

Notice that we declare a constant on our handler for the media type. The registry uses that to apply the handler to the correct media type.

```csharp
public ReadOnlySpan<byte> MediaType => "extension+json"u8;
```

If you use the delegate form of handler, you will have to pass the media type explicitly as a UTF8 `ReadOnlySpan<byte>`. The easiest way to do this is with the UTF8 literal string syntax we also use above: `"application/extension+json"u8`.

Considering the delegate we created in the example earlier, here's how we register it.

```csharp
registry.Register(myDelegate, "application/extension+json"u8);
```

### Using factories to integrate with a DI container

You can also register a factory for your handler. This is one way to integrate with a DI framework.

Here's an example that uses a singleton handler instance registered with a DI container.

```csharp
IServiceCollection serviceCollection;
serviceCollection.AddSingleton<MyHandler>();
serviceCollection.AddSingleton<ContentHandlerRegistry>();

// once the service collection is built, we have our service provider
IServiceProvider serviceProvider;

// Then, register your handlers
ContentHandlerRegistry registry = serviceProvider.GetRequiredService<ContentHandlerRegistry>();
registry.Register(() => serviceProvider.GetRequiredService<MyHandler>());
```

### How many registries do I need?

Notice that the example above is using a singleton `ContentHandlerRegistry` and adding that to the container. Our previous example just chooses to `new` up a registry.

You should consider whether a "single registry for all handlers" is right for your application.

This "one per process" approach can be good if your process is a bounded domain.

However, if you have different *types* of handler dealing with the same content in different domains (e.g. HTTP request handling, Logging, Message Queue handling), then you should consider using different registry instances for the different families of handlers.

You'll probably notice the need for this the first time you want to register a second handler for the same content type, and the registry throws an exception!

One way of doing this is to register different named instances of the `ContentHandlerRegistry` for the different domains. With the Microsoft DI container, you could use `AddKeyedSingleton()` for this purpose.

Outside of DI, you can just `new` up and manage a registry instance for each separate domain.

## Fallback handling

The library looks up a handler registered for the media type, and validates that the instance
conforms to the schema the handler expects. If so, it passes the entity to the handler for processing.

If a handler is found, and returns `true` to indicate that it has succesfully processed the instance, then the request is considered complete.

If no hander is found, or it says that it does not process the instance (by returning `false`) then the media type fallback mechanism comes into play.

The last segment of the media type is removed, and the handler for that new media type is tried, and so on until we reach the top level media type (or a successful handler is found).

For example, if you start with the media type `application/base.specialized+json` then a handler will be tried for `application/base.specialized+json`.

If no successful handler is found, then the library will fall back to a handler for `application/base+json`. If that is not found it will inspect the media type and determine that there are no further fallbacks possible, and the handling will not be successful.

### Why use fallback handling?

Fallback handling is a great way of supporting a hierarchy of handlers, usually in different parts of a system.

For example, imagine a family of APIs that offered various HTML renderers for a JSON payload.

You could define a base handler for `application/vnd.endjin.ui+json` that simply rendered the JSON as text.

And in your observability code, you could register a handler in its registry for `application/vnd.endjin.ui+json` that just logged the fact that a UI message was received at some particular time, for example.

> Notice that you can have multiple content handler registries in play - typically you will have one for each subsystem or "family" of handlers.

You could later define a content type and schema for `application/vnd.endjin.ui.circle+json` that defined a circle. e.g.

```json
{
  "type": "object",
  "properties": {
    "centerX": {"type": "number", "format": "double" },
    "centerY": {"type": "number", "format": "double" },
    "radius": {"type": "number", "format": "double" },
  }
}
```
In your rendering code you could register a specialised handler for `application/vnd.endjin.ui.circle+json` that transformed the JSON into an SVG fragment.

But you would *not* need to register a handler for the new content type in your observability code. The existing code would succesfully fall back from `application/vnd.endjin.ui.circle+json` to `application/vnd.endjin.ui+json` and dispatch to the default handler.

This is a robust way of extending a system piecemeal, without requiring everyone to update at the same time (or, indeed, at all) when new capabilities are added in one subsystem.

