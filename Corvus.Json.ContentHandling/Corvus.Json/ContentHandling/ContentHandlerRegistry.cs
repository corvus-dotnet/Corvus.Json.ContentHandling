// <copyright file="ContentHandlerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Json.ContentHandling;

/// <summary>
/// A registry for JSON content handlers differentiated by media type.
/// </summary>
/// <remarks>
/// <para>
/// This supports the standard media type fallback mechanism where a more specific media type is tried first, and then
/// the last segment is removed, and a handler for that media type is tried, and so on until we reach the top level media type
/// or a successful handler is found.
/// </para>
/// <para>
/// For example, if you try <c>application/base.specialized+json</c> then a handler will be tried for <c>application/base.specialized+json</c>.
/// If that is not found, then it will fall back to a handler for <c>application/base+json</c>. If that is not found it will fail.
/// </para>
/// <para>Handlers can choose to implement the <see cref="IHandler{TContent, TResult}"/> interface, or be a simple delegate
/// of the form <see cref="ContentHandlerRegistry.Handler{TContent, TResult}"/>.</para>
/// <para>While you would typically want to use a singleton stateless handler, we support registering a handler factory instance through
/// the <see cref="ContentHandlerRegistry.Register{TContent, TResult}(Func{IHandler{TContent,TResult}})"/>. One use of this
/// mechanism might be to integrate with a DI container. For example:
/// <code>
/// [!<![CDATA[
/// IServiceCollection serviceCollection;
/// serviceCollection.AddSingleton<MyHandler>();
/// serviceCollection.AddSingleton<ContentHandlerRegistry>();
///
/// // once the service collection is built, we have our service provider
/// IServiceProvider serviceProvider;
///
/// // Then, register your handlers
/// ContentHandlerRegistry registry = serviceProvider.GetRequiredService<ContentHandlerRegistry>();
/// registry.Register(() => serviceProvider.GetRequiredService<MyHandler>());
/// ]]>
/// </code>
/// </para>
/// </remarks>
public sealed class ContentHandlerRegistry
{
    private readonly Dictionary<Utf8ByteKey, HandlerDelegate> handlers;
    private Dictionary<Utf8ByteKey, HandlerDelegate>.AlternateLookup<ReadOnlySpan<byte>> lookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentHandlerRegistry"/> class.
    /// </summary>
    public ContentHandlerRegistry()
    {
        this.handlers = new(Utf8ByteKey.Comparer.Instance);
        this.lookup = this.handlers.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    /// <summary>
    /// A handler delegate.
    /// </summary>
    /// <typeparam name="TContent">The type of the content to handle.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="content">The content to handle.</param>
    /// <param name="result">If the content was handled, then this is the result of handling.</param>
    /// <returns><see langword="true"/> if the content was handled, and the result was of the correct type.</returns>
    public delegate bool Handler<TContent, TResult>(in TContent content, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>;

    private delegate bool HandlerDelegate(in JsonAny content, out JsonAny result);

    /// <summary>
    /// Registers a handler for a given media type, using a factory to create a transient instance of the handler.
    /// </summary>
    /// <typeparam name="TContent">The type of the JSON content to handle.</typeparam>
    /// <typeparam name="TResult">The type of the JSON result of the handler.</typeparam>
    /// <param name="tryHandle">A delegate that tries to handle the instance.</param>
    /// <param name="mediaType">The media type for which to register the handler.</param>
    /// <returns><see langword="true"/> if the handler could be added. <see langword="false"/> if there was already
    /// a handler registered for the media type.</returns>
    public bool Register<TContent, TResult>(Handler<TContent, TResult> tryHandle, in ReadOnlySpan<byte> mediaType)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        return this.handlers.TryAdd(new(mediaType), HandlerWrapper);

        bool HandlerWrapper(in JsonAny content, out JsonAny result)
        {
            TContent convertedContent = content.As<TContent>();

            if (convertedContent.IsValid() && tryHandle(convertedContent, out TResult localResult))
            {
                result = localResult.AsAny;
                return true;
            }

            result = default;
            return false;
        }
    }

    /// <summary>
    /// Registers a handler for a given media type, using a factory to create a transient instance of the handler.
    /// </summary>
    /// <typeparam name="TContent">The type of the JSON content to handle.</typeparam>
    /// <typeparam name="TResult">The type of the JSON result of the handler.</typeparam>
    /// <param name="handler">The singleton instance of the handler for the media type.</param>
    /// <returns><see langword="true"/> if the handler could be added. <see langword="false"/> if there was already
    /// a handler registered for the media type.</returns>
    public bool Register<TContent, TResult>(IHandler<TContent, TResult> handler)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        return this.handlers.TryAdd(new(handler.MediaType), HandlerWrapper);

        bool HandlerWrapper(in JsonAny content, out JsonAny result)
        {
            TContent convertedContent = content.As<TContent>();
            if (convertedContent.IsValid() && handler.TryHandle(convertedContent, out TResult localResult))
            {
                result = localResult.AsAny;
                return true;
            }

            result = default;
            return false;
        }
    }

    /// <summary>
    /// Registers a handler for a given media type, using a factory to create a transient instance of the handler.
    /// </summary>
    /// <typeparam name="TContent">The type of the JSON content to handle.</typeparam>
    /// <typeparam name="TResult">The type of the JSON result of the handler.</typeparam>
    /// <param name="handlerFactory">A factory delegate to create an instance of the handler.</param>
    /// <returns><see langword="true"/> if the handler could be added. <see langword="false"/> if there was already
    /// a handler registered for the media type.</returns>
    public bool Register<TContent, TResult>(Func<IHandler<TContent, TResult>> handlerFactory)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        IHandler<TContent, TResult> warmedUp = handlerFactory();
        return this.handlers.TryAdd(new(warmedUp.MediaType), HandlerWrapper);

        bool HandlerWrapper(in JsonAny content, out JsonAny result)
        {
            TContent convertedContent = content.As<TContent>();
            if (convertedContent.IsValid() && handlerFactory().TryHandle(convertedContent, out TResult localResult))
            {
                result = localResult.AsAny;
                return true;
            }

            result = default;
            return false;
        }
    }

    /// <summary>
    /// Try to handle the given content and produce a result.
    /// </summary>
    /// <typeparam name="TContent">The type of the content to handle.</typeparam>
    /// <typeparam name="TResult">The result of handling the content.</typeparam>
    /// <param name="content">The content instance to handle.</param>
    /// <param name="mediaType">The media type of the content.</param>
    /// <param name="result">The result of processing the content.</param>
    /// <returns><see langword="true"/> if the content was handled, and the result was of the correct type.</returns>
    public bool TryHandle<TContent, TResult>(in TContent content, in JsonString mediaType, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        return mediaType.TryGetValue(TryHandleUtf8, content, out result);

        bool TryHandleUtf8(ReadOnlySpan<byte> span, in TContent content, out TResult value)
        {
            return this.TryHandle(content, span, out value);
        }
    }

    /// <summary>
    /// Try to handle the given content and produce a result.
    /// </summary>
    /// <typeparam name="TContent">The type of the content to handle.</typeparam>
    /// <typeparam name="TResult">The result of handling the content.</typeparam>
    /// <param name="content">The content instance to handle.</param>
    /// <param name="mediaType">The media type of the content.</param>
    /// <param name="result">The result of processing the content.</param>
    /// <returns><see langword="true"/> if the content was handled, and the result was valid.</returns>
    public bool TryHandle<TContent, TResult>(in TContent content, string mediaType, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        return this.TryHandle(content, mediaType.AsSpan(), out result);
    }

    /// <summary>
    /// Try to handle the given content and produce a result.
    /// </summary>
    /// <typeparam name="TContent">The type of the content to handle.</typeparam>
    /// <typeparam name="TResult">The result of handling the content.</typeparam>
    /// <param name="content">The content instance to handle.</param>
    /// <param name="mediaType">The media type of the content.</param>
    /// <param name="result">The result of processing the content.</param>
    /// <returns><see langword="true"/> if the content was handled, and the result was of the correct type.</returns>
    public bool TryHandle<TContent, TResult>(in TContent content, ReadOnlySpan<char> mediaType, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        Span<byte> mediaTypeBytes = stackalloc byte[Encoding.UTF8.GetMaxByteCount(mediaType.Length)];
        int written = Encoding.UTF8.GetBytes(mediaType, mediaTypeBytes);
        return this.TryHandleCore(content, mediaTypeBytes[..written], out result);
    }

    /// <summary>
    /// Try to handle the given content and produce a result.
    /// </summary>
    /// <typeparam name="TContent">The type of the content to handle.</typeparam>
    /// <typeparam name="TResult">The result of handling the content.</typeparam>
    /// <param name="content">The content instance to handle.</param>
    /// <param name="mediaType">The media type of the content.</param>
    /// <param name="result">The result of processing the content.</param>
    /// <returns><see langword="true"/> if the content was handled, and the result was of the correct type.</returns>
    public bool TryHandle<TContent, TResult>(in TContent content, ReadOnlySpan<byte> mediaType, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        return this.TryHandleCore(content, mediaType, out result);
    }

    private bool TryHandleCore<TContent, TResult>(in TContent content, ReadOnlySpan<byte> mediaType, out TResult result)
        where TContent : struct, IJsonValue<TContent>
        where TResult : struct, IJsonValue<TResult>
    {
        JsonAny contentAsAny = content.AsAny;

        // First, do a quick lookup directly
        if (this.lookup.TryGetValue(mediaType, out HandlerDelegate? handler) &&
            handler(contentAsAny, out JsonAny localResult))
        {
            result = localResult.As<TResult>();
            return result.IsValid();
        }

        // It wasn't available, so check to see if we have a + suffix
        int index = mediaType.LastIndexOf((byte)'+');
        ReadOnlySpan<byte> suffix;
        ReadOnlySpan<byte> remainingMedia;

        if (index >= 0)
        {
            // There was a suffix, so we need to allocate a temporary buffer to build the derived media type
            suffix = mediaType[index..];
            remainingMedia = mediaType[..index];
            Span<byte> target = stackalloc byte[remainingMedia.Length + suffix.Length];
            remainingMedia.CopyTo(target);

            while (remainingMedia.Length > 0)
            {
                int indexOfLastDot = remainingMedia.LastIndexOf((byte)'.');

                if (indexOfLastDot < 0)
                {
                    break;
                }

                suffix.CopyTo(target[indexOfLastDot..]);
                if (this.lookup.TryGetValue(target[..(indexOfLastDot + suffix.Length)], out HandlerDelegate? handler2) &&
                    handler2(contentAsAny, out JsonAny localResult2))
                {
                    result = localResult2.As<TResult>();
                    return result.IsValid();
                }

                remainingMedia = remainingMedia[..indexOfLastDot];
            }
        }
        else
        {
            remainingMedia = mediaType[..index];
            while (remainingMedia.Length > 0)
            {
                int indexOfLastDot = remainingMedia.LastIndexOf((byte)'.');

                if (indexOfLastDot < 0)
                {
                    break;
                }

                if (this.lookup.TryGetValue(remainingMedia[..indexOfLastDot], out HandlerDelegate? handler2) &&
                    handler2(contentAsAny, out JsonAny localResult2))
                {
                    result = localResult2.As<TResult>();
                    return result.IsValid();
                }

                remainingMedia = remainingMedia[..indexOfLastDot];
            }
        }

        result = default;
        return false;
    }
}