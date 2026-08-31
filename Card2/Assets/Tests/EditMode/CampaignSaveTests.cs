using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    /// <summary>A3-25 安全存档：安全点、完整状态往返、主备份恢复与坏档拒绝。</summary>
    public class CampaignSaveTests
    {
        private string _saveDirectory;

        [SetUp]
        public void SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "OneJourneySaveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDirectory);
            CampaignSaveService.SetStorageDirectoryForTests(_saveDirectory);
            RunSession.Reset();
            RunSession.Relics.Clear();
            RunSession.EventFlags.Clear();
            RunSession.BuiltBuildings.Clear();
            RunSession.SetBossDefeatedForTest(false);
            RunSession.StartNewGame(24680);
            ContentRegistry.Clear();
            Assert.IsTrue(CampaignSaveService.HasValidSave, CampaignSaveService.StatusMessage);
        }

        [TearDown]
        public void TearDown()
        {
            CombatManager.End();
            RewardResolver.Clear();
            RunSession.Reset();
            CampaignSaveService.DeleteActiveSave();
            CampaignSaveService.ResetStorageDirectoryForTests();
            ContentRegistry.Clear();
            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
        }

        [Test]
        public void MapRoundTrip_RestoresCampaignAndRandomState()
        {
            RunSession.SetFoodForTest(9);
            RunSession.SetWealthForTest(77);
            RunSession.SetReputationForTest(18);
            RunSession.SetMaterialsForTest(7);
            RunSession.SetRiskForTest(4);
            RunSession.SetPlayerFatigueForTest(2);
            RunSession.SetPlayerDiseaseForTest(1);

            Assert.IsTrue(RunSession.CampaignDeck.AddCard("C02"));
            Assert.IsTrue(RunSession.CampaignDeck.UpgradeCard("C17"));
            Assert.IsTrue(PartnerRoster.Recruit("P02"));
            Assert.IsTrue(PartnerRoster.Recruit("P03"));
            var p02 = PartnerRoster.Find("P02");
            p02.Disease = 1;
            p02.Fatigue = 2;
            p02.CurrentHp = 24;
            p02.Loyalty = 73;
            Assert.IsNull(PartnerRoster.SetActiveTeam(new[] { "P03", "P02" }));
            RunSession.AddRelicForTest("R03");
            RunSession.BuiltBuildings.Add("B01");
            RunSession.EventFlags.Add("E01");
            RunSession.SetBossDefeatedForTest(true);
            RunRecord.Log(RecordCategory.General, "存档往返测试记录");

            Assert.IsTrue(CampaignSaveService.TrySave(SaveCheckpointKind.Map, out string saveMessage), saveMessage);
            int expectedNextRandom = RunSession.Random.Next(1000000);

            RunSession.Reset();
            Assert.IsTrue(RunSession.TryContinue(out string loadMessage), loadMessage);

            Assert.AreEqual(GameState.Map, RunSession.CurrentState);
            Assert.AreEqual(24680, RunSession.Seed);
            Assert.AreEqual(9, RunSession.Food);
            Assert.AreEqual(77, RunSession.Wealth);
            Assert.AreEqual(18, RunSession.Reputation);
            Assert.AreEqual(7, RunSession.Materials);
            Assert.AreEqual(4, RunSession.Risk);
            Assert.AreEqual(2, RunSession.PlayerFatigue);
            Assert.AreEqual(1, RunSession.PlayerDisease);
            CollectionAssert.AreEqual(new[] { "P03", "P02" }, ActivePartnerIds());
            Assert.AreEqual(24, PartnerRoster.Find("P02").CurrentHp);
            Assert.AreEqual(73, PartnerRoster.Find("P02").Loyalty);
            Assert.IsTrue(RunSession.CampaignDeck.Cards.Contains("C02"));
            Assert.IsTrue(RunSession.CampaignDeck.UpgradedCards.Contains("C17"));
            Assert.IsTrue(RunSession.HasRelic("R03"));
            Assert.IsTrue(RunSession.HasBuilding("B01"));
            Assert.IsTrue(RunSession.EventFlags.Contains("E01"));
            Assert.IsTrue(RunSession.GrasslandBossDefeated);
            Assert.AreEqual(expectedNextRandom, RunSession.Random.Next(1000000), "读档后随机序列继续而非重置");
            Assert.GreaterOrEqual(RunRecord.Count, 2, "保留原本局记录并追加继续游戏记录");
        }

        [Test]
        public void NodeEntry_Event_ContinueRestartsSameEvent()
        {
            int nodeIndex = FindReachable(NodeType.Event);
            var node = RegionMap.Nodes[nodeIndex];
            GameRandom.TryCreate(RunSession.Random.CaptureState(), out GameRandom previewRandom, out _);
            string expectedEventId = node.EventPoolIds[previewRandom.Next(node.EventPoolIds.Length)];

            RunSession.TryMoveToNode(nodeIndex);
            RunSession.Reset();

            Assert.IsTrue(RunSession.TryContinue(out string message), message);
            Assert.AreEqual(GameState.Event, RunSession.CurrentState);
            Assert.IsNotNull(RunSession.CurrentEvent);
            Assert.AreEqual(expectedEventId, RunSession.CurrentEvent.Id);
        }

        [Test]
        public void NodeEntry_Combat_ContinueRestartsCombatFromBeginning()
        {
            int nodeIndex = FindReachable(NodeType.Combat);
            RunSession.TryMoveToNode(nodeIndex);
            RunSession.Reset();

            Assert.IsTrue(RunSession.TryContinue(out string message), message);
            Assert.AreEqual(GameState.Combat, RunSession.CurrentState);
            Assert.IsTrue(CombatManager.IsActive);
            Assert.AreEqual(1, CombatManager.TurnNumber);
        }

        [Test]
        public void NodeEntry_Camp_FirstContinueAppliesEntryOnce_SecondContinueUsesCampCheckpoint()
        {
            RunSession.SetRiskForTest(5);
            int nodeIndex = FindReachable(NodeType.Camp);
            RunSession.TryMoveToNode(nodeIndex);
            Assert.AreEqual(6, RunSession.Risk, "节点入口存档前已完成移动风险结算");
            RunSession.Reset();

            Assert.IsTrue(RunSession.TryContinue(out string firstMessage), firstMessage);
            Assert.AreEqual(GameState.Camp, RunSession.CurrentState);
            Assert.AreEqual(4, RunSession.Risk, "首次继续结算营地风险 -2");

            RunSession.Reset();
            Assert.IsTrue(RunSession.TryContinue(out string secondMessage), secondMessage);
            Assert.AreEqual(GameState.Camp, RunSession.CurrentState);
            Assert.AreEqual(4, RunSession.Risk, "营地检查点继续时不重复结算入口效果");
        }

        [Test]
        public void EventCompletion_AutosavesMapAndEventFlag()
        {
            int nodeIndex = FindReachable(NodeType.Event);
            RunSession.TryMoveToNode(nodeIndex);
            Assert.IsTrue(RunSession.StartEvent("E01"));
            RunSession.ChooseEventOption(2); // 离开：风险 +1，立即完成
            int expectedRisk = RunSession.Risk;
            Assert.AreEqual(GameState.Map, RunSession.CurrentState);

            RunSession.Reset();
            Assert.IsTrue(RunSession.TryContinue(out string message), message);
            Assert.AreEqual(GameState.Map, RunSession.CurrentState);
            Assert.AreEqual(expectedRisk, RunSession.Risk);
            Assert.IsTrue(RunSession.EventFlags.Contains("E01"));
        }

        [Test]
        public void CampOperation_AutosavesChangedStatus()
        {
            int nodeIndex = FindReachable(NodeType.Camp);
            RunSession.TryMoveToNode(nodeIndex);
            Assert.IsTrue(GameFlow.TryTransition(GameState.Camp, "测试进入营地"));
            RunSession.EnterCampNode();
            RunSession.SetPlayerFatigueForTest(2);
            RunSession.CampfireRest("PLAYER");

            RunSession.Reset();
            Assert.IsTrue(RunSession.TryContinue(out string message), message);
            Assert.AreEqual(GameState.Camp, RunSession.CurrentState);
            Assert.AreEqual(1, RunSession.PlayerFatigue);
        }

        [Test]
        public void RewardCompletion_AutosavesOnlyAfterReturningToMap()
        {
            int nodeIndex = FindReachable(NodeType.Combat);
            RunSession.TryMoveToNode(nodeIndex);
            Assert.IsTrue(RunSession.StartNodeCombat(RegionMap.Nodes[nodeIndex]));
            foreach (var enemy in CombatManager.EnemyTeam) enemy.TakeDamage(enemy.CurrentHp + enemy.Armor);
            CombatManager.CheckEndCondition();
            Assert.AreEqual(GameState.Reward, RunSession.CurrentState);
            RewardResolver.SkipReward();
            int expectedFood = RunSession.Food;
            int expectedWealth = RunSession.Wealth;

            Assert.IsTrue(RunSession.CompleteRewardAndReturnToMap(out string saveMessage), saveMessage);
            Assert.AreEqual(GameState.Map, RunSession.CurrentState);
            Assert.IsFalse(CombatManager.IsActive);

            RunSession.Reset();
            Assert.IsTrue(RunSession.TryContinue(out string loadMessage), loadMessage);
            Assert.AreEqual(GameState.Map, RunSession.CurrentState);
            Assert.AreEqual(expectedFood, RunSession.Food);
            Assert.AreEqual(expectedWealth, RunSession.Wealth);
            Assert.IsFalse(CombatManager.IsActive);
        }

        [Test]
        public void CombatInProgress_ManualSaveRejectedAndPreviousCheckpointUnchanged()
        {
            int nodeIndex = FindReachable(NodeType.Combat);
            RunSession.TryMoveToNode(nodeIndex);
            string checkpointJson = File.ReadAllText(CampaignSaveService.PrimaryPath);
            Assert.IsTrue(RunSession.StartNodeCombat(RegionMap.Nodes[nodeIndex]));

            Assert.IsFalse(CampaignSaveService.TrySave(SaveCheckpointKind.Map, out string message));
            StringAssert.Contains("战斗", message);
            Assert.AreEqual(checkpointJson, File.ReadAllText(CampaignSaveService.PrimaryPath));
        }

        [Test]
        public void CorruptPrimary_ValidBackup_LoadsBackupAndRecoversPrimary()
        {
            RunSession.SetFoodForTest(11);
            Assert.IsTrue(CampaignSaveService.TrySave(SaveCheckpointKind.Map, out _));
            Assert.IsTrue(File.Exists(CampaignSaveService.BackupPath), "第二次保存生成备份");
            File.WriteAllText(CampaignSaveService.PrimaryPath, "{ truncated", System.Text.Encoding.UTF8);
            RunSession.Reset();

            Assert.IsTrue(RunSession.TryContinue(out string message), message);
            StringAssert.Contains("备份", message);
            Assert.AreEqual(GameStartParameters.StartFood, RunSession.Food, "回退到第一次保存的状态");
            Assert.IsTrue(File.Exists(CampaignSaveService.PrimaryPath), "备份恢复为主存档");
        }

        [Test]
        public void CorruptPrimaryAndBackup_RejectsWithoutPartialState()
        {
            File.WriteAllText(CampaignSaveService.PrimaryPath, "{ truncated", System.Text.Encoding.UTF8);
            File.WriteAllText(CampaignSaveService.BackupPath, "{ also truncated", System.Text.Encoding.UTF8);
            RunSession.Reset();

            Assert.IsFalse(RunSession.TryContinue(out string message));
            StringAssert.Contains("存档不可用", message);
            Assert.AreEqual(GameState.MainMenu, RunSession.CurrentState);
            Assert.AreEqual(0, RunSession.Seed);
        }

        [Test]
        public void MissingCriticalField_RejectsWithClearMessage()
        {
            File.Delete(CampaignSaveService.PrimaryPath);
            File.WriteAllText(CampaignSaveService.PrimaryPath,
                "{\"SchemaVersion\":1}", System.Text.Encoding.UTF8);
            RunSession.Reset();

            Assert.IsFalse(RunSession.TryContinue(out string message));
            StringAssert.Contains("缺少关键字段", message);
            Assert.AreEqual(GameState.MainMenu, RunSession.CurrentState);
        }

        [Test]
        public void OldSchema_RejectsWithVersionMessage()
        {
            File.Delete(CampaignSaveService.PrimaryPath);
            File.WriteAllText(CampaignSaveService.PrimaryPath,
                "{\"SchemaVersion\":0,\"Payload\":\"{}\",\"IntegrityHash\":\"x\"}",
                System.Text.Encoding.UTF8);
            RunSession.Reset();

            Assert.IsFalse(RunSession.TryContinue(out string message));
            StringAssert.Contains("版本", message);
            Assert.AreEqual(GameState.MainMenu, RunSession.CurrentState);
        }

        [Test]
        public void Settlement_DeletesActiveSave()
        {
            int nodeIndex = FindReachable(NodeType.Combat);
            RunSession.TryMoveToNode(nodeIndex);
            Assert.IsTrue(RunSession.StartNodeCombat(RegionMap.Nodes[nodeIndex]));
            var player = CombatManager.PlayerCharacter();
            player.TakeDamage(player.CurrentHp + player.Armor + 1);
            CombatManager.CheckEndCondition();
            Assert.AreEqual(GameState.Defeat, RunSession.CurrentState);

            RunSession.EnterSettlement(false, "主角阵亡");

            Assert.AreEqual(GameState.Settlement, RunSession.CurrentState);
            Assert.IsFalse(File.Exists(CampaignSaveService.PrimaryPath));
            Assert.IsFalse(File.Exists(CampaignSaveService.BackupPath));
            Assert.IsFalse(CampaignSaveService.HasValidSave);
        }

        private static int FindReachable(NodeType type)
        {
            foreach (int index in RegionMap.ReachableNext())
            {
                if (RegionMap.Nodes[index].Type == type) return index;
            }

            Assert.Fail("当前层找不到节点类型：" + type);
            return -1;
        }

        private static List<string> ActivePartnerIds()
        {
            var result = new List<string>();
            foreach (var partner in PartnerRoster.ActiveTeamMembers) result.Add(partner.Def.Id);
            return result;
        }
    }
}
