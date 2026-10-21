using UnityEngine;

namespace Akaneiro.Server.Game
{
    /// <summary>A basket, chest or switch in the scene the player is in.</summary>
    public sealed class Breakable
    {
        public int ObjectId { get; init; }
        public GameData.BreakableInfo Info { get; init; }
        public Vector3 Position { get; init; }
        public int Hp { get; set; }
    }
}
