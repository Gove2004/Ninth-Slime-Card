using System;
using System.Collections.Generic;
using System.Text;

namespace Slime.Core
{
    // ---------------------------------------------------------------- 工具

    /// <summary>确定性随机数（xorshift）。可复现，便于调试与录像。</summary>
    public sealed class DeterministicRng
    {
        private uint state;

        public DeterministicRng(int seed)
        {
            uint s = seed == 0 ? 0x9E3779B9u : (uint)seed;
            state = s == 0u ? 1u : s;
        }

        public int Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state & 0x7FFFFFFF);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + Next() % (maxExclusive - minInclusive);
        }

        public void Shuffle<T>(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>极简 CSV 解析：支持双引号包裹与转义双引号，纯 C# 无引擎依赖。</summary>
    public static class CsvTable
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Length = 0;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(row.ToArray());
                        row.Clear();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }
    }
}
