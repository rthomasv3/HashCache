using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HashCache;

public class Benchmarker
{
    private Random _random;
    private IndexCacheService _indexCacheService;
    private string _key;
    private byte[] _value;
    private Dictionary<string, byte[]> _fixedValues;
    private string _selectedKey;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _random = new Random(11);
        _indexCacheService = new IndexCacheService();
        _fixedValues = new Dictionary<string, byte[]>();

        int totalFixedValues = _random.Next(100, 1000);
        for (int i = 0; i < totalFixedValues; ++i)
        {
            byte[] data1 = new byte[_random.Next(16, 129)];
            byte[] data2 = new byte[_random.Next(16, 8193)];
            _random.NextBytes(data1);
            string key = Convert.ToBase64String(data1);
            _random.NextBytes(data2);
            _fixedValues[key] = data2;

            _indexCacheService.Set(key, data2);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _indexCacheService.Dispose();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        byte[] data1 = new byte[_random.Next(16, 129)];
        byte[] data2 = new byte[_random.Next(16, 8193)];

        _random.NextBytes(data1);
        _key = Convert.ToBase64String(data1);

        _random.NextBytes(data2);
        _value = data2;

        _selectedKey = _fixedValues.Keys.ElementAt(_random.Next(0, _fixedValues.Count));
    }

    [Benchmark]
    public void Set()
    {
        _ = _indexCacheService.Set(_key, _value);
    }

    [Benchmark]
    public void Get()
    {
        _ = _indexCacheService.Get(_selectedKey);
    }
}
