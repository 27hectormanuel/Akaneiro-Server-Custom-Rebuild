using System.Collections.Generic;

namespace Akaneiro.Server.Game
{
    /// <summary>
    /// A trigger volume of a scene, extracted from Trigger_Unified (and the older Trigger) by tools/SceneExtract.
    /// The player walking into the box is what sets it off; the server then plays the results.
    /// </summary>
    public sealed class TriggerInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        /// <summary>Trigger_Unified.UnifiedTriggerType: 0 = spawn, 1 = play animation, 2 = speech, 3 = teleport.</summary>
        public int UnifiedType { get; set; }

        public int TriggerType { get; set; }

        /// <summary>Whether the trigger is armed from the start. Disabled ones wait for mission progress.</summary>
        public bool TriggerOn { get; set; }

        /// <summary>How often it may fire; 0 means unlimited.</summary>
        public int MaxCount { get; set; }

        public float DelayTime { get; set; }
        public float[] Center { get; set; }
        public float[] Size { get; set; }
        public float RotationY { get; set; }
        public List<TriggerResult> Results { get; set; } = new List<TriggerResult>();

        /// <summary>Triggers this one arms when it fires (speech triggers).</summary>
        public List<int> EnableTriggers { get; set; } = new List<int>();
    }

    /// <summary>What happens when a trigger fires.</summary>
    public sealed class TriggerResult
    {
        /// <summary>What kind of result (<see cref="TriggerResultType"/>): spawn, teleport, animation or speech.</summary>
        public int Type { get; set; }

        /// <summary>Object the animation applies to (the client knows it by this id).</summary>
        public int ObjectId { get; set; }

        public int RealId { get; set; }
        public string AnimName { get; set; } = "";
        public bool Loop { get; set; }
        public float Delay { get; set; }

        /// <summary>Whether the object still blocks the way afterwards.</summary>
        public bool Collision { get; set; }

        /// <summary>Whether the object is still visible afterwards.</summary>
        public bool Display { get; set; }

        public int SpawnerId { get; set; }

        /// <summary>A spawn result that switches the spawner off instead (speech triggers).</summary>
        public bool DisableSpawner { get; set; }

        /// <summary>Portals: where the player comes out, and how the screen fades.</summary>
        public float[] TeleportPos { get; set; }
        public float FadeIn { get; set; } = 1f;
        public float FadeOut { get; set; } = 1f;
        public bool CameraFollow { get; set; } = true;

        /// <summary>Speech bubbles: the NPC type that speaks (0 = the player), the lines it says, and
        /// whether the bubble only stays while the player is inside the trigger. Loop and Delay
        /// (the speaking time) are shared with animations.</summary>
        public int NpcId { get; set; }
        public bool Random { get; set; }
        public List<int> Words { get; set; }
        public bool Inside { get; set; }
    }

    public static class TriggerResultType
    {
        public const int Spawn = 0;
        public const int Teleport = 2;
        public const int Speech = 5;
        public const int Animation = 4;
    }
}
