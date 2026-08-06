using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>遭遇配置（实施计划 A1-12，对应《MVP 配置表》§5 / §9）。</summary>
    public static class EncounterConfig
    {
        public enum EncounterType
        {
            Normal = 0,
            Elite = 1,
            Boss = 2
        }

        public struct Encounter
        {
            public string Label;
            public string Region;
            public EncounterType Type;
            public EnemyUnit[] Enemies;
        }

        public static readonly Encounter[] All =
        {
            new Encounter
            {
                Label = "草原普通（路匪+野犬）",
                Region = "草原",
                Type = EncounterType.Normal,
                Enemies = new[] { EnemyUnit.CreateBandit(), EnemyUnit.CreateHound() }
            },
            new Encounter
            {
                Label = "草原精英（旱地掠手）",
                Region = "草原",
                Type = EncounterType.Elite,
                Enemies = new[] { EnemyUnit.CreateScavenger() }
            },
            new Encounter
            {
                Label = "草原首领（草原劫首）",
                Region = "草原",
                Type = EncounterType.Boss,
                Enemies = new[] { EnemyUnit.CreatePlainsBoss() }
            },
            new Encounter
            {
                Label = "角兽（草原普通）",
                Region = "草原",
                Type = EncounterType.Normal,
                Enemies = new[] { EnemyUnit.CreateHornBeast() }
            },
            new Encounter
            {
                Label = "毒丝蛛（密林普通）",
                Region = "密林",
                Type = EncounterType.Normal,
                Enemies = new[] { EnemyUnit.CreateSpider() }
            },
            new Encounter
            {
                Label = "菌疫兽（密林普通）",
                Region = "密林",
                Type = EncounterType.Normal,
                Enemies = new[] { EnemyUnit.CreateFungusBeast() }
            },
            new Encounter
            {
                Label = "林间伏匪（密林精英）",
                Region = "密林",
                Type = EncounterType.Elite,
                Enemies = new[] { EnemyUnit.CreateForestBandit() }
            },
            new Encounter
            {
                Label = "古牙野猪（密林普通）",
                Region = "密林",
                Type = EncounterType.Normal,
                Enemies = new[] { EnemyUnit.CreateBoar() }
            },
            new Encounter
            {
                Label = "密林首领（密林守望者）",
                Region = "密林",
                Type = EncounterType.Boss,
                Enemies = new[] { EnemyUnit.CreateJungleBoss() }
            }
        };
    }
}
