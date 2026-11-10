using HMISim.DriverSdk;

namespace HMISim.Drivers.Fins;

/// <summary>合块规划的结果块基类。</summary>
public abstract record FinsBlock;

/// <summary>字块：一次 0101 字读覆盖多个点位（点位在块内的偏移见 <see cref="PlannedRead.OffsetWords"/>）。</summary>
/// <param name="StartWord">块起始字地址。</param>
/// <param name="WordCount">块总字数（含为合并而捎带读入的间隙字）。</param>
/// <param name="Items">块内的点位及其偏移。</param>
public sealed record WordBlock(ushort StartWord, int WordCount, IReadOnlyList<PlannedRead> Items) : FinsBlock;

/// <summary>位块：位访问单独一帧 0101 位读。</summary>
/// <param name="Point">位点位。</param>
public sealed record BitBlock(ReadPoint Point) : FinsBlock;

/// <summary>块内点位：点位 + 相对块首的字偏移。</summary>
/// <param name="Point">点位。</param>
/// <param name="OffsetWords">相对块首的字偏移。</param>
public sealed record PlannedRead(ReadPoint Point, int OffsetWords);

/// <summary>
/// 读合块规划（纯函数）：字点位按地址排序，相邻/小间隙合并为一次块读，按单帧上限切分；位点位各自成块。
/// 这是调度模型「无需求不通讯、有需求合并读」在 FINS 上的落地。
/// </summary>
public static class FinsBlockPlanner
{
    /// <summary>FINS 0101 单次建议最大字数（组网帧长余量内）。</summary>
    public const int DefaultMaxWordsPerFrame = 1000;

    /// <summary>生成规划。</summary>
    /// <param name="points">本轮到期点位（地址须为 <see cref="FinsAddress"/>）。</param>
    /// <param name="maxWordsPerFrame">单帧最大字数。</param>
    /// <param name="maxGapWords">允许为合并而捎带读入的最大间隙字数。</param>
    public static IReadOnlyList<FinsBlock> Plan(
        IReadOnlyList<ReadPoint> points,
        int maxWordsPerFrame = DefaultMaxWordsPerFrame,
        int maxGapWords = 2)
    {
        var blocks = new List<FinsBlock>();
        var wordPoints = new List<(FinsAddress Address, ReadPoint Point, int Span)>();
        foreach (var point in points)
        {
            var address = (FinsAddress)point.Address;
            if (point.DataType == TagDataType.Bool || address.IsBit)
                blocks.Add(new BitBlock(point));
            else
                wordPoints.Add((address, point, FinsCodec.SpanWords(point.DataType, point.LengthWords)));
        }

        wordPoints.Sort((a, b) => a.Address.Word.CompareTo(b.Address.Word));
        List<PlannedRead>? items = null;
        var blockStart = 0;
        var blockEnd = 0; // 不含
        foreach (var (address, point, span) in wordPoints)
        {
            var pointEnd = address.Word + span;
            if (items is null)
            {
                blockStart = address.Word;
                blockEnd = pointEnd;
                items = [new PlannedRead(point, 0)];
                continue;
            }

            var gap = address.Word - blockEnd;
            var mergedCount = pointEnd - blockStart;
            if (gap <= maxGapWords && mergedCount <= maxWordsPerFrame)
            {
                items.Add(new PlannedRead(point, address.Word - blockStart));
                blockEnd = Math.Max(blockEnd, pointEnd);
            }
            else
            {
                blocks.Add(new WordBlock((ushort)blockStart, blockEnd - blockStart, items));
                blockStart = address.Word;
                blockEnd = pointEnd;
                items = [new PlannedRead(point, 0)];
            }
        }

        if (items is not null)
            blocks.Add(new WordBlock((ushort)blockStart, blockEnd - blockStart, items));
        return blocks;
    }
}
