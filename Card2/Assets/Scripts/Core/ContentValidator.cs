using System.Collections.Generic;
using UnityEngine;

namespace OneJourney.Core
{
    /// <summary>一条校验失败记录：定位到内容、字段与原因（实施计划 A0-4）。</summary>
    public struct ValidationIssue
    {
        public readonly string ContentType;
        public readonly string ContentId;
        public readonly string Field;
        public readonly string Reason;

        public ValidationIssue(string contentType, string contentId, string field, string reason)
        {
            ContentType = contentType;
            ContentId = contentId;
            Field = field;
            Reason = reason;
        }

        public override string ToString()
        {
            return ContentType + "[" + ContentId + "] 字段[" + Field + "]：" + Reason;
        }
    }

    /// <summary>
    /// 内容完整性校验（实施计划 A0-4）：必填信息、取值范围、引用完整性与 ID 唯一性。
    /// 校验为纯函数，不修改输入；有任一问题时该内容不得进入可玩流程。
    /// </summary>
    public static class ContentValidator
    {
        public static List<ValidationIssue> Validate(IEnumerable<ContentBase> contents)
        {
            var issues = new List<ValidationIssue>();
            var ids = new HashSet<string>();
            var byId = new Dictionary<string, ContentBase>();

            foreach (var content in contents)
            {
                if (content == null)
                {
                    issues.Add(new ValidationIssue("内容", "(空)", "对象", "内容资产为空引用"));
                    continue;
                }

                string type = content.GetType().Name;
                CheckRequired(issues, type, content.id, content.displayName, content.description);

                if (string.IsNullOrEmpty(content.id))
                {
                    continue; // 无 ID 无法继续定位，跳过后续检查
                }

                if (!ids.Add(content.id))
                {
                    issues.Add(new ValidationIssue(type, content.id, "id", "标识重复（全局唯一）"));
                }

                byId[content.id] = content;
                ValidateSpecific(content, issues);
            }

            // 引用完整性放在第二遍扫描，保证后注册的内容也能被解析
            foreach (var content in contents)
            {
                if (content == null || string.IsNullOrEmpty(content.id))
                {
                    continue;
                }

                CheckReferences(content, byId, issues);
            }

            return issues;
        }

        private static void CheckRequired(List<ValidationIssue> issues, string type, string id, string name, string description)
        {
            if (string.IsNullOrEmpty(id))
            {
                issues.Add(new ValidationIssue(type, "(未命名)", "id", "缺少必填标识"));
            }

            if (string.IsNullOrEmpty(name))
            {
                issues.Add(new ValidationIssue(type, id ?? "(未命名)", "displayName", "缺少必填显示名称"));
            }

            if (string.IsNullOrEmpty(description))
            {
                issues.Add(new ValidationIssue(type, id ?? "(未命名)", "description", "缺少必填描述"));
            }
        }

        private static void ValidateSpecific(ContentBase content, List<ValidationIssue> issues)
        {
            string type = content.GetType().Name;
            string id = content.id;

            switch (content)
            {
                case CardData card:
                    if (card.cost < 0 || card.cost > 4)
                    {
                        issues.Add(new ValidationIssue(type, id, "cost", "费用越界：" + card.cost + "（允许 0-4）"));
                    }

                    if (string.IsNullOrEmpty(card.effectText))
                    {
                        issues.Add(new ValidationIssue(type, id, "effectText", "缺少必填效果说明"));
                    }
                    break;

                case PartnerData partner:
                    if (partner.maxHp < 1 || partner.maxHp > 100)
                    {
                        issues.Add(new ValidationIssue(type, id, "maxHp", "生命越界：" + partner.maxHp + "（允许 1-100）"));
                    }

                    if (partner.commandDamage < 0 || partner.commandDamage > 20)
                    {
                        issues.Add(new ValidationIssue(type, id, "commandDamage", "指令伤害越界：" + partner.commandDamage + "（允许 0-20）"));
                    }

                    if (string.IsNullOrEmpty(partner.role))
                    {
                        issues.Add(new ValidationIssue(type, id, "role", "缺少必填定位"));
                    }

                    if (string.IsNullOrEmpty(partner.joinCardId))
                    {
                        issues.Add(new ValidationIssue(type, id, "joinCardId", "缺少必填专属加入卡引用"));
                    }
                    break;

                case EnemyData enemy:
                    if (enemy.maxHp < 1 || enemy.maxHp > 200)
                    {
                        issues.Add(new ValidationIssue(type, id, "maxHp", "生命越界：" + enemy.maxHp + "（允许 1-200）"));
                    }

                    if (enemy.intents == null || enemy.intents.Count == 0)
                    {
                        issues.Add(new ValidationIssue(type, id, "intents", "缺少必填意图配置"));
                    }
                    else
                    {
                        for (int i = 0; i < enemy.intents.Count; i++)
                        {
                            var intent = enemy.intents[i];
                            if (intent == null)
                            {
                                issues.Add(new ValidationIssue(type, id, "intents[" + i + "]", "意图为空引用"));
                                continue;
                            }

                            if (string.IsNullOrEmpty(intent.name))
                            {
                                issues.Add(new ValidationIssue(type, id, "intents[" + i + "].name", "缺少必填意图名称"));
                            }

                            if (intent.weight <= 0)
                            {
                                issues.Add(new ValidationIssue(type, id, "intents[" + i + "].weight", "权重必须为正：" + intent.weight));
                            }

                            if (string.IsNullOrEmpty(intent.effectText))
                            {
                                issues.Add(new ValidationIssue(type, id, "intents[" + i + "].effectText", "缺少必填意图效果说明"));
                            }
                        }
                    }
                    break;

                case EventData evt:
                    if (evt.options == null || evt.options.Count < 2)
                    {
                        issues.Add(new ValidationIssue(type, id, "options", "选项数量不足：" + (evt.options?.Count ?? 0) + "（至少 2 项）"));
                    }
                    else
                    {
                        for (int i = 0; i < evt.options.Count; i++)
                        {
                            var option = evt.options[i];
                            if (option == null)
                            {
                                issues.Add(new ValidationIssue(type, id, "options[" + i + "]", "选项为空引用"));
                                continue;
                            }

                            if (string.IsNullOrEmpty(option.label))
                            {
                                issues.Add(new ValidationIssue(type, id, "options[" + i + "].label", "缺少必填选项标签"));
                            }

                            if (string.IsNullOrEmpty(option.resultText))
                            {
                                issues.Add(new ValidationIssue(type, id, "options[" + i + "].resultText", "缺少必填选项结果说明"));
                            }
                        }
                    }
                    break;

                case RelicData relic:
                    if (string.IsNullOrEmpty(relic.effectText))
                    {
                        issues.Add(new ValidationIssue(type, id, "effectText", "缺少必填效果说明"));
                    }
                    break;

                case BuildingData building:
                    if (building.foodCost < 0 || building.wealthCost < 0 || building.reputationCost < 0 || building.materialCost < 0)
                    {
                        issues.Add(new ValidationIssue(type, id, "cost", "成本不能为负"));
                    }

                    if (string.IsNullOrEmpty(building.effectText))
                    {
                        issues.Add(new ValidationIssue(type, id, "effectText", "缺少必填效果说明"));
                    }
                    break;
            }
        }

        private static void CheckReferences(ContentBase content, Dictionary<string, ContentBase> byId, List<ValidationIssue> issues)
        {
            string type = content.GetType().Name;
            string id = content.id;

            switch (content)
            {
                case PartnerData partner:
                    if (!string.IsNullOrEmpty(partner.joinCardId) && !byId.ContainsKey(partner.joinCardId))
                    {
                        issues.Add(new ValidationIssue(type, id, "joinCardId", "引用不存在的卡牌 ID：" + partner.joinCardId));
                    }
                    break;

                case EventData evt:
                    for (int i = 0; i < evt.options.Count; i++)
                    {
                        var option = evt.options[i];
                        if (option == null)
                        {
                            continue;
                        }

                        CheckIdArray(issues, type, id, "options[" + i + "].enemyIds", option.enemyIds, byId);
                        if (!string.IsNullOrEmpty(option.partnerId) && !byId.ContainsKey(option.partnerId))
                        {
                            issues.Add(new ValidationIssue(type, id, "options[" + i + "].partnerId", "引用不存在的伙伴 ID：" + option.partnerId));
                        }

                        if (!string.IsNullOrEmpty(option.cardId) && !byId.ContainsKey(option.cardId))
                        {
                            issues.Add(new ValidationIssue(type, id, "options[" + i + "].cardId", "引用不存在的卡牌 ID：" + option.cardId));
                        }

                        if (!string.IsNullOrEmpty(option.relicId) && !byId.ContainsKey(option.relicId))
                        {
                            issues.Add(new ValidationIssue(type, id, "options[" + i + "].relicId", "引用不存在的遗物 ID：" + option.relicId));
                        }
                    }
                    break;

                case NodeData node:
                    CheckIdArray(issues, type, id, "enemyPoolIds", node.enemyPoolIds, byId);
                    CheckIdArray(issues, type, id, "eventPoolIds", node.eventPoolIds, byId);
                    break;
            }
        }

        private static void CheckIdArray(
            List<ValidationIssue> issues, string type, string id, string field, string[] ids, Dictionary<string, ContentBase> byId)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]) && !byId.ContainsKey(ids[i]))
                {
                    issues.Add(new ValidationIssue(type, id, field + "[" + i + "]", "引用不存在的内容 ID：" + ids[i]));
                }
            }
        }
    }

    /// <summary>
    /// 内容注册表：从 Resources/Content 加载全部内容资产并校验（实施计划 A0-4）。
    /// 存在校验问题时 HasBlockingIssues 为 true，阻止进入可玩流程。
    /// </summary>
    public static class ContentRegistry
    {
        private static readonly List<ContentBase> AllContents = new List<ContentBase>();
        private static List<ValidationIssue> _issues = new List<ValidationIssue>();

        public static IReadOnlyList<ContentBase> Contents => AllContents;

        public static IReadOnlyList<ValidationIssue> Issues => _issues;

        public static bool HasBlockingIssues => _issues.Count > 0;

        public static void LoadAll()
        {
            AllContents.Clear();
            AllContents.AddRange(Resources.LoadAll<ContentBase>("Content"));
            ValidateAll();
        }

        /// <summary>清空已注册内容与校验结果（测试隔离与修复后重新加载用）。</summary>
        public static void Clear()
        {
            AllContents.Clear();
            _issues = new List<ValidationIssue>();
        }

        public static void Register(ContentBase content)
        {
            AllContents.Add(content);
        }

        public static void ValidateAll()
        {
            _issues = ContentValidator.Validate(AllContents);
            for (int i = 0; i < _issues.Count; i++)
            {
                Debug.LogError("[内容校验] " + _issues[i].ToString());
            }
        }
    }
}
