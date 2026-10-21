using Akaneiro.Server.Data;
using Akaneiro.Server.Game;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

/// <summary>Talent points, the ability trainer and weapon/armor masteries.</summary>
public sealed class TrainerTests
{
    private const int Cunning = 4; // the test character's discipline (TestPlayer style 4)

    private readonly ITestOutputHelper _output;

    public TrainerTests(ITestOutputHelper output) => _output = output;

    private TestPlayer InVillage(int level = 1, int karma = 1_000_000)
    {
        var player = new TestPlayer(_output).LoginNewCharacter();
        player.Session.Character.Level = level;
        player.Session.Account.Karma = karma;
        player.EnterScene("Hub_Village");
        player.ClearInbox();
        return player;
    }

    private static int Stat(TestPlayer player, int attribute) =>
        player.Take<vectorAttrChange>("On_ChangeCharacterAttribNTF").Cast<SAttributeChange>()
            .Single(a => a.attributeType.Get() == attribute).value;

    /// <summary>A rank I ability of the discipline the character does not know yet, with the stat it needs.</summary>
    private static GameData.AbilityRow Learnable(TestPlayer player, int maxStat) =>
        player.Session.World.Data.Abilities.Values
            .Where(a => a.Discipline == Cunning && a.Rank == 1 && a.KarmaCost > 0 && a.RequiredStat <= maxStat)
            .First(a => player.Session.Character.Abilities.All(k => k.Id / 100 != a.Kind));

    // ---- talents ----------------------------------------------------------------------------

    [Fact]
    public void EachLevelAfterTheFirstBringsATalentPoint()
    {
        using var player = InVillage(level: 3);

        player.Send(ProtocolGame_SendRequest.hasAssignedTalentPointReq());

        // With points left the client opens its level-up screen (CurLV > spent + 1).
        Assert.Equal(new object[] { 0, 0, 0, 2 }, player.Take("On_hasAssignedTalentPointAck").Args);
    }

    [Fact]
    public void AssigningAPointRaisesTheBaseStats()
    {
        using var player = InVillage(level: 2);

        player.Send(ProtocolGame_SendRequest.assignTalentReq(1, 0, 0)); // prowess: +30 POW, +10 DEF, +20 SKILL

        Assert.Equal(EServerErrorType.eSuccess, player.Take<EServerErrorType>("On_assignTalentAck").Get());
        // Cunning start point (10/20/30) plus one prowess point.
        Assert.Equal(40, Stat(player, EAttributeType.ATTR_Power));
        Assert.Equal(1, player.Session.Character.TalentProwess);
        player.Send(ProtocolGame_SendRequest.hasAssignedTalentPointReq());
        Assert.Equal(new object[] { 1, 0, 0, 0 }, player.Take("On_hasAssignedTalentPointAck").Args);
    }

    [Fact]
    public void NoMorePointsThanEarned()
    {
        using var player = InVillage(level: 2);

        player.Send(ProtocolGame_SendRequest.assignTalentReq(0, 2, 0));

        Assert.Equal(EServerErrorType.eLeftTalentPoint_lack, player.Take<EServerErrorType>("On_assignTalentAck").Get());
        Assert.Equal(0, player.Session.Character.TalentFortitude);
    }

    // ---- abilities ----------------------------------------------------------------------------

    [Fact]
    public void LearningAnAbilityCostsKarmaAndAddsIt()
    {
        using var player = InVillage();
        GameData.AbilityRow ability = Learnable(player, maxStat: 30); // a new cunning character has 30 SKILL

        player.Send(ProtocolGame_SendRequest.StudyAbility(ability.Id, ability.Id, new EMoneyType(EMoneyType.eMoneyType_SK)));

        Assert.Equal(new object[] { ability.Id, ability.Id }, player.Take("On_StudyAbilitySucceed").Args);
        Assert.Equal(ability.Id, player.Take<int>("On_AddNewAbilityNotify"));
        Assert.Equal(1_000_000 - ability.KarmaCost, player.Session.Account.Karma);
        Assert.Contains(player.Session.Character.Abilities, a => a.Id == ability.Id);
    }

    [Fact]
    public void AbilitiesNeedTheirDisciplineStat()
    {
        using var player = InVillage();
        GameData.AbilityRow tooHard = player.Session.World.Data.Abilities.Values
            .First(a => a.Discipline == Cunning && a.Rank == 1 && a.KarmaCost > 0 && a.RequiredStat > 30
                        && player.Session.Character.Abilities.All(k => k.Id / 100 != a.Kind));

        player.Send(ProtocolGame_SendRequest.StudyAbility(tooHard.Id, tooHard.Id, new EMoneyType(EMoneyType.eMoneyType_SK)));

        var failed = player.Take("On_StudyAbilityFailed");
        Assert.Equal(EServerErrorType.eGS_LessLevel, failed.Arg<EServerErrorType>(0).Get());
        Assert.Equal(1_000_000, player.Session.Account.Karma);
    }

    [Fact]
    public void RankTwoReplacesRankOneAlsoOnTheShortcutBar()
    {
        using var player = InVillage();
        player.Session.Character.TalentCunning = 20; // plenty of SKILL
        player.Session.Character.Level = 30;
        AbilityData known = player.Session.Character.Abilities.First(a => player.Session.Character.Shortcuts.Any(s => s.AbilityId == a.Id));
        int rankTwo = known.Id + 1;
        Assert.True(player.Session.World.Data.Abilities.ContainsKey(rankTwo));

        player.Send(ProtocolGame_SendRequest.StudyAbility(rankTwo, rankTwo, new EMoneyType(EMoneyType.eMoneyType_SK)));

        Assert.True(player.Has("On_StudyAbilitySucceed"));
        Assert.DoesNotContain(player.Session.Character.Abilities, a => a.Id == rankTwo - 1);
        Assert.Contains(player.Session.Character.Shortcuts, s => s.AbilityId == rankTwo);
    }

    [Fact]
    public void RanksAreLearnedInOrder()
    {
        using var player = InVillage();
        player.Session.Character.TalentCunning = 20;
        GameData.AbilityRow rankOne = Learnable(player, maxStat: 1000);

        player.Send(ProtocolGame_SendRequest.StudyAbility(rankOne.Id + 1, rankOne.Id + 1, new EMoneyType(EMoneyType.eMoneyType_SK)));

        Assert.Equal(EServerErrorType.eGS_SkillIDInvalid, player.Take("On_StudyAbilityFailed").Arg<EServerErrorType>(0).Get());
    }

    // ---- masteries ----------------------------------------------------------------------------

    [Fact]
    public void ADisciplineStartsWithItsTwoMasteries()
    {
        using var player = InVillage();

        player.Send(ProtocolGame_SendRequest.RoleMasteryListReq());

        var masteries = player.Take<vectorMasteryLevelInfo>("On_RoleMasteryListAck").Cast<SMasteryLevelInfo>()
            .Select(m => (m.masteryType.Get(), m.level)).ToList();
        // Cunning: Dueling I (one-handed) and Light Armor I.
        Assert.Equal(new[] { (EMasteryType.e1HWeaponType, 1), (EMasteryType.eLightArmorType, 1) }, masteries);
    }

    [Fact]
    public void AMasteryLevelTakesItsTrainingTime()
    {
        using var player = InVillage();
        player.Session.Character.TalentCunning = 5; // Dueling II needs 120 SKILL

        player.Send(ProtocolGame_SendRequest.masteryLvlupReq(new EMasteryType(EMasteryType.e1HWeaponType)));

        Assert.Equal(EServerErrorType.eSuccess, player.Take("On_masteryLvlupAck").Arg<EServerErrorType>(1).Get());
        var cooldown = player.Take("On_UpdateCoolDown");
        Assert.Equal(1002, cooldown.Arg<int>(1)); // counted under the level being trained
        Assert.False(player.Has("On_AddNewMasteryNotify"));
        Assert.Equal(1_000_000 - 2500, player.Session.Account.Karma);

        // A second training has to wait for the first.
        player.Send(ProtocolGame_SendRequest.masteryLvlupReq(new EMasteryType(EMasteryType.eLightArmorType)));
        Assert.Equal(EServerErrorType.eMasteryError_CoolDown, player.Take("On_masteryLvlupAck").Arg<EServerErrorType>(1).Get());

        player.Wait(10 * 60 + 1); // Dueling II: 10 minutes
        Assert.Equal(1002, player.Take<int>("On_AddNewMasteryNotify"));
        Assert.Equal(2, player.Session.Character.Masteries[EMasteryType.e1HWeaponType]);
    }

    [Fact]
    public void CrystalsFinishATrainingAtOnce()
    {
        using var player = InVillage();
        player.Session.Character.TalentCunning = 5;
        player.Send(ProtocolGame_SendRequest.masteryLvlupReq(new EMasteryType(EMasteryType.e1HWeaponType)));
        player.ClearInbox();

        player.Send(ProtocolGame_SendRequest.SpeedUpCoolDown(new ECooldownType(ECooldownType.eCooldownType_Mastery), 1002,
            new EDecreaseCooldownType(EDecreaseCooldownType.eDecreaseCooldownType_All)));

        Assert.Equal(EServerErrorType.eSuccess, player.Take("On_SpeedUpCoolDownResult").Arg<EServerErrorType>(0).Get());
        Assert.Equal(1002, player.Take<int>("On_AddNewMasteryNotify"));
    }
}
