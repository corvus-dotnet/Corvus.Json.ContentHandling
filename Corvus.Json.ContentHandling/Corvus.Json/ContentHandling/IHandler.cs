// <copyright file="IHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.ContentHandling;

/// <summary>
/// The interface implemented by a handler for JSON content.
/// </summary>
/// <typeparam name="TContent">The type of the content.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface IHandler<TContent, TResult>
    where TContent : struct, IJsonValue<TContent>
    where TResult : struct, IJsonValue<TResult>
{
    /// <summary>
    /// Gets the media type for the handler.
    /// </summary>
    ReadOnlySpan<byte> MediaType { get; }

    /// <summary>
    /// Handle the content.
    /// </summary>
    /// <param name="content">The content to handle.</param>
    /// <param name="result">The result of handling the content.</param>
    /// <returns><see langword="true"/> if the content was handled.</returns>
    bool TryHandle(in TContent content, out TResult result);
}