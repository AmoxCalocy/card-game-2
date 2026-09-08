using System;
using UnityEngine;

namespace OneJourney.Core
{
    /// <summary>A3-26 当前战役的引导进度。独立于战役规则数据，但会随“新游戏”重置。</summary>
    public static class TutorialProgressService
    {
        private const string DefaultStorageKey = "OneJourney.TutorialProgress.v1";

        private static string _storageKeyOverride;
        private static int _completedMask;
        private static bool _initialized;

        public static event Action Changed;

        private static string StorageKey => string.IsNullOrEmpty(_storageKeyOverride)
            ? DefaultStorageKey
            : _storageKeyOverride;

        public static int CompletedMask
        {
            get
            {
                EnsureInitialized();
                return _completedMask;
            }
        }

        public static int CompletedCount
        {
            get
            {
                EnsureInitialized();
                int count = 0;
                int value = _completedMask;
                while (value != 0)
                {
                    count += value & 1;
                    value >>= 1;
                }
                return count;
            }
        }

        public static bool AllCompleted => CompletedMask == TutorialContent.AllTopicMask;

        public static void Initialize()
        {
            _completedMask = PlayerPrefs.GetInt(StorageKey, 0) & TutorialContent.AllTopicMask;
            _initialized = true;
            Changed?.Invoke();
        }

        public static bool IsCompleted(TutorialTopic topic)
        {
            EnsureInitialized();
            int bit = TopicBit(topic);
            return (_completedMask & bit) != 0;
        }

        public static bool Complete(TutorialTopic topic)
        {
            EnsureInitialized();
            int bit = TopicBit(topic);
            if ((_completedMask & bit) != 0) return false;

            _completedMask |= bit;
            Save();
            return true;
        }

        public static void SkipAll()
        {
            EnsureInitialized();
            if (_completedMask == TutorialContent.AllTopicMask) return;
            _completedMask = TutorialContent.AllTopicMask;
            Save();
        }

        public static void BeginNewRun()
        {
            ResetProgress();
        }

        public static void ResetProgress()
        {
            EnsureInitialized();
            if (_completedMask == 0)
            {
                PlayerPrefs.DeleteKey(StorageKey);
                PlayerPrefs.Save();
                Changed?.Invoke();
                return;
            }

            _completedMask = 0;
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void SetStorageKeyForTests(string storageKey)
        {
            if (string.IsNullOrWhiteSpace(storageKey))
                throw new ArgumentException("测试存储键不能为空", nameof(storageKey));

            _storageKeyOverride = storageKey;
            _initialized = false;
            Initialize();
        }

        public static void ReloadForTests()
        {
            _initialized = false;
            Initialize();
        }

        public static void ResetStorageKeyForTests()
        {
            if (!string.IsNullOrEmpty(_storageKeyOverride))
            {
                PlayerPrefs.DeleteKey(_storageKeyOverride);
                PlayerPrefs.Save();
            }

            _storageKeyOverride = null;
            _completedMask = 0;
            _initialized = false;
        }

        private static void EnsureInitialized()
        {
            if (!_initialized) Initialize();
        }

        private static int TopicBit(TutorialTopic topic)
        {
            int index = (int)topic;
            if (index < 0 || index >= TutorialContent.TutorialCount)
                throw new ArgumentOutOfRangeException(nameof(topic), topic, "未知的引导主题");
            return 1 << index;
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(StorageKey, _completedMask);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
