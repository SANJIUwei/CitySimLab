using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CitySim.Core;

public sealed class CpuParallelBackend : IComputeBackend
{
    public string Name => "cpu-parallel";
    public bool Available => true;
    public int Degree { get; }

    public CpuParallelBackend(int degree = 0)
    {
        Degree = degree > 0 ? degree : Math.Max(1, Environment.ProcessorCount);
    }

    public void ExecuteBatch(IReadOnlyList<IComputeJob> jobs)
    {
        if (jobs.Count == 0)
            return;
        if (jobs.Count == 1 || Degree == 1)
        {
            for (int i = 0; i < jobs.Count; i++)
                jobs[i].Execute();
            return;
        }

        var options = new ParallelOptions { MaxDegreeOfParallelism = Degree };
        Parallel.For(0, jobs.Count, options, i => jobs[i].Execute());
    }
}
