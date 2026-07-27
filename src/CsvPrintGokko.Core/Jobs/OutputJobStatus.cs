namespace CsvPrintGokko.Core.Jobs;

public enum OutputJobState
{
    Running,
    Completed,
    Failed
}

/// <summary>出力ジョブの進捗状態。バックグラウンドTaskから更新され、ポーリングで参照される。</summary>
public sealed class OutputJobStatus
{
    public OutputJobState State { get; internal set; } = OutputJobState.Running;
    public int Processed { get; internal set; }
    public required int Total { get; init; }
    public string? ErrorMessage { get; internal set; }
}
