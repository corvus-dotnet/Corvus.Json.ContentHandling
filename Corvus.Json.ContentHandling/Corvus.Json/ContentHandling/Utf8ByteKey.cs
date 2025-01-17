// <copyright file="Utf8ByteKey.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Corvus.Json.ContentHandling;

/// <summary>
/// A UTF8 byte key for a dictionary.
/// </summary>
/// <param name="value">The read-only bytes from which to construct the key.</param>
internal readonly struct Utf8ByteKey(ReadOnlyMemory<byte> value)
      : IEquatable<Utf8ByteKey>,
        IEquatable<ReadOnlySpan<byte>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8ByteKey"/> struct.
    /// </summary>
    /// <param name="readOnlySpan">The read-only bytes from which to construct the key.</param>
    public Utf8ByteKey(ReadOnlySpan<byte> readOnlySpan)
        : this(readOnlySpan.ToArray().AsMemory())
    {
    }

    /// <summary>
    /// Gets the value of the UTF8 byte key.
    /// </summary>
    public ReadOnlySpan<byte> Value => value.Span;

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public static bool operator ==(Utf8ByteKey left, Utf8ByteKey right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left hand side of the comparison.</param>
    /// <param name="right">The right hand side of the comparison.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    public static bool operator !=(Utf8ByteKey left, Utf8ByteKey right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Gets the HashCode for the given UTF8 bytes.
    /// </summary>
    /// <param name="obj">The bytes for which to get the hash code.</param>
    /// <returns>The hash code for the value.</returns>
    public static int GetHashCode([DisallowNull] ReadOnlySpan<byte> obj)
    {
        return GetHashCodeCore(obj);
    }

    /// <inheritdoc/>
    public bool Equals(ReadOnlySpan<byte> other)
    {
        return other.SequenceEqual(this.Value);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return GetHashCode(this.Value);
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is Utf8ByteKey other)
        {
            return other.Value.SequenceEqual(this.Value);
        }

        return base.Equals(obj);
    }

    /// <inheritdoc/>
    public bool Equals(Utf8ByteKey other)
    {
        return other.Value.SequenceEqual(this.Value);
    }

    private static int GetHashCodeCore([DisallowNull] ReadOnlySpan<byte> obj)
    {
        int length = obj.Length;

        if (length > 4)
        {
            int hash = length;

            Vector<int> vectorHash = new(hash);
            int vectorSize = Vector<byte>.Count;

            int i;
            for (i = 0; i <= length - vectorSize; i += vectorSize)
            {
                Vector<byte> vector = new(obj[i..vectorSize]);
                vectorHash ^= Vector.AsVectorInt32(vector);
            }

            for (; i < length; i++)
            {
                hash ^= obj[i];
            }

            for (int j = 0; j < Vector<int>.Count; j++)
            {
                hash ^= vectorHash[j];
            }

            return hash;
        }

        if (length == 4)
        {
            return BitConverter.ToInt32(obj);
        }

        // we have fewer than 4 characters. We compound these
        // with the length to get a better key distribution.
        foreach (byte b in obj)
        {
            length <<= 8;
            length += b;
        }

        return length;
    }

    /// <summary>
    /// A comparer for the <see cref="Utf8ByteKey"/>.
    /// </summary>
    public class Comparer : IEqualityComparer<Utf8ByteKey>, IAlternateEqualityComparer<ReadOnlySpan<byte>, Utf8ByteKey>
    {
        /// <summary>
        /// Gets the static instance of the comparer.
        /// </summary>
        public static Comparer Instance { get; } = new();

        /// <inheritdoc/>
        public Utf8ByteKey Create(ReadOnlySpan<byte> alternate)
        {
            return new(alternate);
        }

        /// <inheritdoc/>
        public bool Equals(Utf8ByteKey x, Utf8ByteKey y)
        {
            return x.Equals(y);
        }

        /// <inheritdoc/>
        public bool Equals(ReadOnlySpan<byte> alternate, Utf8ByteKey other)
        {
            return alternate.SequenceEqual(other.Value);
        }

        /// <inheritdoc/>
        public int GetHashCode(Utf8ByteKey obj)
        {
            return obj.GetHashCode();
        }

        /// <inheritdoc/>
        public int GetHashCode(ReadOnlySpan<byte> alternate)
        {
            return Utf8ByteKey.GetHashCode(alternate);
        }
    }
}