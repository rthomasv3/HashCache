using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HashCache;

internal class IndexedHeader
{
    #region Constructor(s)

    public IndexedHeader(string fileLocation)
    {
        if (File.Exists(fileLocation))
        {
            using (FileStream indexStream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read, 
                FileShare.None, 4096, FileOptions.SequentialScan))
            {
                Deserialize(indexStream);
            }
        }
    }

    #endregion

    #region Properties

    public ushort Version { get; init; } = 1;
    public ConcurrentStack<long> FreeBlocks { get; init; } = new();
    public Dictionary<ulong, IndexedHeaderEntry> Entries { get; init; } = new();

    #endregion

    #region Public Methods

    public byte[] Serialize()
    {
        List<byte> serializedData = new();

        serializedData.AddRange(BitConverter.GetBytes(Version));
        serializedData.AddRange(BitConverter.GetBytes(FreeBlocks.Count));
        foreach (long freeBlockIndex in FreeBlocks)
        {
            serializedData.AddRange(BitConverter.GetBytes(freeBlockIndex));
        }
        serializedData.AddRange(BitConverter.GetBytes(Entries.Count));
        foreach (IndexedHeaderEntry entry in Entries.Values)
        {
            serializedData.AddRange(entry.Serialize());
        }

        return serializedData.ToArray();
    }

    #endregion

    #region Private Methods

    private void Deserialize(FileStream indexStream)
    {
        byte[] versionData = new byte[2];
        indexStream.ReadExactly(versionData, 0, versionData.Length);
        ushort version = BitConverter.ToUInt16(versionData);
        if (version == Version)
        {
            byte[] longBuffer = new byte[8];
            byte[] expBuffer = new byte[20];
            byte[] intBuffer = new byte[4];
            byte[] shortBuffer = new byte[2];

            indexStream.ReadExactly(intBuffer, 0, intBuffer.Length);
            int freeBlockCount = BitConverter.ToUInt16(intBuffer);
            for (int i = 0; i < freeBlockCount; ++i)
            {
                indexStream.ReadExactly(longBuffer, 0, longBuffer.Length);
                FreeBlocks.Push(BitConverter.ToInt64(longBuffer));
            }

            indexStream.ReadExactly(intBuffer, 0, intBuffer.Length);
            int entriesCount = BitConverter.ToUInt16(intBuffer);
            for (int i = 0; i < entriesCount; ++i)
            {
                indexStream.ReadExactly(longBuffer, 0, longBuffer.Length);
                ulong key = BitConverter.ToUInt64(longBuffer);

                indexStream.ReadExactly(expBuffer, 0, expBuffer.Length);
                DateTime exp = DateTime.ParseExact(Encoding.UTF8.GetString(expBuffer), "u", null);

                indexStream.ReadExactly(intBuffer, 0, intBuffer.Length);
                int indexCount = BitConverter.ToInt32(intBuffer);
                List<long> indices = new();
                for (int j = 0; j < indexCount; ++j)
                {
                    indexStream.ReadExactly(longBuffer, 0, longBuffer.Length);
                    indices.Add(BitConverter.ToInt64(longBuffer));
                }

                indexStream.ReadExactly(shortBuffer, 0, shortBuffer.Length);
                ushort checksum = BitConverter.ToUInt16(shortBuffer);

                Entries[key] = new IndexedHeaderEntry()
                {
                    Key = key,
                    Expiration = exp,
                    DataIndices = indices,
                    Checksum = checksum
                };
            }
        }
    }

    #endregion
}
