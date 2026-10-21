using Akaneiro.Server.Game;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

/// <summary>When a mission counts as done, checked against the real mission definitions.</summary>
public sealed class MissionRuleTests
{
    private readonly ITestOutputHelper _output;

    public MissionRuleTests(ITestOutputHelper output) => _output = output;

    private MissionProgress Start(int missionId)
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        return new MissionProgress(player.Session.World.Data.Missions[missionId]);
    }

    /// <summary>Plays every objective of the branches that <paramref name="which"/> picks, in order.</summary>
    private static void Finish(MissionProgress progress, Func<MissionBranch, bool> which)
    {
        foreach (MissionBranch branch in progress.Mission.Branches.Where(which))
            foreach (MissionTask task in branch.Tasks)
                foreach (MissionObjective objective in task.Objectives)
                    for (int i = 0; i < Math.Max(1, objective.Count); i++)
                        progress.Record(objective.TypeId, objective.ObjectId);
    }

    [Fact]
    public void OnLowAMissionIsItsOneTask()
    {
        // Wicked Beasts on Low: 25 Demon Wolf Runts (the video shows "Back to Village" right after).
        MissionProgress low = Start(5111);
        Assert.Single(low.Mission.Branches.SelectMany(b => b.Tasks));
        Finish(low, _ => true);
        Assert.True(low.IsComplete);
    }

    [Fact]
    public void HigherThreatsNeedEveryTaskOfEveryRegularBranch()
    {
        // Rotten Core on Hard: "Rotten Core" (4 tasks) and "To the Heart of it" (1 task).
        MissionProgress hard = Start(5123);
        Assert.Equal(2, hard.Mission.Branches.Count(b => b.IsBonus == 0));

        Finish(hard, b => b == hard.Mission.Branches[0]);
        Assert.False(hard.IsComplete);

        Finish(hard, b => b == hard.Mission.Branches[1]);
        Assert.True(hard.IsComplete);
    }

    [Fact]
    public void TheTasksOfABranchComeOneAfterTheOther()
    {
        // Rotten Core on Medium starts with "Investigate Alpha Wolf Den"; the later tasks do not
        // count before it is done.
        MissionProgress medium = Start(5122);
        MissionTask last = medium.Mission.Branches[0].Tasks.Last();
        foreach (MissionObjective objective in last.Objectives)
            for (int i = 0; i < Math.Max(1, objective.Count); i++)
                Assert.Empty(medium.Record(objective.TypeId, objective.ObjectId));
        Assert.False(medium.IsComplete);
    }

    [Fact]
    public void BonusBranchesAreExtraUnlessTheMissionHasNothingElse()
    {
        // Gashadokuro! consists of bonus branches only: they have to be done.
        MissionProgress bonusOnly = Start(5333);
        Assert.All(bonusOnly.Mission.Branches, b => Assert.Equal(1, b.IsBonus));
        Finish(bonusOnly, b => b == bonusOnly.Mission.Branches[0]);
        Assert.False(bonusOnly.IsComplete);
        Finish(bonusOnly, _ => true);
        Assert.True(bonusOnly.IsComplete);
    }
}
