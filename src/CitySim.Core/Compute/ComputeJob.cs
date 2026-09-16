using System;
using System.Collections.Generic;

namespace CitySim.Core;

public enum ComputePriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public interface IComputeJob
{
    string Category { get; }
    ComputePriority Priority { get; }
    int PriorityScore { get; }
    int CostHint { get; }

    void Execute();
    void Apply();
}

public abstract class ComputeJob : IComputeJob
{
    public string Category { get; }
    public ComputePriority Priority { get; }
    public int PriorityScore { get; }
    public int CostHint { get; }

    protected ComputeJob(
        string category,
        ComputePriority priority = ComputePriority.Normal,
        int costHint = 1)
        : this(category, ScoreOf(priority), costHint)
    {
    }

    protected ComputeJob(string category, int priorityScore, int costHint = 1)
    {
        Category = category;
        PriorityScore = Math.Min(100, Math.Max(0, priorityScore));
        CostHint = Math.Max(1, costHint);
        Priority = PriorityScore >= 70 ? ComputePriority.High
            : PriorityScore <= 30 ? ComputePriority.Low
            : ComputePriority.Normal;
    }

    public abstract void Execute();
    public virtual void Apply() { }

    public static int ScoreOf(ComputePriority priority)
    {
        switch (priority)
        {
            case ComputePriority.High:
                return 80;
            case ComputePriority.Low:
                return 20;
            default:
                return 50;
        }
    }
}

public interface IComputeCenter
{
    string Category { get; }
    int Inbox { get; }
    int Dispatch(int maxJobs);
}

public interface IComputeBackend
{
    string Name { get; }
    bool Available { get; }
    void ExecuteBatch(IReadOnlyList<IComputeJob> jobs);
}
