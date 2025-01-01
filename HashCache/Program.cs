using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

namespace HashCache;

class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net80));

        BenchmarkSwitcher
            .FromTypes([typeof(Benchmarker)])
            .Run(args, config);
    }
}
