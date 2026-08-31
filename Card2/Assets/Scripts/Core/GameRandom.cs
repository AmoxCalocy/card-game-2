using System;
using System.Collections.Generic;

namespace OneJourney.Core
{
    [Serializable]
    public sealed class GameRandomState
    {
        public int Seed;
        public int Inext;
        public int Inextp;
        public int[] SeedArray;
    }

    /// <summary>
    /// 可复现且可持久化的随机数生成器（实施计划 A0-5 / A3-25）。
    /// 算法与 Unity 当前使用的经典 System.Random 序列兼容，并显式保存内部状态。
    /// </summary>
    public sealed class GameRandom
    {
        private const int Big = int.MaxValue;
        private const int SeedConstant = 161803398;
        private const double SampleScale = 1.0 / Big;

        private readonly int[] _seedArray = new int[56];
        private int _inext;
        private int _inextp;

        public int Seed { get; }

        public GameRandom(int seed)
        {
            Seed = seed;
            Initialize(seed);
        }

        private GameRandom(GameRandomState state)
        {
            Seed = state.Seed;
            _inext = state.Inext;
            _inextp = state.Inextp;
            Array.Copy(state.SeedArray, _seedArray, _seedArray.Length);
        }

        public int Next()
        {
            return InternalSample();
        }

        public int Next(int maxValue)
        {
            if (maxValue < 0) throw new ArgumentOutOfRangeException(nameof(maxValue));
            return (int)(Sample() * maxValue);
        }

        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));
            long range = (long)maxValue - minValue;
            if (range <= int.MaxValue)
            {
                return (int)(Sample() * range) + minValue;
            }

            return (int)((long)(GetSampleForLargeRange() * range) + minValue);
        }

        public float NextFloat()
        {
            return (float)Sample();
        }

        public double NextDouble()
        {
            return Sample();
        }

        public GameRandomState CaptureState()
        {
            var state = new GameRandomState
            {
                Seed = Seed,
                Inext = _inext,
                Inextp = _inextp,
                SeedArray = new int[_seedArray.Length]
            };
            Array.Copy(_seedArray, state.SeedArray, _seedArray.Length);
            return state;
        }

        public static bool TryCreate(GameRandomState state, out GameRandom random, out string issue)
        {
            random = null;
            if (!ValidateState(state, out issue)) return false;
            random = new GameRandom(state);
            return true;
        }

        internal static bool ValidateState(GameRandomState state, out string issue)
        {
            issue = null;
            if (state == null || state.SeedArray == null)
            {
                issue = "随机状态缺失";
                return false;
            }

            if (state.SeedArray.Length != 56)
            {
                issue = "随机状态数组长度无效";
                return false;
            }

            if (state.Inext < 0 || state.Inext >= 56 || state.Inextp < 0 || state.Inextp >= 56)
            {
                issue = "随机状态索引无效";
                return false;
            }

            for (int i = 0; i < state.SeedArray.Length; i++)
            {
                if (state.SeedArray[i] < 0 || state.SeedArray[i] > Big)
                {
                    issue = "随机状态数值越界";
                    return false;
                }
            }

            return true;
        }

        /// <summary>Fisher–Yates 洗牌，同一种子的洗牌结果完全一致。</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
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

            int roll = Next(total);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }

        /// <summary>加权随机抽取元素。验证失败时 issue 非空且返回 default(T)。</summary>
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
            return index < 0 ? default : items[index];
        }

        private void Initialize(int seed)
        {
            int subtraction = seed == int.MinValue ? int.MaxValue : Math.Abs(seed);
            int mj = SeedConstant - subtraction;
            _seedArray[55] = mj;
            int mk = 1;

            for (int i = 1; i < 55; i++)
            {
                int ii = 21 * i % 55;
                _seedArray[ii] = mk;
                mk = mj - mk;
                if (mk < 0) mk += Big;
                mj = _seedArray[ii];
            }

            for (int k = 1; k < 5; k++)
            {
                for (int i = 1; i < 56; i++)
                {
                    _seedArray[i] -= _seedArray[1 + (i + 30) % 55];
                    if (_seedArray[i] < 0) _seedArray[i] += Big;
                }
            }

            _inext = 0;
            _inextp = 21;
        }

        private int InternalSample()
        {
            int locINext = _inext + 1;
            if (locINext >= 56) locINext = 1;
            int locINextp = _inextp + 1;
            if (locINextp >= 56) locINextp = 1;

            int retVal = _seedArray[locINext] - _seedArray[locINextp];
            if (retVal == Big) retVal--;
            if (retVal < 0) retVal += Big;

            _seedArray[locINext] = retVal;
            _inext = locINext;
            _inextp = locINextp;
            return retVal;
        }

        private double Sample()
        {
            return InternalSample() * SampleScale;
        }

        private double GetSampleForLargeRange()
        {
            int result = InternalSample();
            if (InternalSample() % 2 == 0) result = -result;
            double value = result;
            value += Big - 1;
            return value / (2.0 * Big - 1);
        }
    }
}
