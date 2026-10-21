using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// A mission as defined by its prefab under Assets/Scenes/Areas/Area_N/Mission/&lt;id&gt;.prefab,
    /// exported by tools/SceneExtract. Field names match the client's own fields.
    /// </summary>
    public sealed class MissionDefinition
    {
        [JsonPropertyName("ID")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("mapName")] public string MapName { get; set; } = "";
        [JsonPropertyName("levelMin")] public int LevelMin { get; set; }
        [JsonPropertyName("levelMax")] public int LevelMax { get; set; }
        [JsonPropertyName("xp")] public int RewardExp { get; set; }
        [JsonPropertyName("sk")] public int RewardKarma { get; set; }

        /// <summary>Missions that finishing this one opens up ("5111;5112;5121": again, next threat, next map).</summary>
        [JsonPropertyName("NewMissionID")] public string NewMissionIds { get; set; } = "";
        [JsonPropertyName("branchArray")] public List<MissionBranch> Branches { get; set; } = new List<MissionBranch>();
    }

    public sealed class MissionBranch
    {
        /// <summary>The branch's id (mostly 1, 2); the client finds a task's name by it (GetCurMissionTaskName).</summary>
        [JsonPropertyName("BranchID")] public int BranchId { get; set; }
        [JsonPropertyName("BranchName")] public string Name { get; set; } = "";
        [JsonPropertyName("isBonus")] public int IsBonus { get; set; }
        [JsonPropertyName("rewardContentExp")] public int RewardExp { get; set; }
        [JsonPropertyName("rewardContentKarma")] public int RewardKarma { get; set; }
        [JsonPropertyName("taskArray")] public List<MissionTask> Tasks { get; set; } = new List<MissionTask>();
    }

    public sealed class MissionTask
    {
        [JsonPropertyName("TaskID")] public int TaskId { get; set; }
        [JsonPropertyName("taskName")] public string Name { get; set; } = "";
        [JsonPropertyName("triggerId")] public string TriggerId { get; set; } = "";
        [JsonPropertyName("rewardContentExp")] public int RewardExp { get; set; }
        [JsonPropertyName("rewardContentKarma")] public int RewardKarma { get; set; }
        [JsonPropertyName("SubObject")] public List<MissionObjective> Objectives { get; set; } = new List<MissionObjective>();
    }

    /// <summary>One objective of a task, e.g. "kill 2 Insane Wolves".</summary>
    public sealed class MissionObjective
    {
        /// <summary>_UI_CS_RamusTask.MISSION_TYPE: 1 = travel, 2 = hunt, 3 = collect, 8 = destroy.</summary>
        [JsonPropertyName("typeID")] public int TypeId { get; set; }

        /// <summary>What to hunt/collect/reach, e.g. a monster's TypeID.</summary>
        [JsonPropertyName("objectID")] public int ObjectId { get; set; }

        [JsonPropertyName("count")] public int Count { get; set; }

        /// <summary>Travel objectives: the place is (objectID, recycle) as x and z, reached within 5 (PlayerDataManager.CheckMissionProgress).</summary>
        [JsonPropertyName("recycle")] public int Recycle { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = "";
    }

    public static class MissionObjectiveType
    {
        public const int Travel = 1;
        public const int Hunt = 2;
        public const int Collect = 3;
        public const int Destroy = 8;
    }
}
