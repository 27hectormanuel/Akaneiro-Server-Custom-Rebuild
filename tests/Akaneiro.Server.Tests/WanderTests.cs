using Akaneiro.Server.Game;
using Akaneiro.Server.Tests.Harness;
using UnityEngine;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

/// <summary>Monsters with the entrance "Wander" walk their waypoints until they notice the player.</summary>
public sealed class WanderTests
{
    private readonly ITestOutputHelper _output;

    public WanderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void AWandererWalksItsRoute()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterScene("A5_M3");
        player.Session.CurrentHp = 1_000_000;
        var wanderer = player.Scene.Monsters.First(m => m.Entrance == MonsterEntrance.Wander && m.Patrol.Count > 0);
        wanderer.Dormant = false;
        Vector3 start = wanderer.Position;

        // Close enough to be shown, too far to be noticed.
        float vision = wanderer.Type.VisionRadius > 0 ? wanderer.Type.VisionRadius : 6f;
        player.MoveTo(new Vector3(start.x + vision + 5, start.y, start.z));
        player.Wait(3);

        Assert.NotEqual(start, wanderer.Position);
        Assert.True(player.Has("On_SetObjectVecPosition", m => m.Arg<int>(1) == wanderer.ObjectId));
    }
}
