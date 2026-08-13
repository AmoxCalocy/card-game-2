using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>意图种类（A1-11）：至少覆盖攻击、防御、掠夺。</summary>
    public enum IntentKind
    {
        Attack = 0,    // 单体攻击
        AoeAttack = 1, // 全体攻击
        Defense = 2,   // 获得护甲
        Plunder = 3    // 攻击并施加掠夺
    }

    /// <summary>可执行的敌人意图（对应《MVP 配置表》§5）。</summary>
    public sealed class EnemyIntentExec
    {
        public string Name;
        public IntentKind Kind;
        public int Damage;
        public int ArmorGain;
        public int PlunderStacks;
        public int BleedStacks;    // 附带流血层数
        public int DiseaseStacks;  // 附带疾病层数
        public int Weight = 1;
        public bool TargetsPlayer; // C30 诱饵：意图被改为攻击主角

        public EnemyIntentExec(string name, IntentKind kind, int weight)
        {
            Name = name;
            Kind = kind;
            Weight = weight;
        }

        /// <summary>预计影响文本（玩家可见）。</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case IntentKind.Attack:
                case IntentKind.AoeAttack:
                    return Name + "（" + (Kind == IntentKind.AoeAttack ? "全体 " : "") + Damage + " 伤害）";
                case IntentKind.Defense:
                    return Name + "（获得 " + ArmorGain + " 护甲）";
                case IntentKind.Plunder:
                    return Name + "（" + Damage + " 伤害 + 掠夺 " + PlunderStacks + " 层）";
                default:
                    return Name;
            }
        }
    }

    /// <summary>
    /// 敌人单位：携带意图池，回合开始时按权重抽取下一意图（本局种子驱动，可复现）。
    /// </summary>
    public sealed class EnemyUnit : CombatUnit
    {
        public readonly List<EnemyIntentExec> Intents = new List<EnemyIntentExec>();

        /// <summary>下一回合将执行的意图（玩家回合可见）。</summary>
        public EnemyIntentExec CurrentIntent;

        public EnemyUnit(string id, string displayName, int maxHp)
            : base(id, displayName, maxHp)
        {
        }

        /// <summary>按权重抽取下一意图；意图池无效时置空并记录问题。</summary>
        public void RollIntent(GameRandom rng)
        {
            if (rng == null)
            {
                CurrentIntent = null;
                return;
            }

            var picked = rng.WeightedPick(Intents, i => i.Weight, out string issue);
            if (picked == null)
            {
                CurrentIntent = null;
                RunRecord.Log(RecordCategory.EnemyIntent, DisplayName + " 意图抽取失败：" + issue);
                return;
            }

            CurrentIntent = picked;
            RunRecord.Log(RecordCategory.EnemyIntent, DisplayName + " 意图：" + picked.Describe());
        }

        public override CombatUnit Clone()
        {
            var clone = new EnemyUnit(Id, DisplayName, MaxHp)
            {
                CurrentHp = CurrentHp,
                Armor = Armor,
                Bleed = Bleed,
                Disease = Disease,
                Fatigue = Fatigue,
                IsPlayerCharacter = IsPlayerCharacter,
                CommandDamage = CommandDamage,
                CurrentIntent = CurrentIntent
            };
            clone.Intents.AddRange(Intents);
            return clone;
        }

        /// <summary>按配置表 ID 创建敌人（EN01-EN10）；未知 ID 返回 null。</summary>
        public static EnemyUnit CreateById(string id)
        {
            switch (id)
            {
                case "EN01": return CreateBandit();
                case "EN02": return CreateHound();
                case "EN03": return CreateScavenger();
                case "EN04": return CreateHornBeast();
                case "EN05": return CreatePlainsBoss();
                case "EN06": return CreateSpider();
                case "EN07": return CreateFungusBeast();
                case "EN08": return CreateForestBandit();
                case "EN09": return CreateBoar();
                case "EN10": return CreateJungleBoss();
                default: return null;
            }
        }

        /// <summary>路匪（EN01，草原普通）：砍击 6 伤(50)、勒索 4 伤+1 掠夺(30)、架盾 6 甲(20)。</summary>
        public static EnemyUnit CreateBandit()
        {
            var e = new EnemyUnit("EN01", "路匪", 28);
            e.Intents.Add(new EnemyIntentExec("砍击", IntentKind.Attack, 50) { Damage = 6 });
            e.Intents.Add(new EnemyIntentExec("勒索", IntentKind.Plunder, 30) { Damage = 4, PlunderStacks = 1 });
            e.Intents.Add(new EnemyIntentExec("架盾", IntentKind.Defense, 20) { ArmorGain = 6 });
            return e;
        }

        /// <summary>野犬（EN02，草原普通）：撕咬 4 伤(60)、飞扑 6 伤(40)。</summary>
        public static EnemyUnit CreateHound()
        {
            var e = new EnemyUnit("EN02", "野犬", 22);
            e.Intents.Add(new EnemyIntentExec("撕咬", IntentKind.Attack, 60) { Damage = 4 });
            e.Intents.Add(new EnemyIntentExec("飞扑", IntentKind.Attack, 40) { Damage = 6 });
            return e;
        }

        /// <summary>旱地掠手（EN03，草原精英）：劈砍 7 伤(45)、断筋斩 8 伤(35)、架势 7 甲(20)。</summary>
        public static EnemyUnit CreateScavenger()
        {
            var e = new EnemyUnit("EN03", "旱地掠手", 34);
            e.Intents.Add(new EnemyIntentExec("劈砍", IntentKind.Attack, 45) { Damage = 7 });
            e.Intents.Add(new EnemyIntentExec("断筋斩", IntentKind.Attack, 35) { Damage = 8 });
            e.Intents.Add(new EnemyIntentExec("架势", IntentKind.Defense, 20) { ArmorGain = 7 });
            return e;
        }

        /// <summary>角兽（EN04，草原普通）：冲撞 10 伤(50)、践踏全体 5 伤(30)、蛰伏 6 甲(20)。</summary>
        public static EnemyUnit CreateHornBeast()
        {
            var e = new EnemyUnit("EN04", "角兽", 38);
            e.Intents.Add(new EnemyIntentExec("冲撞", IntentKind.Attack, 50) { Damage = 10 });
            e.Intents.Add(new EnemyIntentExec("践踏", IntentKind.AoeAttack, 30) { Damage = 5 });
            e.Intents.Add(new EnemyIntentExec("蛰伏", IntentKind.Defense, 20) { ArmorGain = 6 });
            return e;
        }

        /// <summary>草原劫首（EN05，草原首领）：重斩 10(40)、掠夺突袭 8+1掠夺(30)、断筋斩 9(20)、号令 10 甲(10)。</summary>
        public static EnemyUnit CreatePlainsBoss()
        {
            var e = new EnemyUnit("EN05", "草原劫首", 72);
            e.Intents.Add(new EnemyIntentExec("重斩", IntentKind.Attack, 40) { Damage = 10 });
            e.Intents.Add(new EnemyIntentExec("掠夺突袭", IntentKind.Plunder, 30) { Damage = 8, PlunderStacks = 1 });
            e.Intents.Add(new EnemyIntentExec("断筋斩", IntentKind.Attack, 20) { Damage = 9 });
            e.Intents.Add(new EnemyIntentExec("号令", IntentKind.Defense, 10) { ArmorGain = 10 });
            return e;
        }

        /// <summary>毒丝蛛（EN06，密林普通）：啃咬 4 伤+1 流血(60)、缠网 3 伤(40)。</summary>
        public static EnemyUnit CreateSpider()
        {
            var e = new EnemyUnit("EN06", "毒丝蛛", 24);
            e.Intents.Add(new EnemyIntentExec("啃咬", IntentKind.Attack, 60) { Damage = 4, BleedStacks = 1 });
            e.Intents.Add(new EnemyIntentExec("缠网", IntentKind.Attack, 40) { Damage = 3 });
            return e;
        }

        /// <summary>菌疫兽（EN07，密林普通）：撞击 6(45)、孢子全体 3+1 疾病(35)、菌壳 5 甲(20)。</summary>
        public static EnemyUnit CreateFungusBeast()
        {
            var e = new EnemyUnit("EN07", "菌疫兽", 32);
            e.Intents.Add(new EnemyIntentExec("撞击", IntentKind.Attack, 45) { Damage = 6 });
            e.Intents.Add(new EnemyIntentExec("孢子", IntentKind.AoeAttack, 35) { Damage = 3, DiseaseStacks = 1 });
            e.Intents.Add(new EnemyIntentExec("菌壳", IntentKind.Defense, 20) { ArmorGain = 5 });
            return e;
        }

        /// <summary>林间伏匪（EN08，密林精英）：箭射 8(45)、洗劫 5+2 掠夺(35)、伏守 8 甲(20)。</summary>
        public static EnemyUnit CreateForestBandit()
        {
            var e = new EnemyUnit("EN08", "林间伏匪", 36);
            e.Intents.Add(new EnemyIntentExec("箭射", IntentKind.Attack, 45) { Damage = 8 });
            e.Intents.Add(new EnemyIntentExec("洗劫", IntentKind.Plunder, 35) { Damage = 5, PlunderStacks = 2 });
            e.Intents.Add(new EnemyIntentExec("伏守", IntentKind.Defense, 20) { ArmorGain = 8 });
            return e;
        }

        /// <summary>古牙野猪（EN09，密林普通）：破骨冲撞 10(50)、横扫全体 5(30)、蓄势 7 甲(20)。</summary>
        public static EnemyUnit CreateBoar()
        {
            var e = new EnemyUnit("EN09", "古牙野猪", 44);
            e.Intents.Add(new EnemyIntentExec("破骨冲撞", IntentKind.Attack, 50) { Damage = 10 });
            e.Intents.Add(new EnemyIntentExec("横扫", IntentKind.AoeAttack, 30) { Damage = 5 });
            e.Intents.Add(new EnemyIntentExec("蓄势", IntentKind.Defense, 20) { ArmorGain = 7 });
            return e;
        }

        /// <summary>密林守望者（EN10，密林首领）：树根重击 9(35)、孢子风暴全体 4+1 疾病(30)、缠枝 6(20)、树皮 12 甲(15)。</summary>
        public static EnemyUnit CreateJungleBoss()
        {
            var e = new EnemyUnit("EN10", "密林守望者", 80);
            e.Intents.Add(new EnemyIntentExec("树根重击", IntentKind.Attack, 35) { Damage = 9 });
            e.Intents.Add(new EnemyIntentExec("孢子风暴", IntentKind.AoeAttack, 30) { Damage = 4, DiseaseStacks = 1 });
            e.Intents.Add(new EnemyIntentExec("缠枝", IntentKind.Attack, 20) { Damage = 6 });
            e.Intents.Add(new EnemyIntentExec("树皮", IntentKind.Defense, 15) { ArmorGain = 12 });
            return e;
        }
    }
}
