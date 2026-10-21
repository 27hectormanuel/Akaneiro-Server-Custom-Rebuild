using Akaneiro.Server.Game;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

public sealed class MissionTests
{
    private const int TutorialMission = 5011;
    private const int InsaneWolf = 212;

    private readonly ITestOutputHelper _output;

    public MissionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheVillagePlacesItsNpcsIncludingTheBountyBoard()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterScene("Hub_Village");

        var npcs = player.Inbox.Where(m => m.Name == "On_npcEnter").Select(m => m.Arg<SNpcEnter>(0)).ToList();

        // The nine prefabs of the client's "npc info.prefab": bounty board (1001), vendor, ability and
        // spirit trainer, elder, craftsman, rare vendor, well and stash.
        Assert.Equal(new[] { 1001, 1002, 1003, 1004, 1005, 1007, 1009, 1010, 1011 }, npcs.Select(n => n.npcID).OrderBy(id => id));
        Assert.Equal(npcs.Count, npcs.Select(n => n.objectID).Distinct().Count());
        Assert.DoesNotContain(npcs, n => n.objectID == Session.PlayerObjectId);
        var board = npcs.Single(n => n.npcID == 1001);
        Assert.InRange(board.pos.x, 82f, 84f); // CH_BountyMaster stands at (83.0, 11.2, 74.5)
        Assert.InRange(board.pos.z, 73f, 76f);
    }

    [Fact]
    public void MissionScenesPlaceNoVillageNpcs()
    {
        // Statues and the dying tutorial villager (DIALOG) are part of the level itself.
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.AcceptMission(TutorialMission);
        player.EnterScene("Hub_Village_Tutorial");

        Assert.DoesNotContain(player.Inbox, m => m.Name == "On_npcEnter");
    }

    [Fact]
    public void KillingTheFirstWolfCompletesTheFirstTask()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.AcceptMission(TutorialMission);
        player.EnterTutorial();
        var wolf = player.AnnouncedMonsters().First(m => m.npcID == InsaneWolf);

        player.Kill(wolf.objectID);

        var task = player.Take("On_taskCompleteNTF");
        Assert.Equal(TutorialMission, task.Arg<int>(0));
        // The branch's BranchID, by which the client finds the task's name (not "Unkonw Task").
        var branch = TestData.Data.Missions[TutorialMission].Branches[0];
        Assert.Equal(branch.BranchId, task.Arg<int>(1));
        Assert.Contains(branch.Tasks, t => t.TaskId == task.Arg<int>(2));
    }

    [Fact]
    public void MissionListOffersTheMissions()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();

        player.Send(ProtocolGame_SendRequest.MissionListReq());

        var maps = player.Take<vectorSAcceptMissionRelate>("On_MissionListAck").Cast<SAcceptMissionRelate2>().ToList();

        // The mission screen shows a map only when an entry carries its base id (A1_M1 = 5110) and
        // describes it with base id + 1 from MissionDescription (5111 "Wicked Beasts").
        Assert.All(maps, m => Assert.Equal(0, m.missionID % 10));
        Assert.Equal(maps.Count, maps.Select(m => m.missionID).Distinct().Count());
        var a1m1 = maps.Single(m => m.missionID == 5110);
        Assert.Equal(1, a1m1.canAcceptMaxLvl); // only "Low" is open on a new character
        Assert.Equal(0, a1m1.star);
    }

    [Fact]
    public void BeatingAThreatUnlocksTheNextOne()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.Session.Character.CompletedMissions.AddRange(new[] { 5111, 5112 });

        player.Send(ProtocolGame_SendRequest.MissionListReq());

        var a1m1 = player.Take<vectorSAcceptMissionRelate>("On_MissionListAck").Cast<SAcceptMissionRelate2>().Single(m => m.missionID == 5110);
        Assert.Equal(3, a1m1.canAcceptMaxLvl);
        Assert.Equal(1 | 2, a1m1.star);
        Assert.Equal(1, a1m1.IsHaveCompleted);
    }

    [Fact]
    public void TheHuntButtonAcceptsBaseIdPlusThreat()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();

        player.AcceptMission(5110 + 1);

        Assert.Equal(5111, player.Session.Mission.Mission.Id);
    }

    [Fact]
    public void ObjectivesTheClientReportsAreAcknowledged()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.AcceptMission(TutorialMission);

        player.Send(ProtocolGame_SendRequest.exploretaskcompleteReq(TutorialMission, 0, 4, 1, 83));
        Assert.Equal(0, player.Take("On_exploretaskcompleteAck").Arg<EServerErrorType>(4).Get());

        player.Send(ProtocolGame_SendRequest.conversationNpcCheckReq(TutorialMission, 4, 1, 83, 1001));
        Assert.Equal(0, player.Take("On_conversationNpcCheckAck").Arg<EServerErrorType>(5).Get());
    }

    [Fact]
    public void MissionCanBeGivenUp()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.AcceptMission(TutorialMission);

        player.Send(ProtocolGame_SendRequest.MissionFailReq(TutorialMission));

        Assert.Equal(1, player.Take("On_MissionFailAck").Arg<int>(1));
        Assert.Null(player.Session.Mission);
    }

    [Fact]
    public void TasksAreDoneOneAfterTheOther()
    {
        // 5011: 1 Clear Docks (a wolf), 2 Clear Village (2 wolves), 3 Defeat Amano Harbinger, ...,
        // 6 Kill Amano Harbinger. A Harbinger killed early counts for nothing, like in the client.
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.AcceptMission(TutorialMission);
        player.EnterTutorial();
        var battle = player.Session.Battle;

        battle.RecordMissionProgress(MissionObjectiveType.Hunt, 125); // Harbinger before the wolves
        Assert.False(player.Has("On_taskCompleteNTF"));

        battle.RecordMissionProgress(MissionObjectiveType.Hunt, InsaneWolf);
        Assert.Equal(1, player.Take("On_taskCompleteNTF").Arg<int>(2));
        battle.RecordMissionProgress(MissionObjectiveType.Hunt, InsaneWolf);
        battle.RecordMissionProgress(MissionObjectiveType.Hunt, InsaneWolf);
        Assert.Equal(2, player.Take("On_taskCompleteNTF").Arg<int>(2));
        battle.RecordMissionProgress(MissionObjectiveType.Hunt, 125);
        Assert.Equal(3, Assert.Single(player.TakeAll("On_taskCompleteNTF")).Arg<int>(2));
    }
}
