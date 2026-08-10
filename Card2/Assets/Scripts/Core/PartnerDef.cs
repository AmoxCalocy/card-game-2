namespace OneJourney.Core
{
    /// <summary>
    /// 伙伴静态定义（对应《MVP 配置表》§4）。
    /// 含定位、特质、最大生命、指令伤害、被动效果描述、专属加入卡、招募事件。
    /// </summary>
    public class PartnerDef
    {
        public string Id;
        public string DisplayName;
        public string Role;       // 定位
        public string Trait;      // 特质
        public int MaxHp;
        public int CommandDamage;
        public string PassiveText; // 被动效果描述
        public string JoinCardId;  // 专属加入卡
        public string RecruitEventId; // 招募事件

        public PartnerDef(string id, string name, string role, string trait, int hp, int cmdDmg,
            string passive, string joinCard, string recruitEvent)
        {
            Id = id; DisplayName = name; Role = role; Trait = trait;
            MaxHp = hp; CommandDamage = cmdDmg;
            PassiveText = passive; JoinCardId = joinCard; RecruitEventId = recruitEvent;
        }
    }
}
