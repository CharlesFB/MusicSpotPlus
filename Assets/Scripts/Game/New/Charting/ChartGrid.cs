using UnityEngine;

/// <summary>
/// 48 列、每四分音符 48 格。横向格距固定，纵向间距随 BPM 缩放。
/// 屏幕分数 0 在演奏区中心，±1 在 0.90×0.64 演奏区边缘。
/// </summary>
public static class ChartGrid
{
    public const int TicksPerQuarter = 48;
    public const int Columns = 48;
    public const float ReferenceBpm = 100f;
    public const float PlayfieldX = 0.9f;
    public const float PlayfieldY = 0.64f;
    public const float PitchMin = 0.55f;
    public const float PitchMax = 1.35f;

    public static int Pack(int tick, int column)
    {
        return Mathf.Max(0, tick) * Columns + ClampColumn(column);
    }

    public static void Unpack(int cellId, out int column, out int tick)
    {
        if (cellId < 0)
            cellId = 0;
        column = cellId % Columns;
        tick = cellId / Columns;
    }

    public static int ClampColumn(int column)
    {
        if (column < 0)
            return 0;
        if (column >= Columns)
            return Columns - 1;
        return column;
    }

    public static float PitchScale(float bpm)
    {
        if (bpm < 1f)
            bpm = ReferenceBpm;
        return Mathf.Clamp(bpm / ReferenceBpm, PitchMin, PitchMax);
    }

    public static float ColumnToFec(int column)
    {
        return (ClampColumn(column) / (float)(Columns - 1)) * 2f - 1f;
    }

    public static int FecToColumn(float fec)
    {
        float unit = (fec + 1f) * 0.5f;
        return ClampColumn(Mathf.RoundToInt(unit * (Columns - 1)));
    }

    public static float SecondsPerTick(float bpm)
    {
        if (bpm < 1f)
            bpm = ReferenceBpm;
        return (60f / bpm) / TicksPerQuarter;
    }

    public static float RowPitchPixels(float halfScreenHeight, float bpm)
    {
        return (PlayfieldY * halfScreenHeight / TicksPerQuarter) * PitchScale(bpm);
    }
}
