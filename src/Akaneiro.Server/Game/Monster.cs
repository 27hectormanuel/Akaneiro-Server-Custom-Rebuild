using UnityEngine;

namespace Akaneiro.Server.Game
{
    /// <summary>NpcSpawner.EStateType: how a spawner's monsters enter the world.</summary>
    public static class MonsterEntrance
    {
        public const int Idle = 0;
        public const int Wander = 1;
        public const int Attack = 2;

        /// <summary>Asleep until it notices the player, then it wakes up.</summary>
        public const int Sleep = 3;

        /// <summary>An ambush: it plays its entrance animation (jumps over a wall, ...) and attacks at once.</summary>
        public const int Spawn = 4;
    }

    /// <summary>A monster instance in the scene the player is currently in.</summary>
    public sealed class Monster
    {
        public int ObjectId { get; init; }
        public GameData.MonsterRow Type { get; init; }
        public Vector3 Position { get; set; }
        public int Hp { get; set; }

        /// <summary>When the client was told about this monster; the movement packets are timed against it.</summary>
        public System.DateTime SpawnedAt { get; set; }

        /// <summary>The spawner this monster belongs to (0 = none).</summary>
        public int SpawnerId { get; init; }

        /// <summary>Monsters of a trigger-linked spawner wait until their trigger fires.</summary>
        public bool Dormant { get; set; }

        /// <summary>How it enters the world (<see cref="MonsterEntrance"/>), from its spawner.</summary>
        public int Entrance { get; init; }

        /// <summary>The entrance animation of an ambush.</summary>
        public int SpawnAnimIndex { get; init; }

        /// <summary>Seconds after its spawner wakes until it appears (SpawnDelay + DelayPerNpc of its group).</summary>
        public float AppearDelay { get; init; }

        /// <summary>Where it heads first: an ambusher's entrance jump lands there. Null = nowhere.</summary>
        public Vector3? Waypoint { get; init; }

        /// <summary>The wander route of its group (NpcSpawner WanderWaypoint), walked while it has not noticed the player.</summary>
        public System.Collections.Generic.List<Vector3> Patrol { get; init; } = new System.Collections.Generic.List<Vector3>();

        /// <summary>
        /// Groups that repeat (SpawnRepeatCount) come in waves: wave 0 like any other monster, each
        /// further wave once the one before it is dead. Monsters of one group share the key.
        /// </summary>
        public string WaveGroup { get; init; }
        public int Wave { get; init; }

        /// <summary>Weapon elements and masteries: a stunned monster does nothing, a slowed one moves
        /// and attacks at half speed.</summary>
        public System.DateTime StunnedUntil { get; set; }
        public System.DateTime SlowedUntil { get; set; }

        public bool IsActive => IsAlive && !Dormant;

        public bool IsAlive => Hp > 0;
    }
}
