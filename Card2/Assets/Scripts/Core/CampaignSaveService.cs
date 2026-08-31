using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace OneJourney.Core
{
    /// <summary>
    /// A3-25 本地战役存档：版本化 JSON、SHA-256 完整性校验、主档/备份恢复与安全点限制。
    /// </summary>
    public static class CampaignSaveService
    {
        public const int CurrentSchemaVersion = 1;

        private const string PrimaryFileName = "campaign-save.json";
        private const string BackupFileName = "campaign-save.backup.json";
        private const string TempFileName = "campaign-save.tmp";

        private static string _storageDirectoryOverride;
        private static bool _autosaveEnabled;

        public static bool HasValidSave { get; private set; }
        public static string StatusMessage { get; private set; } = "尚未检查存档";
        public static string PrimaryPath => Path.Combine(StorageDirectory, PrimaryFileName);
        public static string BackupPath => Path.Combine(StorageDirectory, BackupFileName);

        public static event Action Changed;

        private static string StorageDirectory => string.IsNullOrEmpty(_storageDirectoryOverride)
            ? Application.persistentDataPath
            : _storageDirectoryOverride;

        [Serializable]
        private sealed class SaveEnvelope
        {
            public int SchemaVersion;
            public string Payload;
            public string IntegrityHash;
        }

        public static void Initialize()
        {
            _autosaveEnabled = true;
            RefreshStatus();
        }

        internal static bool TryAutosave(SaveCheckpointKind checkpoint, out string message)
        {
            if (!_autosaveEnabled)
            {
                message = "自动存档尚未初始化";
                return false;
            }

            return TrySave(checkpoint, out message);
        }

        public static void RefreshStatus()
        {
            if (TryReadValidated(PrimaryPath, out CampaignSaveData primary, out string primaryIssue))
            {
                SetStatus(true, BuildSummary("可继续", primary));
                return;
            }

            if (TryReadValidated(BackupPath, out CampaignSaveData backup, out string backupIssue))
            {
                SetStatus(true, BuildSummary("主存档不可用，将从备份恢复", backup));
                return;
            }

            bool hasAnyFile = File.Exists(PrimaryPath) || File.Exists(BackupPath);
            if (!hasAnyFile)
            {
                SetStatus(false, "暂无可继续的存档");
                return;
            }

            string issue = !string.IsNullOrEmpty(primaryIssue) ? primaryIssue : backupIssue;
            SetStatus(false, "存档不可用：" + issue + "。请开始新游戏。");
        }

        public static bool TrySave(SaveCheckpointKind checkpoint, out string message)
        {
            message = null;
            if (!RunSession.CanCaptureCheckpoint(checkpoint, out string issue))
            {
                message = "当前不是安全存档点：" + issue;
                SetStatus(HasValidSave, message);
                return false;
            }

            CampaignSaveData data = RunSession.CaptureSaveData(checkpoint);
            if (!CampaignSaveValidator.Validate(data, out issue))
            {
                message = "存档数据校验失败：" + issue;
                Debug.LogError("[CampaignSave] " + message);
                SetStatus(HasValidSave, message);
                return false;
            }

            string tempPath = Path.Combine(StorageDirectory, TempFileName);
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                string payload = JsonUtility.ToJson(data, false);
                var envelope = new SaveEnvelope
                {
                    SchemaVersion = CurrentSchemaVersion,
                    Payload = payload,
                    IntegrityHash = ComputeHash(payload)
                };
                string json = JsonUtility.ToJson(envelope, true);
                WriteDurable(tempPath, json);

                if (File.Exists(PrimaryPath))
                {
                    if (TryReadValidated(PrimaryPath, out _, out _))
                    {
                        if (File.Exists(BackupPath)) File.Delete(BackupPath);
                        File.Replace(tempPath, PrimaryPath, BackupPath);
                    }
                    else
                    {
                        File.Delete(PrimaryPath);
                        File.Move(tempPath, PrimaryPath);
                    }
                }
                else
                {
                    File.Move(tempPath, PrimaryPath);
                }

                message = BuildSummary("自动存档完成", data);
                SetStatus(true, message);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is CryptographicException)
            {
                message = "写入存档失败：" + ex.Message;
                Debug.LogError("[CampaignSave] " + message);
                SetStatus(HasValidSave, message);
                return false;
            }
            finally
            {
                TryDelete(tempPath);
            }
        }

        public static bool TryLoad(out SaveCheckpointKind checkpoint, out string message)
        {
            checkpoint = SaveCheckpointKind.None;
            message = null;

            bool usedBackup = false;
            CampaignSaveData data;
            string primaryIssue;
            if (!TryReadValidated(PrimaryPath, out data, out primaryIssue))
            {
                if (!TryReadValidated(BackupPath, out data, out string backupIssue))
                {
                    message = File.Exists(PrimaryPath) || File.Exists(BackupPath)
                        ? "存档不可用：" + (!string.IsNullOrEmpty(primaryIssue) ? primaryIssue : backupIssue) + "。请开始新游戏。"
                        : "没有可继续的存档";
                    SetStatus(false, message);
                    return false;
                }

                usedBackup = true;
            }

            if (!RunSession.TryRestoreFromSaveData(data, out string restoreIssue))
            {
                message = "存档恢复失败：" + restoreIssue;
                Debug.LogError("[CampaignSave] " + message);
                SetStatus(false, message);
                return false;
            }

            checkpoint = (SaveCheckpointKind)data.Checkpoint;
            if (usedBackup)
            {
                RecoverPrimaryFromBackup();
                message = "主存档损坏，已从备份恢复";
            }
            else
            {
                message = "存档读取成功";
            }

            SetStatus(true, BuildSummary(message, data));
            return true;
        }

        public static void DeleteActiveSave()
        {
            if (!_autosaveEnabled) return;
            try
            {
                TryDelete(PrimaryPath);
                TryDelete(BackupPath);
                TryDelete(Path.Combine(StorageDirectory, TempFileName));
                SetStatus(false, "本局已结束，没有可继续的存档");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogError("[CampaignSave] 删除已结束战役的存档失败：" + ex.Message);
                RefreshStatus();
            }
        }

        public static void SetStorageDirectoryForTests(string directory)
        {
            _storageDirectoryOverride = directory;
            _autosaveEnabled = true;
            RefreshStatus();
        }

        public static void ResetStorageDirectoryForTests()
        {
            _storageDirectoryOverride = null;
            _autosaveEnabled = false;
            HasValidSave = false;
            StatusMessage = "尚未检查存档";
            Changed?.Invoke();
        }

        private static bool TryReadValidated(string path, out CampaignSaveData data, out string issue)
        {
            data = null;
            issue = null;
            if (!File.Exists(path))
            {
                issue = "文件不存在";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    issue = "文件为空";
                    return false;
                }

                SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                if (envelope == null)
                {
                    issue = "JSON 结构无效";
                    return false;
                }

                if (envelope.SchemaVersion != CurrentSchemaVersion)
                {
                    issue = "不支持的存档版本 " + envelope.SchemaVersion + "（当前版本 " + CurrentSchemaVersion + "）";
                    return false;
                }

                if (string.IsNullOrEmpty(envelope.Payload) || string.IsNullOrEmpty(envelope.IntegrityHash))
                {
                    issue = "缺少关键字段";
                    return false;
                }

                string actualHash = ComputeHash(envelope.Payload);
                if (!string.Equals(actualHash, envelope.IntegrityHash, StringComparison.OrdinalIgnoreCase))
                {
                    issue = "完整性校验失败";
                    return false;
                }

                data = JsonUtility.FromJson<CampaignSaveData>(envelope.Payload);
                if (!CampaignSaveValidator.Validate(data, out issue))
                {
                    data = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is CryptographicException)
            {
                issue = "读取失败：" + ex.Message;
                data = null;
                return false;
            }
        }

        private static void RecoverPrimaryFromBackup()
        {
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                string recoveryPath = Path.Combine(StorageDirectory, TempFileName);
                File.Copy(BackupPath, recoveryPath, true);
                if (File.Exists(PrimaryPath)) File.Delete(PrimaryPath);
                File.Move(recoveryPath, PrimaryPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Debug.LogWarning("[CampaignSave] 已从备份载入，但恢复主存档文件失败：" + ex.Message);
            }
        }

        private static void WriteDurable(string path, string content)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private static string ComputeHash(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var result = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
                return result.ToString();
            }
        }

        private static string BuildSummary(string prefix, CampaignSaveData data)
        {
            string region = data.Map.Region == (int)ContentRegion.Jungle ? "密林" : "草原";
            int layer = 0;
            if (data.Map.CurrentNodeIndex >= 0 && data.Map.CurrentNodeIndex < data.Map.Nodes.Count)
                layer = data.Map.Nodes[data.Map.CurrentNodeIndex].Layer;
            return prefix + "：" + region + "第 " + layer + " 层，种子 " + data.Seed;
        }

        private static void SetStatus(bool hasValidSave, string message)
        {
            HasValidSave = hasValidSave;
            StatusMessage = message ?? string.Empty;
            Changed?.Invoke();
        }

        private static void TryDelete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
        }
    }

    internal static class CampaignSaveValidator
    {
        public static bool Validate(CampaignSaveData data, out string issue)
        {
            issue = null;
            if (data == null) return Fail("存档主体缺失", out issue);
            if (!Enum.IsDefined(typeof(SaveCheckpointKind), data.Checkpoint)
                || data.Checkpoint == (int)SaveCheckpointKind.None)
                return Fail("检查点类型无效", out issue);
            if (data.SavedUtcTicks <= 0) return Fail("保存时间缺失", out issue);
            if (data.Seed <= 0) return Fail("随机种子无效", out issue);
            if (data.ElapsedSeconds < 0) return Fail("累计用时无效", out issue);
            if (!GameRandom.ValidateState(data.Random, out issue)) return false;
            if (data.Random.Seed != data.Seed) return Fail("随机状态与种子不一致", out issue);
            if (!ValidateResources(data.Resources, out issue)) return false;
            if (!ValidateDeck(data.Deck, out issue)) return false;
            if (!ValidatePartners(data.Partners, data.ActivePartnerIds, out issue)) return false;
            if (!ValidateIds(data.RelicIds, "遗物", id => RelicCatalog.Find(id) != null, out issue)) return false;
            if (!ValidateIds(data.BuildingIds, "建筑", id => BuildingCatalog.Find(id) != null, out issue)) return false;
            if (!ValidateIds(data.EventFlags, "事件标记", id => EventCatalog.Find(id) != null, out issue)) return false;
            if (data.Flags == null) return Fail("战役标记缺失", out issue);
            if (!ValidateMap(data.Map, out issue)) return false;
            if (!ValidateRunRecords(data.RunRecords, out issue)) return false;

            var checkpoint = (SaveCheckpointKind)data.Checkpoint;
            if (checkpoint == SaveCheckpointKind.NodeEntry && data.Map.CurrentNodeIndex < 0)
                return Fail("节点入口检查点缺少当前位置", out issue);
            if (checkpoint == SaveCheckpointKind.Camp)
            {
                if (data.Map.CurrentNodeIndex < 0
                    || data.Map.Nodes[data.Map.CurrentNodeIndex].Type != (int)NodeType.Camp)
                    return Fail("营地检查点不在营地节点", out issue);
            }

            return true;
        }

        private static bool ValidateResources(CampaignResourceSaveData data, out string issue)
        {
            issue = null;
            if (data == null) return Fail("资源数据缺失", out issue);
            if (data.Food < 0 || data.Food > GameStartParameters.MaxFood) return Fail("粮食越界", out issue);
            if (data.Wealth < 0 || data.Wealth > GameStartParameters.MaxWealth) return Fail("财富越界", out issue);
            if (data.Reputation < 0 || data.Reputation > GameStartParameters.MaxReputation) return Fail("声望越界", out issue);
            if (data.Materials < 0 || data.Materials > GameStartParameters.MaxBuildingMaterials) return Fail("建材越界", out issue);
            if (data.Risk < 0 || data.Risk > GameStartParameters.RiskThreshold) return Fail("风险越界", out issue);
            if (data.PlayerFatigue < 0 || data.PlayerFatigue > CombatStatus.MaxFatigue) return Fail("主角疲劳越界", out issue);
            if (data.PlayerDisease < 0 || data.PlayerDisease > CombatStatus.MaxDisease) return Fail("主角疾病越界", out issue);
            return true;
        }

        private static bool ValidateDeck(CampaignDeckSaveData data, out string issue)
        {
            issue = null;
            if (data == null || data.Cards == null || data.UpgradedCardIds == null)
                return Fail("牌组关键字段缺失", out issue);
            if (data.Cards.Count < GameStartParameters.MinDeckSize || data.Cards.Count > GameStartParameters.MaxDeckSize)
                return Fail("牌组数量越界", out issue);
            foreach (string id in data.Cards)
            {
                if (string.IsNullOrEmpty(id) || !CardCatalog.Exists(id)) return Fail("牌组包含未知卡牌：" + id, out issue);
            }

            var upgraded = new HashSet<string>();
            foreach (string id in data.UpgradedCardIds)
            {
                if (string.IsNullOrEmpty(id) || !upgraded.Add(id)) return Fail("升级卡标记重复或为空", out issue);
                if (!data.Cards.Contains(id)) return Fail("升级卡不在牌组中：" + id, out issue);
            }

            return true;
        }

        private static bool ValidatePartners(List<PartnerSaveData> partners, List<string> activeIds, out string issue)
        {
            issue = null;
            if (partners == null || activeIds == null) return Fail("伙伴关键字段缺失", out issue);
            if (partners.Count != PartnerRoster.All.Count) return Fail("伙伴数据数量不完整", out issue);

            var seen = new HashSet<string>();
            var byId = new Dictionary<string, PartnerSaveData>();
            foreach (var saved in partners)
            {
                if (saved == null || string.IsNullOrEmpty(saved.Id) || !seen.Add(saved.Id))
                    return Fail("伙伴 ID 缺失或重复", out issue);
                var current = PartnerRoster.Find(saved.Id);
                if (current == null) return Fail("未知伙伴：" + saved.Id, out issue);
                if (saved.CurrentHp < 0 || saved.CurrentHp > current.Def.MaxHp) return Fail(saved.Id + " 生命越界", out issue);
                if (saved.Loyalty < 0 || saved.Loyalty > 100) return Fail(saved.Id + " 忠诚度越界", out issue);
                if (saved.Disease < 0 || saved.Disease > CombatStatus.MaxDisease) return Fail(saved.Id + " 疾病越界", out issue);
                if (saved.Fatigue < 0 || saved.Fatigue > CombatStatus.MaxFatigue) return Fail(saved.Id + " 疲劳越界", out issue);
                int effectiveMax = Math.Max(1, current.Def.MaxHp - saved.Disease * CombatStatus.DiseaseMaxHpPenalty);
                if (saved.CurrentHp > effectiveMax) return Fail(saved.Id + " 生命高于疾病后的有效上限", out issue);
                byId.Add(saved.Id, saved);
            }

            if (activeIds.Count > GameStartParameters.MaxPartySize - 1) return Fail("上阵伙伴超过上限", out issue);
            var activeSeen = new HashSet<string>();
            foreach (string id in activeIds)
            {
                if (string.IsNullOrEmpty(id) || !activeSeen.Add(id) || !byId.TryGetValue(id, out var partner))
                    return Fail("上阵伙伴 ID 无效或重复", out issue);
                if (!partner.IsRecruited || partner.CurrentHp <= 0) return Fail("未招募或阵亡伙伴不能上阵：" + id, out issue);
            }

            return true;
        }

        private static bool ValidateMap(RegionMapSaveData map, out string issue)
        {
            issue = null;
            if (map == null || map.Nodes == null || map.VisitedIndexes == null || map.Path == null)
                return Fail("地图关键字段缺失", out issue);
            if (map.Region != (int)ContentRegion.Plains && map.Region != (int)ContentRegion.Jungle)
                return Fail("区域无效", out issue);
            if (map.Nodes.Count != 10) return Fail("地图节点数量不完整", out issue);
            if (map.CurrentNodeIndex < -1 || map.CurrentNodeIndex >= map.Nodes.Count)
                return Fail("当前节点索引无效", out issue);

            var ids = new HashSet<string>();
            int[] layerCounts = new int[RegionMap.LayerCount + 1];
            int[] incoming = new int[map.Nodes.Count];
            for (int i = 0; i < map.Nodes.Count; i++)
            {
                var node = map.Nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id) || !ids.Add(node.Id)) return Fail("地图节点 ID 缺失或重复", out issue);
                if (node.Layer < 1 || node.Layer > RegionMap.LayerCount) return Fail("地图层数无效", out issue);
                if (!Enum.IsDefined(typeof(NodeType), node.Type)) return Fail("地图节点类型无效", out issue);
                if (string.IsNullOrEmpty(node.DisplayName) || node.EnemyPoolIds == null || node.EventPoolIds == null || node.NextIndexes == null)
                    return Fail("地图节点关键字段缺失", out issue);
                layerCounts[node.Layer]++;

                var type = (NodeType)node.Type;
                if (type == NodeType.Event)
                {
                    if (node.EventPoolIds.Length == 0 || node.EnemyPoolIds.Length != 0) return Fail("事件节点内容池无效", out issue);
                    foreach (string id in node.EventPoolIds)
                        if (EventCatalog.Find(id) == null) return Fail("事件池引用不存在：" + id, out issue);
                }
                else if (type == NodeType.Combat || type == NodeType.Elite || type == NodeType.Boss)
                {
                    if (node.EnemyPoolIds.Length == 0 || node.EventPoolIds.Length != 0) return Fail("战斗节点内容池无效", out issue);
                    foreach (string id in node.EnemyPoolIds)
                        if (EnemyUnit.CreateById(id) == null) return Fail("敌人池引用不存在：" + id, out issue);
                }
                else if (node.EnemyPoolIds.Length != 0 || node.EventPoolIds.Length != 0)
                {
                    return Fail("营地节点不应包含内容池", out issue);
                }

                var edgeSeen = new HashSet<int>();
                foreach (int next in node.NextIndexes)
                {
                    if (next < 0 || next >= map.Nodes.Count || !edgeSeen.Add(next)) return Fail("地图连接索引无效或重复", out issue);
                    if (map.Nodes[next] == null || map.Nodes[next].Layer != node.Layer + 1) return Fail("地图连接未指向下一层", out issue);
                    incoming[next]++;
                }
                if (node.Layer < RegionMap.LayerCount && node.NextIndexes.Count == 0) return Fail("非末层节点缺少出边", out issue);
                if (node.Layer == RegionMap.LayerCount && node.NextIndexes.Count != 0) return Fail("末层节点不应有出边", out issue);
            }

            if (layerCounts[1] != 3 || layerCounts[2] != 3 || layerCounts[3] != 3 || layerCounts[4] != 1)
                return Fail("地图层节点构成无效", out issue);
            for (int i = 0; i < map.Nodes.Count; i++)
                if (map.Nodes[i].Layer > 1 && incoming[i] == 0) return Fail("地图节点缺少入边", out issue);

            if (map.VisitedIndexes.Count != map.Path.Count) return Fail("已访问节点与路径数量不一致", out issue);
            var visited = new HashSet<int>();
            for (int i = 0; i < map.Path.Count; i++)
            {
                int index = map.Path[i];
                if (index < 0 || index >= map.Nodes.Count || !visited.Add(index)) return Fail("地图路径索引无效或重复", out issue);
                if (map.VisitedIndexes[i] != index) return Fail("已访问节点与路径不一致", out issue);
                if (map.Nodes[index].Layer != i + 1) return Fail("地图路径层级不连续", out issue);
                if (i > 0 && !map.Nodes[map.Path[i - 1]].NextIndexes.Contains(index)) return Fail("地图路径连接不成立", out issue);
            }

            if (map.CurrentNodeIndex == -1 && map.Path.Count != 0) return Fail("起点状态不应包含访问路径", out issue);
            if (map.CurrentNodeIndex >= 0 && (map.Path.Count == 0 || map.Path[map.Path.Count - 1] != map.CurrentNodeIndex))
                return Fail("当前位置与路径末端不一致", out issue);
            return true;
        }

        private static bool ValidateRunRecords(List<RunRecordSaveData> records, out string issue)
        {
            issue = null;
            if (records == null) return Fail("本局记录字段缺失", out issue);
            if (records.Count > 200) return Fail("本局记录超过上限", out issue);
            foreach (var record in records)
            {
                if (record == null || record.Detail == null || !Enum.IsDefined(typeof(RecordCategory), record.Category))
                    return Fail("本局记录条目无效", out issue);
            }
            return true;
        }

        private static bool ValidateIds(List<string> ids, string label, Func<string, bool> exists, out string issue)
        {
            issue = null;
            if (ids == null) return Fail(label + "字段缺失", out issue);
            var seen = new HashSet<string>();
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) return Fail(label + " ID 缺失或重复", out issue);
                if (!exists(id)) return Fail(label + "引用不存在：" + id, out issue);
            }
            return true;
        }

        private static bool Fail(string message, out string issue)
        {
            issue = message;
            return false;
        }
    }
}
