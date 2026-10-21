using Akaneiro.Server.Game;
using Akaneiro.Server.Tests.Harness;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akaneiro.Server.Tests;

/// <summary>Checks over the extracted level and mission data of every map and threat level.</summary>
public sealed class LevelDataTests
{
    public static IEnumerable<object[]> Missions() =>
        TestData.Data.Missions.Values.Select(m => new object[] { m.Id, m.MapName });

    /// <summary>
    /// The spawners whose monsters a player can meet: everything the level's events can wake,
    /// assuming the player walks everywhere and kills everything (a fixpoint over triggers,
    /// spawner links, speech triggers and the mission's task triggers).
    /// </summary>
    private static HashSet<int> ReachableSpawners(SceneInstance scene, MissionDefinition mission)
    {
        var awake = scene.Monsters.Where(m => !m.Dormant).Select(m => m.SpawnerId).ToHashSet();
        // Baskets, chests and switches can all be broken.
        awake.UnionWith(scene.Breakables.SelectMany(b => b.Info.Links).Where(l => l.Kind == GameData.SpawnerLink.EnableSpawner).Select(l => l.TargetId));
        var armed = scene.Triggers.Where(t => t.TriggerOn).ToHashSet();
        var fired = new HashSet<TriggerInfo>();
        var taskTriggers = mission.Branches.SelectMany(b => b.Tasks).SelectMany(t => (t.TriggerId ?? "").Split(';'))
            .Select(s => int.TryParse(s, out int id) ? id : 0).Where(id => id != 0);
        var toFire = new Queue<TriggerInfo>(scene.Triggers.Where(t => taskTriggers.Contains(t.Id)));

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (TriggerInfo trigger in armed.ToList())
                toFire.Enqueue(trigger);

            foreach (int spawnerId in awake.ToList())
            {
                if (!scene.Spawners.TryGetValue(spawnerId, out var spawner))
                    continue;
                foreach (var link in spawner.Links)
                {
                    if (link.Kind == GameData.SpawnerLink.EnableSpawner)
                        changed |= awake.Add(link.TargetId);
                    else if (link.Kind == GameData.SpawnerLink.AutoTrigger)
                        foreach (var t in scene.TriggersById(link.TargetId)) toFire.Enqueue(t);
                    else if (link.Kind == GameData.SpawnerLink.ToggleTrigger)
                        foreach (var t in scene.TriggersById(link.TargetId)) changed |= armed.Add(t);
                }
            }

            while (toFire.Count > 0)
            {
                TriggerInfo trigger = toFire.Dequeue();
                if (!fired.Add(trigger))
                    continue;
                changed = true;
                foreach (var result in trigger.Results.Where(r => r.Type == TriggerResultType.Spawn && !r.DisableSpawner))
                    awake.Add(result.SpawnerId);
                foreach (int id in trigger.EnableTriggers ?? new List<int>())
                    foreach (var t in scene.TriggersById(id)) armed.Add(t);
            }
        }
        return awake;
    }

    /// <summary>
    /// Missions that cannot be finished yet, and why. When one of them starts working, the test
    /// says so, so this list only ever shrinks.
    /// </summary>
    private static readonly Dictionary<int, string> KnownGaps = new()
    {
        [5832] = "A8_M3 threat 2: the objective's target (132) is placed by neither a spawner nor an object",
    };

    [Theory]
    [MemberData(nameof(Missions))]
    public void EveryHuntObjectiveHasEnoughMonsters(int missionId, string map)
    {
        if (!TestData.Data.Spawners.ContainsKey(map))
            return; // developer test maps (Aka_TestBox) are not part of the game

        MissionDefinition mission = TestData.Data.Missions[missionId];
        var scene = SceneInstance.Build(map, missionId % 10, TestData.Data, NullLogger.Instance);
        var reachable = ReachableSpawners(scene, mission);
        var available = scene.Monsters.Where(m => reachable.Contains(m.SpawnerId))
            .GroupBy(m => m.Type.TypeId).ToDictionary(g => g.Key, g => g.Count());

        var missing = mission.Branches.Where(b => b.IsBonus == 0).SelectMany(b => b.Tasks).SelectMany(t => t.Objectives)
            .Where(o => o.TypeId == MissionObjectiveType.Hunt)
            .Where(o => available.GetValueOrDefault(o.ObjectId) < o.Count)
            .Select(o => $"'{o.Description}': {o.Count} of monster {o.ObjectId} needed, {available.GetValueOrDefault(o.ObjectId)} reachable")
            .ToList();

        if (KnownGaps.TryGetValue(missionId, out string reason))
            Assert.True(missing.Count > 0, $"mission {missionId} works now, remove it from the known gaps ({reason})");
        else
            Assert.True(missing.Count == 0, string.Join("; ", missing));
    }

    public static IEnumerable<object[]> ScenesAndThreats() =>
        TestData.Data.Spawners.Keys.SelectMany(scene => Enumerable.Range(1, 4).Select(threat => new object[] { scene, threat }));

    [Theory]
    [MemberData(nameof(ScenesAndThreats))]
    public void EveryMissionMapHasSomethingToFight(string sceneName, int threat)
    {
        if (sceneName == "Hub_Village")
            return; // the village is peaceful

        var scene = SceneInstance.Build(sceneName, threat, TestData.Data, NullLogger.Instance);

        Assert.NotEmpty(scene.Monsters);
    }
}
