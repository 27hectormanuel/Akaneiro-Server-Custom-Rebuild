using Akaneiro.Server.Game;
using UnityEngine;

namespace Akaneiro.Server.Tests;

/// <summary>Monsters keep to ground known to be walkable: they follow the player's trail around corners.</summary>
public sealed class NavGridTests
{
    [Fact]
    public void AMonsterFollowsTheTrailAroundACorner()
    {
        // The player walked east, then north: an L around something in the corner.
        var nav = new NavGrid();
        foreach (float x in new[] { 0f, 10f, 20f, 30f })
            nav.MarkPlayer(new Vector3(x, 0, 0));
        foreach (float z in new[] { 10f, 20f, 30f })
            nav.MarkPlayer(new Vector3(30, 0, z));

        var path = nav.FindPath(new Vector3(0, 0, 0), new Vector3(30, 0, 30));

        // Only the corner (one or two points around it), then the player.
        Assert.InRange(path.Count, 2, 3);
        Assert.All(path.Take(path.Count - 1), p => Assert.True(p.x > 25 && p.z < 5));
        Assert.Equal(new Vector3(30, 0, 30), path[^1]);
    }

    [Fact]
    public void KnownStraightGroundStaysStraight()
    {
        var nav = new NavGrid();
        nav.MarkPlayer(new Vector3(0, 0, 0));
        nav.MarkPlayer(new Vector3(20, 0, 0));

        var path = nav.FindPath(new Vector3(0, 0, 0), new Vector3(20, 0, 0));

        Assert.Equal(new Vector3(20, 0, 0), Assert.Single(path));
    }

    [Fact]
    public void WithoutAnyKnownWayItGoesStraight()
    {
        var nav = new NavGrid();
        nav.MarkPoint(new Vector3(0, 0, 0));

        var path = nav.FindPath(new Vector3(0, 0, 0), new Vector3(40, 0, 10));

        Assert.Equal(new Vector3(40, 0, 10), path[^1]);
    }
}
