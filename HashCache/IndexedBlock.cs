using System;
using System.Collections.Generic;

namespace HashCache;

internal class IndexedBlock
{
    #region Constructor(s)

    public IndexedBlock() { }

    public IndexedBlock(byte[] fullBlockData, int dataBlockSize)
    {
        if (fullBlockData != null && fullBlockData.Length > 1)
        {
            byte[] dataSizeBytes = new byte[2];
            Array.Copy(fullBlockData, 0, dataSizeBytes, 0, 2);
            DataSize = BitConverter.ToUInt16(dataSizeBytes);

            if (fullBlockData.Length > 2)
            {
                Data = new byte[DataSize];
                Array.Copy(fullBlockData, 2, Data, 0, DataSize);
            }
        }
    }

    #endregion

    #region Properties

    public ushort DataSize { get; init; } = 0;
    public byte[] Data { get; init; } = null;

    #endregion

    #region Public Methods

    public byte[] Serialize(int dataBlockSize)
    {
        List<byte> serializedData = new();

        serializedData.AddRange(BitConverter.GetBytes(DataSize));

        if (Data == null)
        {
            serializedData.AddRange(new byte[dataBlockSize]);
        }
        else if (DataSize < dataBlockSize)
        {
            byte[] paddedData = new byte[dataBlockSize];
            Array.Copy(Data, 0, paddedData, 0, Data.Length);
            serializedData.AddRange(paddedData);
        }
        else
        {
            serializedData.AddRange(Data);
        }

        return serializedData.ToArray();
    }

    #endregion
}
