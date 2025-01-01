using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace HashCache;

public class SparseCacheService : IDisposable
{
    #region Native

    private const int FSCTL_SET_SPARSE = 0x000900c4; // 590020

    [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        int dwIoControlCode,
        IntPtr InBuffer,
        int nInBufferSize,
        IntPtr OutBuffer,
        int nOutBufferSize,
        ref int pBytesReturned,
        [In] ref NativeOverlapped lpOverlapped
    );

    #endregion

    #region Fields

    private const long _hashIndexPrime = 37;
    private const long _hashIndexMax = 1_000_000_007;
    private const string _cacheFileLocation = "cache.dat";
    private const short _headerSize = 2;
    private const int _cacheBlockSize = 1024;

    private FileStream _cacheFile;

    #endregion

    #region Constructor(s)

    public SparseCacheService()
    {
        InitCacheFile();
    }

    #endregion

    #region Public Methods

    public void Dispose()
    {
        _cacheFile?.Dispose();
    }

    public string Get(string key)
    {
        string cacheValue = null;

        long hashIndex = ComputeHashIndex(key);
        long hashLocation = _headerSize + _cacheBlockSize * hashIndex;

        if (_cacheFile.Length > hashIndex)
        {
            byte[] cacheData = new byte[_cacheBlockSize];
            _cacheFile.Seek(hashLocation, SeekOrigin.Begin);
            _cacheFile.Read(cacheData, 0, _cacheBlockSize);
            cacheValue = Encoding.UTF8.GetString(cacheData).Trim('\0');
        }

        return cacheValue;
    }

    public bool Set(string key, string value)
    {
        bool success = false;

        try
        {
            long hashIndex = ComputeHashIndex(key);
            long hashLocation = _headerSize + _cacheBlockSize * hashIndex;
            long size = hashLocation + _cacheBlockSize;

            if (size > _cacheFile.Length)
            {
                _cacheFile.SetLength(size);
            }

            byte[] cacheData = new byte[_cacheBlockSize];
            byte[] cacheValue = Encoding.UTF8.GetBytes(value);
            Array.Copy(cacheValue, 0, cacheData, 0, cacheValue.Length);
            _cacheFile.Seek(hashLocation, SeekOrigin.Begin);
            _cacheFile.Write(cacheData);
            _cacheFile.Flush();

            success = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return success;
    }

    #endregion

    #region Private Methods

    private void InitCacheFile()
    {
        if (!File.Exists(_cacheFileLocation))
        {
            _cacheFile = new FileStream("cache.dat", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.RandomAccess);
            _cacheFile.Write(BitConverter.GetBytes((ushort)1), 0, 2);
            MarkAsSparseFile(_cacheFile.SafeFileHandle);
        }
        else
        {
            _cacheFile = new FileStream("cache.dat", FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.RandomAccess);
        }
    }

    private static long ComputeHashIndex(string input)
    {
        long hash = 0;
        for (int i = 0; i < input.Length; i++)
        {
            hash = (hash * _hashIndexPrime + input[i]) % _hashIndexMax;
        }
        return hash;
    }

    private static void MarkAsSparseFile(SafeFileHandle fileHandle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int bytesReturned = 0;
            NativeOverlapped lpOverlapped = new NativeOverlapped();
            bool result =
                DeviceIoControl(
                    fileHandle,
                    FSCTL_SET_SPARSE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    ref lpOverlapped);
            if (result == false)
                throw new Win32Exception();
        }
    }

    #endregion
}
