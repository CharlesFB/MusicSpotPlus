using System;
using System.Collections.Generic;
using System.Globalization;

public struct NoteRecipe
{
    public int type;
    public int column;
    public int holdTicks;
    public int dir;
    public int rateIndex;
}

/// <summary>
/// 新谱以 n 开头：n,类型,格子编号,持续格数,方向。多条用分号隔开。
/// 认不出的旧长整数仍按原来的位段解析，纵向位置不再使用。
/// </summary>
public static class NotePayload
{
    public static bool IsGrid(string payload)
    {
        return !string.IsNullOrEmpty(payload) && payload.IndexOf("n,", StringComparison.Ordinal) >= 0;
    }

    public static void Read(string payload, List<NoteRecipe> into)
    {
        into.Clear();
        if (string.IsNullOrEmpty(payload))
            return;
        if (IsGrid(payload))
        {
            string[] parts = payload.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (TryGrid(parts[i], out NoteRecipe recipe))
                    into.Add(recipe);
            }
            return;
        }

        string[] packed = payload.Split(',');
        for (int i = 0; i < packed.Length; i++)
        {
            if (TryLegacy(packed[i], out NoteRecipe recipe))
                into.Add(recipe);
        }
    }

    static bool TryGrid(string text, out NoteRecipe recipe)
    {
        recipe = default;
        if (string.IsNullOrEmpty(text))
            return false;
        text = text.Trim();
        if (!text.StartsWith("n,", StringComparison.Ordinal))
            return false;
        string[] fields = text.Split(',');
        if (fields.Length < 5)
            return false;
        if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
            return false;
        if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cell))
            return false;
        if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hold))
            return false;
        if (!int.TryParse(fields[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int dir))
            return false;
        ChartGrid.Unpack(cell, out int column, out _);
        recipe.type = type;
        recipe.column = column;
        recipe.holdTicks = hold < 0 ? 0 : hold;
        recipe.dir = dir;
        recipe.rateIndex = 1;
        return true;
    }

    static bool TryLegacy(string text, out NoteRecipe recipe)
    {
        recipe = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            return false;
        int len = (int)(value % 10);
        value /= 10;
        int dir = (int)(value % 10);
        value /= 10;
        value /= 1000;
        value /= 10;
        float posX = (value % 1000) / 100f;
        value /= 1000;
        if (value % 10 == 1)
            posX = -posX;
        value /= 10;
        recipe.type = (int)value;
        recipe.column = ChartGrid.FecToColumn(posX);
        recipe.dir = dir;
        recipe.rateIndex = len;
        recipe.holdTicks = recipe.type >= 2 ? HoldTicksFromRate(len) : 0;
        return true;
    }

    public static int HoldTicksFromRate(int rateIndex)
    {
        float rate = 1f;
        switch (rateIndex)
        {
            case 0:
                rate = 0.5f;
                break;
            case 2:
                rate = 2f;
                break;
            case 3:
                rate = 4f;
                break;
        }
        return MathfRoundBeats(2f * rate) * ChartGrid.TicksPerQuarter;
    }

    static int MathfRoundBeats(float beats)
    {
        return (int)(beats + 0.5f);
    }
}
