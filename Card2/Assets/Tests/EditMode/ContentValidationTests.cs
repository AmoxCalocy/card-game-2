using System.Collections.Generic;
using NUnit.Framework;
using OneJourney.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneJourney.Tests.EditMode
{
    /// <summary>内容校验器测试（实施计划 A0-4 验证条目）。</summary>
    public class ContentValidationTests
    {
        [TearDown]
        public void TearDown()
        {
            ContentRegistry.Clear();
            RunSession.Reset();
        }

        [Test]
        public void ValidContent_NoIssues()
        {
            var contents = new ContentBase[] { ValidCard(), ValidEvent() };

            var issues = ContentValidator.Validate(contents);

            Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
        }

        [Test]
        public void MissingRequiredFields_BlockedWithFieldAndReason()
        {
            var card = ValidCard();
            card.displayName = string.Empty;

            var issues = ContentValidator.Validate(new ContentBase[] { card });

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("displayName", issues[0].Field);
            Assert.AreEqual("C99", issues[0].ContentId);
            StringAssert.Contains("缺少必填", issues[0].Reason);
        }

        [Test]
        public void MissingId_Blocked()
        {
            var card = ValidCard();
            card.id = string.Empty;

            var issues = ContentValidator.Validate(new ContentBase[] { card });

            Assert.IsTrue(issues.Exists(i => i.Field == "id" && i.ContentId == "(未命名)"));
        }

        [Test]
        public void ReferenceToMissingId_Blocked()
        {
            var evt = ValidEvent();
            evt.options[0].partnerId = "P99";

            var issues = ContentValidator.Validate(new ContentBase[] { evt });

            Assert.IsTrue(issues.Exists(i => i.Field == "options[0].partnerId" && i.Reason.Contains("P99")));
        }

        [Test]
        public void OutOfRangeCost_Blocked()
        {
            var card = ValidCard();
            card.cost = 6;

            var issues = ContentValidator.Validate(new ContentBase[] { card });

            Assert.IsTrue(issues.Exists(i => i.Field == "cost" && i.Reason.Contains("0-4")));
        }

        [Test]
        public void DuplicateId_Blocked()
        {
            var a = ValidCard();
            var b = ValidCard();
            b.displayName = "重复卡";

            var issues = ContentValidator.Validate(new ContentBase[] { a, b });

            Assert.IsTrue(issues.Exists(i => i.Field == "id" && i.Reason.Contains("重复")));
        }

        [Test]
        public void EventWithSingleOption_Blocked()
        {
            var evt = ValidEvent();
            evt.options.RemoveAt(1);

            var issues = ContentValidator.Validate(new ContentBase[] { evt });

            Assert.IsTrue(issues.Exists(i => i.Field == "options" && i.Reason.Contains("至少 2")));
        }

        [Test]
        public void EnemyWithoutIntents_Blocked()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.id = "EN99";
            enemy.displayName = "坏敌人";
            enemy.description = "缺少意图";
            enemy.maxHp = 10;

            var issues = ContentValidator.Validate(new ContentBase[] { enemy });

            Assert.IsTrue(issues.Exists(i => i.Field == "intents"));
        }

        [Test]
        public void ZeroWeightIntent_Blocked()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.id = "EN99";
            enemy.displayName = "坏敌人";
            enemy.description = "权重为零";
            enemy.maxHp = 10;
            enemy.intents.Add(new EnemyIntent { name = "攻击", weight = 0, effectText = "6 伤害" });

            var issues = ContentValidator.Validate(new ContentBase[] { enemy });

            Assert.IsTrue(issues.Exists(i => i.Field == "intents[0].weight" && i.Reason.Contains("正")));
        }

        [Test]
        public void FixedContent_ValidationPasses()
        {
            // 先坏后修：修复后必须通过校验（不残留问题）
            var card = ValidCard();
            card.displayName = string.Empty;
            ContentValidator.Validate(new ContentBase[] { card });

            card.displayName = "修复后的卡";
            var issues = ContentValidator.Validate(new ContentBase[] { card });

            Assert.AreEqual(0, issues.Count);
        }

        [Test]
        public void BlockingIssues_PreventStartNewGame()
        {
            var bad = ValidCard();
            bad.displayName = string.Empty;
            ContentRegistry.Register(bad);
            LogAssert.Expect(LogType.Error, "[内容校验] CardData[C99] 字段[displayName]：缺少必填显示名称");
            ContentRegistry.ValidateAll();
            RunSession.Reset();

            RunSession.StartNewGame();

            Assert.IsTrue(ContentRegistry.HasBlockingIssues);
            Assert.AreEqual(GameState.MainMenu, RunSession.CurrentState, "存在校验问题时不得进入新局");
            Assert.AreEqual("内容校验", RunSession.LastResolution.Value.Source);
            StringAssert.Contains("displayName", RunSession.LastResolution.Value.Result);
        }

        [Test]
        public void BlockingIssues_FixedThenStartNewGame_Succeeds()
        {
            var bad = ValidCard();
            bad.displayName = string.Empty;
            ContentRegistry.Register(bad);
            LogAssert.Expect(LogType.Error, "[内容校验] CardData[C99] 字段[displayName]：缺少必填显示名称");
            ContentRegistry.ValidateAll();
            RunSession.Reset();

            // 修复内容后重新校验：必须能正常启动新局
            ContentRegistry.Clear();
            ContentRegistry.Register(ValidCard());
            ContentRegistry.ValidateAll();

            RunSession.StartNewGame();

            Assert.IsFalse(ContentRegistry.HasBlockingIssues);
            Assert.AreEqual(GameState.Map, RunSession.CurrentState, "修复内容后应可正常启动新局");
        }

        private static CardData ValidCard()
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.id = "C99";
            card.displayName = "测试卡";
            card.description = "测试用卡牌";
            card.cost = 1;
            card.targetType = TargetType.SingleEnemy;
            card.effectText = "造成 6 伤害";
            card.rarity = CardRarity.Common;
            return card;
        }

        private static EventData ValidEvent()
        {
            var evt = ScriptableObject.CreateInstance<EventData>();
            evt.id = "E99";
            evt.displayName = "测试事件";
            evt.description = "测试用事件";
            evt.region = ContentRegion.Plains;
            evt.category = EventCategory.Encounter;
            evt.options.Add(new EventOption { label = "支援", conditionText = "粮食 >= 3", resultText = "声望 +8" });
            evt.options.Add(new EventOption { label = "离开", resultText = "风险 +1" });
            return evt;
        }
    }
}
