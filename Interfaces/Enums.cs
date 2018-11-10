namespace TaskingSolutions.Interfaces
{

    public enum OnShutdownAction : byte
    {
        TryToFinishJob,
        FinishJob,
        AbortJob
    }

    public enum JobQueuePriority : byte
    {
        Normal,
        Low,
        High
    }

}
