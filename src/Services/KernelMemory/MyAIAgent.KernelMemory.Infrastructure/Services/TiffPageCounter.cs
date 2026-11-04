/**
 * MyAIAgent | KernelMemory · Infrastructure
 *
 * Counts TIFF frames before submitting a document to a limited OCR tier.
 */

using System.Buffers.Binary;

namespace MyAIAgent.KernelMemory.Infrastructure.Services;

internal static class TiffPageCounter
{
    internal static bool HasHeader(ReadOnlySpan<byte> data) => data.Length >= 4 &&
        ((data[0] == 'I' && data[1] == 'I' && data[2] == 42 && data[3] == 0) ||
         (data[0] == 'M' && data[1] == 'M' && data[2] == 0 && data[3] == 42));

    internal static int CountPages(ReadOnlySpan<byte> data)
    {
        if (!HasHeader(data) || data.Length < 8)
            throw new InvalidOperationException("Document TIFF page count could not be read.");

        var littleEndian = data[0] == 'I';
        uint Read32(ReadOnlySpan<byte> value) => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(value)
            : BinaryPrimitives.ReadUInt32BigEndian(value);
        ushort Read16(ReadOnlySpan<byte> value) => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(value)
            : BinaryPrimitives.ReadUInt16BigEndian(value);

        var offset = Read32(data.Slice(4, 4));
        var visited = new HashSet<uint>();
        var count = 0;
        while (offset != 0)
        {
            if (!visited.Add(offset) || offset > data.Length - 2)
                throw new InvalidOperationException("Document TIFF page count could not be read.");

            var entries = Read16(data.Slice((int)offset, 2));
            var nextPosition = (long)offset + 2 + (long)entries * 12;
            if (nextPosition + 4 > data.Length)
                throw new InvalidOperationException("Document TIFF page count could not be read.");

            count++;
            offset = Read32(data.Slice((int)nextPosition, 4));
        }

        if (count == 0)
            throw new InvalidOperationException("Document TIFF page count could not be read.");
        return count;
    }
}
