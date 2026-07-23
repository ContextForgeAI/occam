using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace OccamMcp.Core.Text;

/// <summary>Zero-allocation SIMD helpers for Tier-A HTML stream scanning (tag/whitespace).</summary>
public static class VectorizedHtmlScanner
{
    private const char LessThan = '<';
    private const char GreaterThan = '>';
    private const char Space = ' ';
    private const char Tab = '\t';
    private const char Cr = '\r';
    private const char Lf = '\n';

    private const ushort LessThanU = (ushort)LessThan;
    private const ushort GreaterThanU = (ushort)GreaterThan;
    private const ushort SpaceU = (ushort)Space;
    private const ushort TabU = (ushort)Tab;
    private const ushort CrU = (ushort)Cr;
    private const ushort LfU = (ushort)Lf;

    private const int Vector128CharWidth = 8;
    private const int Vector256CharWidth = 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfAnyTag(ReadOnlySpan<char> window)
    {
        if (window.IsEmpty)
        {
            return -1;
        }

        if (Vector256.IsHardwareAccelerated && window.Length >= Vector256CharWidth)
        {
            return IndexOfAnyTagVector256(window);
        }

        if (Vector128.IsHardwareAccelerated && window.Length >= Vector128CharWidth)
        {
            return IndexOfAnyTagVector128(window);
        }

        return IndexOfAnyTagScalar(window);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SkipWhitespaceVectorized(ReadOnlySpan<char> window)
    {
        if (window.IsEmpty)
        {
            return 0;
        }

        var offset = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            offset = SkipWhitespaceVector256(window);
            if (offset >= window.Length)
            {
                return window.Length;
            }

            window = window[offset..];
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            offset = SkipWhitespaceVector128(window);
            if (offset >= window.Length)
            {
                return window.Length;
            }

            window = window[offset..];
        }

        return offset + SkipWhitespaceScalar(window);
    }

    private static int IndexOfAnyTagVector256(ReadOnlySpan<char> window)
    {
        var uWindow = MemoryMarshal.Cast<char, ushort>(window);
        ref var uRef = ref MemoryMarshal.GetReference(uWindow);
        var lt256 = Vector256.Create(LessThanU);
        var gt256 = Vector256.Create(GreaterThanU);
        var width = Vector256CharWidth;
        var i = 0;

        if (Avx2.IsSupported)
        {
            while (i <= uWindow.Length - width)
            {
                var chunk16 = Vector256.LoadUnsafe(ref Unsafe.Add(ref uRef, i));
                var mask = Avx2.Or(
                    Avx2.CompareEqual(chunk16, lt256),
                    Avx2.CompareEqual(chunk16, gt256));
                if (!mask.Equals(Vector256<ushort>.Zero))
                {
                    var lane = FirstMatchLane(mask);
                    if (lane >= 0)
                    {
                        return i + lane;
                    }
                }

                i += width;
            }
        }
        else
        {
            while (i <= uWindow.Length - width)
            {
                var chunk16 = Vector256.LoadUnsafe(ref Unsafe.Add(ref uRef, i));
                var mask = Vector256.Equals(chunk16, lt256) | Vector256.Equals(chunk16, gt256);
                var lane = FirstMatchLane(mask);
                if (lane >= 0)
                {
                    return i + lane;
                }

                i += width;
            }
        }

        var tail = window[i..];
        var tailHit = IndexOfAnyTagScalar(tail);
        return tailHit < 0 ? -1 : i + tailHit;
    }

    private static int IndexOfAnyTagVector128(ReadOnlySpan<char> window)
    {
        var uWindow = MemoryMarshal.Cast<char, ushort>(window);
        ref var uRef = ref MemoryMarshal.GetReference(uWindow);
        var lt128 = Vector128.Create(LessThanU);
        var gt128 = Vector128.Create(GreaterThanU);
        var width = Vector128CharWidth;
        var i = 0;

        if (AdvSimd.IsSupported)
        {
            while (i <= uWindow.Length - width)
            {
                var chunk16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref uRef, i));
                var mask = AdvSimd.Or(
                    AdvSimd.CompareEqual(chunk16, lt128),
                    AdvSimd.CompareEqual(chunk16, gt128));
                if (!mask.Equals(Vector128<ushort>.Zero))
                {
                    var lane = FirstMatchLane(mask);
                    if (lane >= 0)
                    {
                        return i + lane;
                    }
                }

                i += width;
            }
        }
        else if (Sse2.IsSupported)
        {
            while (i <= uWindow.Length - width)
            {
                var chunk16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref uRef, i));
                var mask = Sse2.Or(
                    Sse2.CompareEqual(chunk16, lt128),
                    Sse2.CompareEqual(chunk16, gt128));
                if (!mask.Equals(Vector128<ushort>.Zero))
                {
                    var lane = FirstMatchLane(mask);
                    if (lane >= 0)
                    {
                        return i + lane;
                    }
                }

                i += width;
            }
        }
        else
        {
            while (i <= uWindow.Length - width)
            {
                var chunk16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref uRef, i));
                var mask = Vector128.Equals(chunk16, lt128) | Vector128.Equals(chunk16, gt128);
                var lane = FirstMatchLane(mask);
                if (lane >= 0)
                {
                    return i + lane;
                }

                i += width;
            }
        }

        var tail = window[i..];
        var tailHit = IndexOfAnyTagScalar(tail);
        return tailHit < 0 ? -1 : i + tailHit;
    }

    private static int SkipWhitespaceVector256(ReadOnlySpan<char> window)
    {
        var uWindow = MemoryMarshal.Cast<char, ushort>(window);
        ref var uRef = ref MemoryMarshal.GetReference(uWindow);
        var width = Vector256CharWidth;
        var offset = 0;
        while (offset <= uWindow.Length - width)
        {
            var chunk16 = Vector256.LoadUnsafe(ref Unsafe.Add(ref uRef, offset));
            if (!IsWhitespaceVector256(chunk16))
            {
                return offset + FirstNonWhitespaceLane(chunk16);
            }

            offset += width;
        }

        return offset;
    }

    private static int SkipWhitespaceVector128(ReadOnlySpan<char> window)
    {
        var uWindow = MemoryMarshal.Cast<char, ushort>(window);
        ref var uRef = ref MemoryMarshal.GetReference(uWindow);
        var width = Vector128CharWidth;
        var offset = 0;
        while (offset <= uWindow.Length - width)
        {
            var chunk16 = Vector128.LoadUnsafe(ref Unsafe.Add(ref uRef, offset));
            if (!IsWhitespaceVector128(chunk16))
            {
                return offset + FirstNonWhitespaceLane(chunk16);
            }

            offset += width;
        }

        return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespaceVector256(Vector256<ushort> chunk)
    {
        var mask = WhitespaceMask256(chunk);
        for (var j = 0; j < Vector256CharWidth; j++)
        {
            if (mask[j] == 0)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespaceVector128(Vector128<ushort> chunk)
    {
        var mask = WhitespaceMask128(chunk);
        for (var j = 0; j < Vector128CharWidth; j++)
        {
            if (mask[j] == 0)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstNonWhitespaceLane(Vector128<ushort> chunk)
    {
        var mask = WhitespaceMask128(chunk);
        for (var j = 0; j < Vector128CharWidth; j++)
        {
            if (mask[j] == 0)
            {
                return j;
            }
        }

        return Vector128CharWidth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstNonWhitespaceLane(Vector256<ushort> chunk)
    {
        var mask = WhitespaceMask256(chunk);
        for (var j = 0; j < Vector256CharWidth; j++)
        {
            if (mask[j] == 0)
            {
                return j;
            }
        }

        return Vector256CharWidth;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> WhitespaceMask128(Vector128<ushort> chunk)
    {
        return Vector128.Equals(chunk, Vector128.Create(SpaceU))
            | Vector128.Equals(chunk, Vector128.Create(TabU))
            | Vector128.Equals(chunk, Vector128.Create(CrU))
            | Vector128.Equals(chunk, Vector128.Create(LfU));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> WhitespaceMask256(Vector256<ushort> chunk)
    {
        return Vector256.Equals(chunk, Vector256.Create(SpaceU))
            | Vector256.Equals(chunk, Vector256.Create(TabU))
            | Vector256.Equals(chunk, Vector256.Create(CrU))
            | Vector256.Equals(chunk, Vector256.Create(LfU));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstMatchLane(Vector256<ushort> mask)
    {
        for (var j = 0; j < Vector256CharWidth; j++)
        {
            if (mask[j] != 0)
            {
                return j;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstMatchLane(Vector128<ushort> mask)
    {
        for (var j = 0; j < Vector128CharWidth; j++)
        {
            if (mask[j] != 0)
            {
                return j;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfAnyTagScalar(ReadOnlySpan<char> window)
    {
        for (var i = 0; i < window.Length; i++)
        {
            var c = window[i];
            if (c is LessThan or GreaterThan)
            {
                return i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SkipWhitespaceScalar(ReadOnlySpan<char> window)
    {
        var i = 0;
        while (i < window.Length && IsWhitespaceScalar(window[i]))
        {
            i++;
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhitespaceScalar(char c) =>
        c is Space or Tab or Cr or Lf;
}
