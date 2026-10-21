using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// The monsters of the scene a player is in. Single player for now: every session has its own
    /// instance of the scene, built from the spawner data extracted from the Unity scenes.
    /// </summary>
    public sealed class SceneInstance
    {
        /// <summary>Object ids 1..999 are reserved (1 = the player), monsters start above that.</summary>
        private const int FirstMonsterObjectId = 1000;

        /// <summary>Threat level used outside of missions (village) and for missions without one.</summary>
        public const int DefaultThreat = 1;

        private readonly Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        private int _nextObjectId = FirstMonsterObjectId;

        public string SceneName { get; }

        /// <summary>Mission threat level 1..4; decides which spawners take part.</summary>
        public int Threat { get; }

        public IEnumerable<Monster> Monsters => _monsters.Values;

        /// <summary>Karma shards lying on the ground: object id -> amount.</summary>
        public Dictionary<int, int> Karma { get; } = new Dictionary<int, int>();

        /// <summary>Items lying on the ground: object id -> item.</summary>
        public Dictionary<int, Data.ItemData> Items { get; } = new Dictionary<int, Data.ItemData>();

        /// <summary>
        /// Objects placed by abilities (meteors, caltrops, ...): object id -> when they disappear.
        /// The client keeps them until the server says they are gone.
        /// </summary>
        public Dictionary<int, DateTime> TemporaryObjects { get; } = new Dictionary<int, DateTime>();

        public int NewObjectId() => _nextObjectId++;

        /// <summary>
        /// Items the level's tutorial still waits for the player to pick up (the pelt after the
        /// basket). The next monster with loot of its own drops them.
        /// </summary>
        public List<GameData.CollectTarget> PendingCollectTargets { get; } = new List<GameData.CollectTarget>();

        /// <summary>Monsters the client already knows about.</summary>
        private readonly HashSet<int> _announced = new HashSet<int>();

        public bool MarkAnnounced(int objectId) => _announced.Add(objectId);

        public bool IsAnnounced(int objectId) => _announced.Contains(objectId);

        public SceneInstance(string sceneName, int threat = DefaultThreat)
        {
            SceneName = sceneName;
            Threat = threat;
        }

        public Monster Find(int objectId) => _monsters.TryGetValue(objectId, out Monster m) ? m : null;

        public void Remove(int objectId) => _monsters.Remove(objectId);

        // ---- level events: triggers and spawner links -----------------------------------------

        /// <summary>Triggers of the scene. Ids are not unique in every level, so state is kept per trigger object.</summary>
        public List<TriggerInfo> Triggers { get; private set; } = new List<TriggerInfo>();

        /// <summary>How often each trigger has fired.</summary>
        public Dictionary<TriggerInfo, int> TriggerFireCount { get; } = new Dictionary<TriggerInfo, int>();

        /// <summary>Triggers that react to the player walking in. Starts as the level data says; spawners and speech change it.</summary>
        public HashSet<TriggerInfo> ArmedTriggers { get; } = new HashSet<TriggerInfo>();

        /// <summary>Ground known to be walkable (NavGrid): the player's trail, spawn points, wander routes.</summary>
        public NavGrid Nav { get; } = new NavGrid();

        /// <summary>How often each looping spawner has sent its monsters again.</summary>
        public Dictionary<int, int> SpawnerLoops { get; } = new Dictionary<int, int>();

        /// <summary>Armed triggers that went off while the player stood in them and have not been left since.</summary>
        public HashSet<TriggerInfo> TriggersEntered { get; } = new HashSet<TriggerInfo>();

        /// <summary>The spawners taking part at this threat level, by id.</summary>
        public Dictionary<int, GameData.SpawnerInfo> Spawners { get; } = new Dictionary<int, GameData.SpawnerInfo>();

        /// <summary>Spawners that were switched off: their waiting monsters never appear.</summary>
        public HashSet<int> DisabledSpawners { get; } = new HashSet<int>();

        /// <summary>Dead monsters per spawner, for the spawners' linked events.</summary>
        public Dictionary<int, int> KillsPerSpawner { get; } = new Dictionary<int, int>();

        /// <summary>How many monsters each spawner brought into the scene.</summary>
        public Dictionary<int, int> MonstersPerSpawner { get; } = new Dictionary<int, int>();

        /// <summary>Linked events that already happened (each happens once).</summary>
        public HashSet<GameData.SpawnerLink> FiredLinks { get; } = new HashSet<GameData.SpawnerLink>();

        private readonly List<(DateTime Due, Action Action)> _scheduled = new List<(DateTime, Action)>();

        /// <summary>Runs <paramref name="action"/> once <paramref name="due"/> has come (events with a delay).</summary>
        public void Schedule(DateTime due, Action action) => _scheduled.Add((due, action));

        /// <summary>Runs the events that are due, in order.</summary>
        public void RunDueEvents(DateTime now)
        {
            while (true)
            {
                int index = _scheduled.FindIndex(e => e.Due <= now);
                if (index < 0)
                    return;
                Action action = _scheduled[index].Action;
                _scheduled.RemoveAt(index);
                action();
            }
        }

        public IEnumerable<TriggerInfo> TriggersById(int id) => Triggers.Where(t => t.Id == id);

        /// <summary>Monsters a spawner has in this scene.</summary>
        public IEnumerable<Monster> MonstersOf(int spawnerId) => _monsters.Values.Where(m => m.SpawnerId == spawnerId);

        // ---- building ---------------------------------------------------------------------------

        /// <summary>Creates the monsters of every spawner in the scene that takes part at the threat level.</summary>
        public static SceneInstance Build(string sceneName, int threat, GameData data, ILogger log, Random random = null)
        {
            random ??= new Random();
            var scene = new SceneInstance(sceneName, threat);
            AddBreakables(scene, data, random);
            if (data.Scenes.TryGetValue(sceneName, out GameData.SceneInfo info) && info.CollectTargets != null)
                scene.PendingCollectTargets.AddRange(info.CollectTargets);
            data.Triggers.TryGetValue(sceneName, out List<TriggerInfo> triggers);
            scene.Triggers = triggers ?? new List<TriggerInfo>();
            foreach (TriggerInfo trigger in scene.Triggers.Where(t => t.TriggerOn))
                scene.ArmedTriggers.Add(trigger);

            if (!data.Spawners.TryGetValue(sceneName, out List<GameData.SpawnerInfo> spawners))
            {
                log.LogInformation("   no spawner data for scene '{Scene}'", sceneName);
                return scene;
            }

            foreach (GameData.SpawnerInfo spawner in spawners.Where(s => s.SpawnsAtThreat(threat)))
                scene.Spawners.TryAdd(spawner.Id, spawner);

            // Monsters of a spawner that a trigger, another spawner or an object switches on wait until then.
            var waitingSpawners = new HashSet<int>(scene.Triggers
                .SelectMany(t => t.Results)
                .Where(r => r.Type == TriggerResultType.Spawn && r.SpawnerId != 0 && !r.DisableSpawner)
                .Select(r => r.SpawnerId));
            waitingSpawners.UnionWith(scene.Spawners.Values
                .SelectMany(s => s.Links)
                .Where(l => l.Kind == GameData.SpawnerLink.EnableSpawner)
                .Select(l => l.TargetId));
            waitingSpawners.UnionWith(scene.Spawners.Values.Where(s => s.EnabledByObject).Select(s => s.Id));

            int unknown = 0;
            foreach (GameData.SpawnerInfo spawner in spawners.Where(s => s.SpawnsAtThreat(threat)))
                unknown += scene.AddMonsters(spawner, waitingSpawners.Contains(spawner.Id), data, random).Unknown;

            log.LogInformation("   scene '{Scene}' (threat {Threat}): {Count} monsters from {Spawners} of {All} spawners ({Dormant} waiting, {Triggers} triggers){Unknown}",
                sceneName, threat, scene._monsters.Count, scene.Spawners.Count, spawners.Count,
                scene._monsters.Values.Count(m => m.Dormant), scene.Triggers.Count,
                unknown > 0 ? $", {unknown} spawner groups with unknown monsters" : "");
            return scene;
        }

        /// <summary>
        /// Creates the monsters of one spawner: every group, each repeating group in waves (later
        /// waves wait). Returns them, and how many groups name a monster the server does not know.
        /// </summary>
        public (List<Monster> Monsters, int Unknown) AddMonsters(GameData.SpawnerInfo spawner, bool waiting, GameData data, Random random)
        {
            var added = new List<Monster>();
            int unknown = 0;
            int batch = _nextObjectId; // one wave key per group and call, so a respawn gets its own waves
            for (int groupIndex = 0; groupIndex < spawner.Groups.Count; groupIndex++)
            {
                GameData.SpawnGroup group = spawner.Groups[groupIndex];
                List<GameData.MonsterRow> types = (group.MonsterPrefabs.Count > 0 ? group.MonsterPrefabs : new List<string> { group.MonsterPrefab })
                    .Select(data.MonsterByPrefab)
                    .Where(t => t != null)
                    .ToList();
                if (types.Count == 0)
                {
                    unknown++;
                    continue;
                }

                int waves = Math.Max(1, group.RepeatCount);
                for (int n = 0; n < group.Count * waves; n++)
                {
                    int wave = n / Math.Max(1, group.Count);
                    int i = n % Math.Max(1, group.Count);
                    GameData.MonsterRow type = group.RandomPrefab ? types[random.Next(types.Count)] : types[n % types.Count];
                    float[] point = group.SpawnPositions.Count > 0
                        ? group.SpawnPositions[i % group.SpawnPositions.Count]
                        : spawner.Position;
                    float[] waypoint = group.Waypoints.Count > 0 ? group.Waypoints[i % group.Waypoints.Count] : null;

                    var monster = new Monster
                    {
                        ObjectId = _nextObjectId,
                        Type = type,
                        Position = SpawnPoint(group, point, random),
                        Hp = type.MaxHealth,
                        SpawnerId = spawner.Id,
                        Dormant = waiting || wave > 0,
                        WaveGroup = waves > 1 ? $"{spawner.Id}:{groupIndex}:{batch}" : null,
                        Wave = wave,
                        Entrance = group.InitialState,
                        SpawnAnimIndex = EntranceFor(type, group.SpawnAnimIndex),
                        AppearDelay = group.SpawnDelay + i * group.DelayPerNpc,
                        Waypoint = waypoint != null ? new Vector3(waypoint[0], waypoint[1], waypoint[2]) : null,
                        Patrol = group.Waypoints.Select(w => new Vector3(w[0], w[1], w[2])).ToList(),
                    };
                    _monsters.Add(_nextObjectId, monster);
                    Nav.MarkPoint(monster.Position);
                    foreach (Vector3 stop in monster.Patrol)
                        Nav.MarkPoint(stop);
                    added.Add(monster);
                    _nextObjectId++;
                    MonstersPerSpawner[spawner.Id] = MonstersPerSpawner.GetValueOrDefault(spawner.Id) + 1;
                }
            }
            return (added, unknown);
        }

        /// <summary>
        /// The entrance animation a monster really has: mixed ambush groups ask for one index for all
        /// (a Thrall's jump is index 1), but a Shade Wolf has a single entrance and a Zealot's second
        /// is empty; the client then plays nothing and the monster just stands there.
        /// </summary>
        private static int EntranceFor(GameData.MonsterRow type, int wanted)
        {
            List<float> clips = type.SpawnSeconds;
            if (clips == null || clips.Count == 0)
                return wanted;
            if (wanted >= 0 && wanted < clips.Count && clips[wanted] > 0)
                return wanted;
            int first = clips.FindIndex(t => t > 0);
            return first >= 0 ? first : 0;
        }

        /// <summary>
        /// A place inside the group's spawn area (NpcSpawner.Spawner: a circle of SpawnArea around the
        /// location, or a SpawnWidth x SpawnHeight rectangle turned by SpawnRotation), so a pack
        /// stands spread out instead of on one spot. The client puts it on the ground itself.
        /// </summary>
        private static Vector3 SpawnPoint(GameData.SpawnGroup group, float[] location, Random random)
        {
            float x = location[0], z = location[2];
            if (group.Circle && group.SpawnArea > 0)
            {
                double angle = random.NextDouble() * 2 * Math.PI;
                double radius = group.SpawnArea * Math.Sqrt(random.NextDouble());
                x += (float)(Math.Cos(angle) * radius);
                z += (float)(Math.Sin(angle) * radius);
            }
            else if (!group.Circle && (group.SpawnWidth > 0 || group.SpawnHeight > 0))
            {
                double along = (random.NextDouble() - 0.5) * group.SpawnWidth;
                double across = (random.NextDouble() - 0.5) * group.SpawnHeight;
                double yaw = group.SpawnRotation * Math.PI / 180.0;
                x += (float)(along * Math.Cos(yaw) + across * Math.Sin(yaw));
                z += (float)(-along * Math.Sin(yaw) + across * Math.Cos(yaw));
            }
            return new Vector3(x, location[1], z);
        }

        // ---- interactive objects (baskets, chests, switches) ------------------------------------

        private readonly Dictionary<int, Breakable> _breakables = new Dictionary<int, Breakable>();

        public IEnumerable<Breakable> Breakables => _breakables.Values;

        public Breakable FindBreakable(int objectId) => _breakables.TryGetValue(objectId, out Breakable b) ? b : null;

        public void RemoveBreakable(int objectId) => _breakables.Remove(objectId);

        /// <summary>Places the scene's objects; some only appear by chance (AppearChance).</summary>
        private static void AddBreakables(SceneInstance scene, GameData data, Random random)
        {
            if (!data.Breakables.TryGetValue(scene.SceneName, out List<GameData.BreakableInfo> infos))
                return;
            foreach (GameData.BreakableInfo info in infos)
            {
                if (info.AppearChance < 100 && random.Next(100) >= info.AppearChance)
                    continue;
                int id = scene._nextObjectId++;
                scene._breakables[id] = new Breakable
                {
                    ObjectId = id,
                    Info = info,
                    Hp = info.Health,
                    Position = new Vector3(info.Position[0], info.Position[1], info.Position[2]),
                };
            }
        }

        /// <summary>Breakables within <paramref name="radius"/> of a position.</summary>
        public IEnumerable<Breakable> BreakablesNear(Vector3 position, float radius) =>
            _breakables.Values.Where(b => b.Hp > 0 && Distance(b.Position, position) <= radius);

        /// <summary>Monsters within <paramref name="radius"/> of a position, closest first.</summary>
        public IEnumerable<Monster> Near(Vector3 position, float radius)
        {
            return _monsters.Values
                .Where(m => m.IsActive && Distance(m.Position, position) <= radius)
                .OrderBy(m => Distance(m.Position, position));
        }

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
