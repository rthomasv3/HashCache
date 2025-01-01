using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Timers;

namespace HashCache;

public class IndexCacheService : IDisposable
{
    #region Fields

    private static readonly string _indexFileName = "index.dat";
    private static readonly string _cacheFileName = "cache.dat";

    private static readonly int _blockCountSize = 2;
    private static readonly int _blockDataSize = 4094;
    private static readonly int _totalBlockSize = _blockCountSize + _blockDataSize;

    private static readonly ulong _FNVOffsetBasis = 0xcbf29ce484222325; // 14695981039346656037
    private static readonly ulong _FNVPrime = 0x100000001b3; // 1099511628211

    private static readonly TimeSpan _defaultExp = TimeSpan.FromMinutes(5);

    private readonly System.Timers.Timer _indexFlushTimer;
    private readonly SemaphoreSlim _cacheWriteSemaphore;
    private readonly SemaphoreSlim _indexWriteSemaphore;

    private IndexedHeader _index;
    private FileStream _cacheStream;

    private volatile bool _indexUpdated = false;

    #endregion

    #region Constructor(s)

    public IndexCacheService()
    {
        InitCache();

        _indexFlushTimer = new System.Timers.Timer
        {
            AutoReset = true,
            Interval = 5000,
        };
        _indexFlushTimer.Elapsed += IndexFlushTimer_Elapsed;
        _indexFlushTimer.Start();

        _cacheWriteSemaphore = new SemaphoreSlim(1);
        _indexWriteSemaphore = new SemaphoreSlim(1);
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        _cacheStream.Dispose();

        _indexFlushTimer.Stop();
        _indexFlushTimer.Elapsed -= IndexFlushTimer_Elapsed;
        _indexFlushTimer.Dispose();

        FlushIndex();
    }

    public bool Set(string key, byte[] value, TimeSpan? expiration = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        bool success = false;

        _cacheWriteSemaphore.Wait();

        try
        {
            ulong keyHash = ComputeFNV1aHash(key);

            if (_index.Entries.ContainsKey(keyHash))
            {
                Delete(key);
            }

            DateTime expirationDate = DateTime.UtcNow + (expiration ?? _defaultExp);
            ushort checksum = FletcherChecksum(value);

            List<long> dataIndices = new List<long>();
            foreach (byte[] dataChunk in SplitByteArray(value, _blockDataSize))
            {
                if (!_index.FreeBlocks.TryPop(out long nextBlock))
                {
                    nextBlock = _cacheStream.Length;
                }

                dataIndices.Add(nextBlock);

                _cacheStream.Seek(nextBlock, SeekOrigin.Begin);
                _cacheStream.Write(BitConverter.GetBytes((ushort)dataChunk.Length));
                byte[] blockData = new byte[_blockDataSize];
                Array.Copy(dataChunk, 0, blockData, 0, dataChunk.Length);
                _cacheStream.Write(blockData);
            }

            _index.Entries.Add(keyHash, new IndexedHeaderEntry()
            {
                Key = keyHash,
                Expiration = expirationDate,
                DataIndices = dataIndices,
                Checksum = checksum
            });

            QueueIndexFlush();

            _cacheStream.Flush();
            success = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());

            // reload the stream from file contents to prevent partial cache writes on errors
            _cacheStream.Dispose();
            _cacheStream = new FileStream(_cacheFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                FileShare.Read, 4096, FileOptions.RandomAccess);
        }
        finally
        {
            _cacheWriteSemaphore.Release();
        }

        return success;
    }

    public byte[] Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        byte[] cacheValue = null;

        try
        {
            ulong keyHash = ComputeFNV1aHash(key);

            if (_index.Entries.TryGetValue(keyHash, out IndexedHeaderEntry headerEntry))
            {
                if (headerEntry.Expiration > DateTime.UtcNow)
                {
                    List<byte> totalBlockData = new();

                    byte[] buffer = new byte[_totalBlockSize];
                    foreach (long index in headerEntry.DataIndices)
                    {
                        _cacheStream.Seek(index, SeekOrigin.Begin);
                        _cacheStream.Read(buffer, 0, buffer.Length);
                        IndexedBlock block = new IndexedBlock(buffer, _blockDataSize);
                        totalBlockData.AddRange(block.Data);
                    }

                    ushort checksum = FletcherChecksum(totalBlockData.ToArray());
                    if (checksum == headerEntry.Checksum)
                    {
                        cacheValue = totalBlockData.ToArray();
                    }
                }
                else
                {
                    Delete(key);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return cacheValue;
    }

    public bool Delete(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        bool deleted = false;

        try
        {
            ulong keyHash = ComputeFNV1aHash(key);
            if (_index.Entries.TryGetValue(keyHash, out IndexedHeaderEntry headerEntry))
            {
                _index.Entries.Remove(keyHash);
                foreach (long index in headerEntry.DataIndices)
                {
                    _index.FreeBlocks.Push(index);
                }
                deleted = true;
            }
        }
        catch (Exception ex)
        {
            Debug.Write(ex.ToString());
        }

        return deleted;
    }

    #endregion

    #region Private Methods

    private void InitCache()
    {
        _index = new IndexedHeader(_indexFileName);
        _cacheStream = new FileStream(_cacheFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, 
            FileShare.Read, 4096, FileOptions.RandomAccess);
    }

    private static ulong ComputeFNV1aHash(string input)
    {
        ulong hash = _FNVOffsetBasis;
        for (int i = 0; i < input.Length; i++)
        {
            hash ^= input[i];
            hash *= _FNVPrime;
        }
        return hash;
    }

    private static ushort FletcherChecksum(byte[] data)
    {
        ushort sum1 = 0;
        ushort sum2 = 0;
        ushort modulus = 65535; // 255 for 8-bit checksum, 65535 for 16-bit

        for (int i = 0; i < data.Length; i++)
        {
            sum1 = (ushort)((sum1 + data[i]) % modulus);
            sum2 = (ushort)((sum2 + sum1) % modulus);
        }

        return (ushort)((sum2 << 8) | sum1);
    }

    private void QueueIndexFlush()
    {
        _indexUpdated = true;
    }

    private void IndexFlushTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        FlushIndex();
    }

    private void FlushIndex()
    {
        if (_indexUpdated)
        {
            _indexUpdated = false;
            _indexWriteSemaphore.Wait();
            try
            {
                FileInfo fileInfo = new FileInfo(_indexFileName);

                File.WriteAllBytes(_indexFileName, _index.Serialize());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                _indexWriteSemaphore.Release();
            }
        }
    }

    private static List<byte[]> SplitByteArray(byte[] data, int chunkSize)
    {
        List<byte[]> chunks = new();

        if (data != null && data.Length > 0)
        {
            int offset = 0;
            while (offset < data.Length)
            {
                int chunkLength = Math.Min(chunkSize, data.Length - offset);
                byte[] chunk = new byte[chunkLength];
                Array.Copy(data, offset, chunk, 0, chunkLength);
                chunks.Add(chunk);
                offset += chunkLength;
            }
        }

        return chunks;
    }

    #endregion
}
