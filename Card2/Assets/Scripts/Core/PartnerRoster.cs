using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>单个伙伴的运行时状态。</summary>
    public class PartnerState
    {
        public PartnerDef Def;
        public int CurrentHp;
        public bool IsRecruited;
        public bool IsAlive => CurrentHp > 0;
        public bool IsInActiveTeam; // 是否在上阵队伍中
        public int Loyalty = 60;    // 初始 60
        public int Disease;
        public int Fatigue;

        public int EffectiveMaxHp => System.Math.Max(1, Def.MaxHp - Disease * CombatStatus.DiseaseMaxHpPenalty);

        public PartnerState(PartnerDef def)
        {
            Def = def;
            CurrentHp = def.MaxHp;
        }

        public CombatUnit ToCombatUnit()
        {
            return new CombatUnit(Def.Id, Def.DisplayName, Def.MaxHp, Def.CommandDamage) {
                CurrentHp = CurrentHp,
                Disease = Disease,
                Fatigue = Fatigue
            };
        }
    }

    /// <summary>
    /// 伙伴队伍运行时状态管理（A2-15）。
    /// 管理招募、上阵/后备、生命/疾病/疲劳/忠诚度。
    /// </summary>
    public static class PartnerRoster
    {
        public static readonly List<PartnerState> All = new List<PartnerState>();
        public static IReadOnlyList<PartnerState> ActiveTeamMembers => _activeTeam;
        public static int ActiveCount => _activeTeam.Count;

        private static readonly List<PartnerState> _activeTeam = new List<PartnerState>();
        private static readonly Dictionary<string, PartnerState> _byId = new Dictionary<string, PartnerState>();

        static PartnerRoster()
        {
            AddDef(new PartnerDef("P01", "阿德里安", "防护", "坚守", 42, 5,
                "战斗开始时主角获得 4 护甲", "C12", "E04"));
            AddDef(new PartnerDef("P02", "米蕾", "后勤", "医师", 30, 2,
                "首次打出治疗牌额外恢复 2 生命", "C34", "E11"));
            AddDef(new PartnerDef("P03", "诺克斯", "控制", "斥候", 34, 5,
                "选择地图节点时显示下一层所有节点类型", "C17", "E02"));
            AddDef(new PartnerDef("P04", "莉薇", "后勤", "管事", 36, 3,
                "战斗胜利后额外获得 1 粮食", "C35", "E05"));
            AddDef(new PartnerDef("P05", "艾达", "外交", "交涉", 30, 2,
                "事件获得声望时额外 +2", "C21", "E08"));
            AddDef(new PartnerDef("P06", "约恩", "输出", "猎人", 35, 6,
                "首次由伙伴造成的指令伤害额外 +2", "C27", "E20"));
            AddDef(new PartnerDef("P07", "赛尔", "控制", "学者", 28, 3,
                "首次打出策略牌额外抽 1 张", "C23", "E12"));
            AddDef(new PartnerDef("P08", "布蕾", "防护", "斗士", 38, 4,
                "首次有伙伴生命低于 50% 时全体伙伴获 3 护甲", "C26", "E18"));
        }

        private static void AddDef(PartnerDef def)
        {
            var state = new PartnerState(def);
            All.Add(state);
            _byId[def.Id] = state;
        }

        public static PartnerState Find(string id)
        {
            _byId.TryGetValue(id, out var s);
            return s;
        }

        /// <summary>招募伙伴（加入后备队，不自动上阵）。</summary>
        public static bool Recruit(string id)
        {
            var p = Find(id);
            if (p == null || p.IsRecruited) return false;
            p.IsRecruited = true;
            return true;
        }

        /// <summary>将伙伴设为上阵。主战队伍不能超过 MaxPartySize-1（主角占一位）。</summary>
        public static string SetActiveTeam(IReadOnlyList<string> partnerIds)
        {
            int maxPartners = GameStartParameters.MaxPartySize - 1;
            if (partnerIds.Count > maxPartners)
                return "最多上阵 " + maxPartners + " 名伙伴";

            foreach (var id in partnerIds)
            {
                var p = Find(id);
                if (p == null) return "不存在的伙伴 ID：" + id;
                if (!p.IsRecruited) return p.Def.DisplayName + " 尚未招募";
                if (!p.IsAlive) return p.Def.DisplayName + " 已阵亡";
            }

            // 清空旧上阵状态
            foreach (var p in _activeTeam) p.IsInActiveTeam = false;
            _activeTeam.Clear();

            foreach (var id in partnerIds)
            {
                var p = Find(id);
                p.IsInActiveTeam = true;
                _activeTeam.Add(p);
            }

            return null; // OK
        }

        /// <summary>将战斗结果同步回伙伴状态（HP/疾病/疲劳）。</summary>
        public static void SyncFromCombat(IReadOnlyList<CombatUnit> team)
        {
            foreach (var u in team)
            {
                if (u.IsPlayerCharacter) continue;
                var p = Find(u.Id);
                if (p == null) continue;
                p.CurrentHp = u.IsAlive ? u.CurrentHp : 0;
                p.Disease = u.Disease;
                p.Fatigue = u.Fatigue;
            }
        }

        /// <summary>构建当前上阵队伍的战斗单位列表（伙伴在前，旅人放第二位）。</summary>
        public static List<CombatUnit> BuildCombatTeam(CombatUnit player)
        {
            var team = new List<CombatUnit>();
            foreach (var p in _activeTeam)
            {
                if (!p.IsAlive) continue;
                team.Add(p.ToCombatUnit());
            }
            if (team.Count == 0) team.Add(player);
            else team.Insert(1, player); // 旅人固定在第二位
            return team;
        }

        /// <summary>招募测试用默认伙伴（P01 阿德里安），直接上阵。</summary>
        public static void InitTestRoster()
        {
            Clear();
            var p01 = Find("P01");
            if (p01 != null)
            {
                p01.IsRecruited = true;
                p01.CurrentHp = p01.Def.MaxHp;
                p01.Disease = 0;
                p01.Fatigue = 0;
                p01.Loyalty = 60;
                _activeTeam.Clear();
                p01.IsInActiveTeam = true;
                _activeTeam.Add(p01);
            }
        }

        public static void Clear()
        {
            _activeTeam.Clear();
            foreach (var p in All)
            {
                p.IsRecruited = false;
                p.IsInActiveTeam = false;
                p.CurrentHp = p.Def.MaxHp;
                p.Disease = 0;
                p.Fatigue = 0;
                p.Loyalty = 60;
            }
        }
    }
}
