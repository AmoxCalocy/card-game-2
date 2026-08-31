using System;
using System.Collections.Generic;

namespace OneJourney.Core
{
    /// <summary>可安全恢复的战役检查点类型。</summary>
    public enum SaveCheckpointKind
    {
        None = 0,
        Map = 1,
        NodeEntry = 2,
        Camp = 3
    }

    [Serializable]
    public sealed class CampaignSaveData
    {
        public int Checkpoint;
        public long SavedUtcTicks;
        public int Seed;
        public GameRandomState Random;
        public long ElapsedSeconds;
        public CampaignResourceSaveData Resources;
        public CampaignDeckSaveData Deck;
        public List<PartnerSaveData> Partners;
        public List<string> ActivePartnerIds;
        public List<string> RelicIds;
        public List<string> BuildingIds;
        public List<string> EventFlags;
        public CampaignFlagSaveData Flags;
        public RegionMapSaveData Map;
        public List<RunRecordSaveData> RunRecords;
    }

    [Serializable]
    public sealed class CampaignResourceSaveData
    {
        public int Food;
        public int Wealth;
        public int Reputation;
        public int Materials;
        public int Risk;
        public int PlayerFatigue;
        public int PlayerDisease;
        public bool AmbushPending;
    }

    [Serializable]
    public sealed class CampaignDeckSaveData
    {
        public List<string> Cards;
        public List<string> UpgradedCardIds;
    }

    [Serializable]
    public sealed class PartnerSaveData
    {
        public string Id;
        public int CurrentHp;
        public bool IsRecruited;
        public int Loyalty;
        public int Disease;
        public int Fatigue;
    }

    [Serializable]
    public sealed class CampaignFlagSaveData
    {
        public bool GrasslandBossDefeated;
        public bool CampBonusUsedThisRegion;
        public bool EventWealthBonusUsedThisRegion;
        public bool FreeUpgradePending;
        public bool RelicCampFoodUsedThisRegion;
        public bool RelicClinicUsedThisRegion;
        public bool RelicEventWealthUsedThisRegion;
    }

    [Serializable]
    public sealed class RegionMapSaveData
    {
        public int Region;
        public int CurrentNodeIndex;
        public List<int> VisitedIndexes;
        public List<int> Path;
        public List<RegionMapNodeSaveData> Nodes;
    }

    [Serializable]
    public sealed class RegionMapNodeSaveData
    {
        public string Id;
        public int Layer;
        public int Type;
        public string DisplayName;
        public string[] EnemyPoolIds;
        public string[] EventPoolIds;
        public List<int> NextIndexes;
    }

    [Serializable]
    public sealed class RunRecordSaveData
    {
        public int Category;
        public string Detail;
    }
}
