using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 曲内 BPM 分段。TickAt 按每段的恒定 BPM 积分，不把速度当成全曲一个常数。
/// </summary>
public sealed class TempoMap
{
    struct Mark
    {
        public float time;
        public float bpm;
    }

    readonly List<Mark> marks = new List<Mark>();

    public void SetBase(float bpm)
    {
        marks.Clear();
        marks.Add(new Mark { time = 0f, bpm = Sanitize(bpm) });
    }

    public void Add(float time, float bpm)
    {
        if (marks.Count == 0)
            SetBase(bpm);
        bpm = Sanitize(bpm);
        if (time < 0f)
            time = 0f;
        for (int i = 0; i < marks.Count; i++)
        {
            if (Mathf.Abs(marks[i].time - time) < 0.0001f)
            {
                marks[i] = new Mark { time = time, bpm = bpm };
                return;
            }
        }
        marks.Add(new Mark { time = time, bpm = bpm });
        marks.Sort((a, b) => a.time.CompareTo(b.time));
    }

    public float BpmAt(float time)
    {
        if (marks.Count == 0)
            return ChartGrid.ReferenceBpm;
        float bpm = marks[0].bpm;
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i].time > time)
                break;
            bpm = marks[i].bpm;
        }
        return bpm;
    }

    public float TickAt(float time)
    {
        if (time <= 0f || marks.Count == 0)
            return 0f;
        float tick = 0f;
        float cursor = 0f;
        float bpm = marks[0].bpm;
        int index = 0;
        while (cursor < time)
        {
            float next = time;
            if (index + 1 < marks.Count && marks[index + 1].time < time)
                next = marks[index + 1].time;
            tick += (next - cursor) / ChartGrid.SecondsPerTick(bpm);
            cursor = next;
            if (index + 1 < marks.Count && marks[index + 1].time <= cursor + 0.00001f)
            {
                index++;
                bpm = marks[index].bpm;
            }
            else
                break;
        }
        return tick;
    }

    public float SecondsPerBeat(float time)
    {
        return 60f / BpmAt(time);
    }

    /// <summary>
    /// 旧谱把时长写成秒，并按曲子起始 BPM 对齐。BPM 变化后按比例缩短或拉长。
    /// </summary>
    public float ScaleFromBase(float seconds, float time)
    {
        float bpm = BpmAt(time);
        if (bpm < 1f)
            return seconds;
        return seconds * BpmAt(0f) / bpm;
    }

    static float Sanitize(float bpm)
    {
        return bpm < 1f ? ChartGrid.ReferenceBpm : bpm;
    }
}

public static class ChartSession
{
    public static TempoMap Tempo;
}
