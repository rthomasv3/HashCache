using System;
using System.Collections.Generic;
using System.Text;

namespace HashCache;

internal class IndexedHeaderEntry
{
    #region Properties

    public ulong Key { get; init; } = 0;
    public DateTime Expiration { get; init; } = DateTime.MaxValue;
    public List<long> DataIndices { get; init; } = new();
    public ushort Checksum { get; init; } = 0;

    #endregion

    #region Public Methods

    public byte[] Serialize()
    {
        List<byte> serializedData = new();

        serializedData.AddRange(BitConverter.GetBytes(Key));
        serializedData.AddRange(Encoding.UTF8.GetBytes(Expiration.ToString("u")));
        serializedData.AddRange(BitConverter.GetBytes(DataIndices.Count));
        foreach (long dataIndex in DataIndices)
        {
            serializedData.AddRange(BitConverter.GetBytes(dataIndex));
        }
        serializedData.AddRange(BitConverter.GetBytes(Checksum));

        return serializedData.ToArray();
    }

    #endregion
}
