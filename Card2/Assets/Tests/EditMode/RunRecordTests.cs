using NUnit.Framework;
using OneJourney.Core;

namespace OneJourney.Tests.EditMode
{
    public class RunRecordTests
    {
        [TearDown]
        public void TearDown()
        {
            RunRecord.Clear();
            RunSession.Reset();
        }

        [Test]
        public void Log_SingleEntry_IncrementsCount()
        {
            RunRecord.Log(RecordCategory.Draw, "抽到 C01");

            Assert.AreEqual(1, RunRecord.Count);
            Assert.AreEqual(RecordCategory.Draw, RunRecord.Entries[0].Category);
            Assert.AreEqual("抽到 C01", RunRecord.Entries[0].Detail);
            Assert.AreEqual(0, RunRecord.Entries[0].Index);
        }

        [Test]
        public void Log_MultipleEntries_MaintainsOrder()
        {
            RunRecord.Log(RecordCategory.Draw, "第一张");
            RunRecord.Log(RecordCategory.EnemyIntent, "攻击意图");
            RunRecord.Log(RecordCategory.MapBranch, "选择上层");

            Assert.AreEqual(3, RunRecord.Count);
            Assert.AreEqual(0, RunRecord.Entries[0].Index);
            Assert.AreEqual(1, RunRecord.Entries[1].Index);
            Assert.AreEqual(2, RunRecord.Entries[2].Index);
            Assert.AreEqual("第一张", RunRecord.Entries[0].Detail);
            Assert.AreEqual("攻击意图", RunRecord.Entries[1].Detail);
            Assert.AreEqual("选择上层", RunRecord.Entries[2].Detail);
        }

        [Test]
        public void Clear_RemovesAllEntries()
        {
            RunRecord.Log(RecordCategory.Draw, "x");
            RunRecord.Log(RecordCategory.Draw, "y");

            RunRecord.Clear();

            Assert.AreEqual(0, RunRecord.Count);
        }

        [Test]
        public void OverMax_LimitsSize()
        {
            // 写入 250 条，超过上限 200
            for (int i = 0; i < 250; i++)
            {
                RunRecord.Log(RecordCategory.General, "条目 " + i);
            }

            Assert.LessOrEqual(RunRecord.Count, 200);
            Assert.AreEqual(0, RunRecord.Entries[0].Index, "截断后应从 0 开始重新编号");
        }

        [Test]
        public void CategoryName_ReturnsCorrectChinese()
        {
            Assert.AreEqual("抽牌", RunRecordEntry.CategoryName(RecordCategory.Draw));
            Assert.AreEqual("敌人意图", RunRecordEntry.CategoryName(RecordCategory.EnemyIntent));
            Assert.AreEqual("地图分支", RunRecordEntry.CategoryName(RecordCategory.MapBranch));
            Assert.AreEqual("事件选项", RunRecordEntry.CategoryName(RecordCategory.EventChoice));
            Assert.AreEqual("奖励选择", RunRecordEntry.CategoryName(RecordCategory.RewardChoice));
        }
    }
}
