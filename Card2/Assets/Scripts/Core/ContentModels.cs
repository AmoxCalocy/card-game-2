using System.Collections.Generic;
using UnityEngine;

namespace OneJourney.Core
{
    // ---- 公共枚举（对应《MVP 配置表》取值范围）----

    public enum ContentRegion
    {
        None = 0,
        Plains = 1,
        Jungle = 2
    }

    public enum TargetType
    {
        None = 0,
        SingleEnemy = 1,
        AllEnemies = 2,
        Self = 3,
        SingleAlly = 4,
        AllAllies = 5
    }

    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    public enum EnemyCategory
    {
        Normal = 0,
        Elite = 1,
        Boss = 2
    }

    public enum EventCategory
    {
        Encounter = 0,
        Disaster = 1,
        Social = 2
    }

    public enum RelicType
    {
        Passive = 0,
        OneShot = 1
    }

    public enum NodeType
    {
        Combat = 0,
        Event = 1,
        Camp = 2,
        Elite = 3,
        Boss = 4
    }

    /// <summary>内容数据基类：所有可配置内容（卡牌/伙伴/敌人/事件/遗物/节点/建筑）的必填基础字段。</summary>
    public abstract class ContentBase : ScriptableObject
    {
        [Header("必填")]
        public string id;
        public string displayName;
        [TextArea] public string description;
    }

    /// <summary>卡牌（配置表 3）：必填 id/displayName/description/effectText/cost/targetType。</summary>
    public class CardData : ContentBase
    {
        [Header("卡牌")]
        [Range(0, 4)] public int cost;
        public TargetType targetType;
        [TextArea] public string effectText;
        public CardRarity rarity;
        public string sourceText;
    }

    /// <summary>伙伴（配置表 4）：必填 id/displayName/description/role/maxHp/commandDamage/joinCardId。</summary>
    public class PartnerData : ContentBase
    {
        [Header("伙伴")]
        public string role;
        public string trait;
        [Range(1, 100)] public int maxHp = 1;
        [Range(0, 20)] public int commandDamage;
        public string joinCardId; // 专属加入卡，引用卡牌 ID
        [TextArea] public string passiveText;
    }

    /// <summary>敌人意图：显示即执行的下一回合效果。</summary>
    [System.Serializable]
    public class EnemyIntent
    {
        public string name;
        public int weight = 1;
        [TextArea] public string effectText;
    }

    /// <summary>敌人（配置表 5）：必填 id/displayName/description/region/category/maxHp/intents。</summary>
    public class EnemyData : ContentBase
    {
        [Header("敌人")]
        public ContentRegion region;
        public EnemyCategory category;
        [Range(1, 200)] public int maxHp = 1;
        public List<EnemyIntent> intents = new List<EnemyIntent>();
    }

    /// <summary>事件选项：每个选项至少包含标签与固定结果；引用字段按 ID 解析。</summary>
    [System.Serializable]
    public class EventOption
    {
        public string label;
        public string conditionText; // 无条件时留空
        [TextArea] public string resultText;
        public string[] enemyIds;    // 触发战斗的敌人（可空）
        public string partnerId;     // 招募伙伴（可空）
        public string cardId;        // 获得卡牌（可空）
        public string relicId;       // 获得遗物（可空）
    }

    /// <summary>事件（配置表 6）：必填 id/displayName/description/region/category，至少 2 个选项。</summary>
    public class EventData : ContentBase
    {
        [Header("事件")]
        public ContentRegion region;
        public EventCategory category;
        public List<EventOption> options = new List<EventOption>();
    }

    /// <summary>遗物（配置表 7）：必填 id/displayName/description/type/effectText。</summary>
    public class RelicData : ContentBase
    {
        [Header("遗物")]
        public RelicType type;
        [TextArea] public string effectText;
    }

    /// <summary>地图节点（配置表 9）：必填 id/displayName/description/region/nodeType。</summary>
    public class NodeData : ContentBase
    {
        [Header("节点")]
        public ContentRegion region;
        public NodeType nodeType;
        public string[] enemyPoolIds; // 战斗/精英/首领节点的敌人池（可空）
        public string[] eventPoolIds; // 事件节点的事件池（可空）
    }

    /// <summary>建筑（配置表 8）：必填 id/displayName/description/effectText；成本字段 0-999。</summary>
    public class BuildingData : ContentBase
    {
        [Header("建筑")]
        [Range(0, 999)] public int foodCost;
        [Range(0, 999)] public int wealthCost;
        [Range(0, 999)] public int reputationCost;
        [Range(0, 999)] public int materialCost;
        [TextArea] public string effectText;
        public string prerequisiteText; // 前置说明，无前置时留空
    }
}
