using System;
using System.Collections.Generic;

namespace CitySim.Core;

public sealed class GpuDeferredBackend : IComputeBackend
{
    public string Name => "gpu-deferred";
    public bool Available => false;

    public void ExecuteBatch(IReadOnlyList<IComputeJob> jobs)
    {
        throw new NotSupportedException(
            "GPU 后端是可选实验，尚未接到调度器。调度器只排队和分顺序，不决定在 CPU 还是 GPU 上算。");
    }
}
