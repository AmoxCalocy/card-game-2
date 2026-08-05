using System;
using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>
    /// 可复现的带种子随机数生成器（实施计划 A0-5）。
    /// 包装 System.Random，支持洗牌、加权抽取与空池/零权重保护。
    /// </summary>
    public sealed class GameRandom
    {
        private System.Random _rng;

        public int Seed { get; }

        public GameRandom(int seed)
        {
            Seed = seed;
            _rng = new System.Random(seed);
        }

        public int Next()
        {
            return _rng.Next();
        }

        public int Next(int maxValue)
        {
            return _rng.Next(maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            return _rng.Next(minValue, maxValue);
        }

        public float NextFloat()
        {
            return (float)_rng.NextDouble();
        }

        public double NextDouble()
        {
            return _rng.NextDouble();
        }

        /// <summary>Fisher–Yates 洗牌，同一种子的洗牌结果完全一致。</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        /// <summary>
        /// 加权随机抽取，返回命中索引。
        /// 空数组、所有权重非正或总和为零时通过 issue 报告明确原因并返回 -1。
        /// </summary>
        public int WeightedPick(int[] weights, out string issue)
        {
            issue = null;

            if (weights == null || weights.Length == 0)
            {
                issue = "加权池为空";
                return -1;
            }

            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] < 0)
                {
                    issue = "权重[" + i + "]为负：" + weights[i];
                    return -1;
                }

                total += weights[i];
            }

            if (total <= 0)
            {
                issue = "权重总和为零";
                return -1;
            }

            int roll = _rng.Next(total);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                {
                    return i;
                }
            }

            // 浮点精度兜底
            return weights.Length - 1;
        }

        /// <summary>
        /// 加权随机抽取元素。验证失败时 issue 非空且返回 default(T)。
        /// </summary>
        public T WeightedPick<T>(IList<T> items, Func<T, int> weightSelector, out string issue)
        {
            if (items == null || items.Count == 0)
            {
                issue = "加权池为空";
                return default;
            }

            var weights = new int[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                weights[i] = weightSelector(items[i]);
            }

            int index = WeightedPick(weights, out issue);
            if (index < 0)
            {
                return default;
            }

            return items[index];
        }
    }
}
