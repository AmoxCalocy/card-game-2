using System;
using System.Collections.Generic;

namespace OneJourney.Core
{
    public enum TutorialTopic
    {
        MapMovement = 0,
        Food = 1,
        PlayCards = 2,
        SharedEnergy = 3,
        EnemyIntent = 4,
        Reward = 5,
        EventChoice = 6,
        CampBuildings = 7
    }

    public enum HelpSection
    {
        Overview = 0,
        CardTypes = 1,
        Statuses = 2,
        Resources = 3,
        Icons = 4
    }

    public sealed class TutorialEntry
    {
        public TutorialTopic Topic { get; }
        public string Title { get; }
        public string Summary { get; }
        public string Detail { get; }
        public HelpSection HelpSection { get; }

        public TutorialEntry(TutorialTopic topic, string title, string summary, string detail, HelpSection helpSection)
        {
            Topic = topic;
            Title = title;
            Summary = summary;
            Detail = detail;
            HelpSection = helpSection;
        }
    }

    public sealed class HelpSectionEntry
    {
        public HelpSection Section { get; }
        public string Title { get; }
        public string Body { get; }

        public HelpSectionEntry(HelpSection section, string title, string body)
        {
            Section = section;
            Title = title;
            Body = body;
        }
    }

    /// <summary>A3-26 基础引导与规则说明的唯一文本目录。</summary>
    public static class TutorialContent
    {
        private static readonly TutorialEntry[] Tutorials =
        {
            new TutorialEntry(
                TutorialTopic.MapMovement,
                "地图移动",
                "金色边框表示当前可达节点。先点击节点进行选择，再次点击才会确认前往。",
                "你只能前往下一层且与当前位置相连的节点。第一次点击用于确认路线，第二次点击才执行移动、消耗资源并进入节点内容；未来节点和已经走过的节点不能直接选择。",
                HelpSection.Icons),
            new TutorialEntry(
                TutorialTopic.Food,
                "粮食与移动消耗",
                "草原每次移动消耗 1 粮食，密林每次移动消耗 2 粮食。",
                "粮食不足不会阻止移动，但粮食会归零，主角增加 1 层疲劳，区域风险额外增加 2。移动前可在地图顶部查看当前粮食和风险。",
                HelpSection.Resources),
            new TutorialEntry(
                TutorialTopic.PlayCards,
                "出牌与目标",
                "点击下方手牌进行出牌；需要目标的卡牌会等待你选择有效单位。",
                "卡牌左上角数字是能量费用。单体卡牌选中后，再点击高亮的有效目标；无目标或全体卡牌会立即结算。若能量不足或没有合法目标，卡牌不会被消耗。",
                HelpSection.CardTypes),
            new TutorialEntry(
                TutorialTopic.SharedEnergy,
                "共享能量",
                "队伍每回合共享 3 点能量，所有上阵成员的卡牌都从同一能量池支付。",
                "顶部能量会在玩家回合开始时恢复。合理组合低费与高费卡，结束回合后敌人将按意图行动，未保留的手牌会进入弃牌流程。",
                HelpSection.CardTypes),
            new TutorialEntry(
                TutorialTopic.EnemyIntent,
                "敌人意图",
                "敌人行动前会公开意图，显示攻击、防御、掠夺或附带状态。",
                "在结束回合前阅读每名敌人的意图。你可以用护甲、治疗、意图削弱、诱饵或击杀来应对；意图文字是行动预告，敌人真正行动时仍会重新检查目标是否存活。",
                HelpSection.Icons),
            new TutorialEntry(
                TutorialTopic.Reward,
                "战斗奖励",
                "战斗胜利后处理卡牌与遗物奖励，全部处理完毕后才能继续旅程。",
                "普通奖励通常提供卡牌选择；精英和首领还可能提供遗物。卡牌与遗物各自只能选择一项，也可以放弃剩余奖励。跳过只放弃未领取奖励，不会撤销已经获得的资源。",
                HelpSection.CardTypes),
            new TutorialEntry(
                TutorialTopic.EventChoice,
                "事件选择",
                "事件选项会提前显示条件、成本、预期结果和锁定原因。",
                "灰色选项表示当前条件不足。部分选项会继续要求选择卡牌或队员，也可能进入战斗；只有完成整个事件后才会返回地图并写入安全存档。",
                HelpSection.Overview),
            new TutorialEntry(
                TutorialTopic.CampBuildings,
                "营地与建筑",
                "营地左侧查看队伍状态，右侧使用设施、建造建筑或管理牌组。",
                "建筑会显示财富、建材、声望与首领前置条件。建成后可提供治疗、升级、资源或事件奖励。离开营地会返回地图并保存当前战役状态。",
                HelpSection.Resources)
        };

        private static readonly HelpSectionEntry[] HelpSections =
        {
            new HelpSectionEntry(
                HelpSection.Overview,
                "旅程总览",
                "核心流程\n地图选择节点 → 处理战斗、事件或营地 → 领取奖励 → 继续深入区域。\n\n安全与结局\n主角死亡会结束本局。战斗、事件选择和奖励处理中不会保存半完成状态；继续游戏会从最近的安全节点重新开始该段内容。\n\n交互原则\n金色边框通常表示当前可选目标，灰色表示条件不足。返回主菜单会结束当前运行中的会话，但不会重置已经确认过的基础引导。"),
            new HelpSectionEntry(
                HelpSection.CardTypes,
                "卡牌类型",
                "攻击\n主要造成直接伤害。\n\n防御\n提供护甲、治疗或保护伙伴。\n\n策略\n调整抽牌、弃牌、状态与资源。\n\n战术\n利用伙伴指令、士气、集火、诱饵和敌人意图。\n\n后勤\n影响粮食、牌组维护或战役资源。\n\n费用与目标\n卡牌左上角是能量费用。单体卡需要选择目标；无目标和全体卡通常会直接结算。战斗手牌与奖励卡使用同一张卡牌模板。"),
            new HelpSectionEntry(
                HelpSection.Statuses,
                "状态说明",
                "护甲\n优先吸收普通伤害，每名单位有自己的护甲上限。\n\n流血\n回合开始时造成等于层数的真实伤害，随后减少 1 层。\n\n士气\n提高队伍造成的伤害，存在层数上限。\n\n疾病\n每层降低最大生命，施加时当前生命会被钳制到新的上限。\n\n疲劳\n每层降低护甲上限与伙伴指令伤害。\n\n集火与诱饵\n集火令目标受到普通伤害时承受额外伤害；诱饵可改变敌人当前攻击目标并削弱意图。"),
            new HelpSectionEntry(
                HelpSection.Resources,
                "资源与风险",
                "粮食\n用于地图移动：草原消耗 1，密林消耗 2。不足时仍可前进，但主角增加疲劳且风险额外上升。\n\n财富\n用于事件支付、治疗和建筑。\n\n声望\n用于高阶建筑与部分事件条件。\n\n建材\n主要用于建设营地与城镇建筑。\n\n风险\n区域风险范围为 0–10。移动会提高风险，营地会降低风险；达到阈值会触发危机伏击并回落到 5。"),
            new HelpSectionEntry(
                HelpSection.Icons,
                "图标与页面标记",
                "地图节点\n战：普通战斗　事：事件　营：营地　精：精英　首：首领　始：出发点。\n\n地图状态\n金色：当前或可达　深色：未来节点　已走路线会保留较弱高亮。\n\n战斗信息\n卡牌费用显示在左上角；敌人卡底部意图文字说明下一次行动；目标选择时可点击单位会出现高亮边框。\n\n奖励与锁定\n奖励卡可直接点击领取；灰色事件或建筑入口会同时显示无法使用的文字原因。")
        };

        public static IReadOnlyList<TutorialEntry> AllTutorials => Tutorials;
        public static IReadOnlyList<HelpSectionEntry> AllHelpSections => HelpSections;
        public static int TutorialCount => Tutorials.Length;
        public static int AllTopicMask => (1 << Tutorials.Length) - 1;

        public static TutorialEntry Find(TutorialTopic topic)
        {
            int index = (int)topic;
            return index >= 0 && index < Tutorials.Length ? Tutorials[index] : null;
        }

        public static HelpSectionEntry Find(HelpSection section)
        {
            int index = (int)section;
            return index >= 0 && index < HelpSections.Length ? HelpSections[index] : null;
        }
    }
}
