using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

public sealed class CombatTests
{
    private const string Tutorial = "Hub_Village_Tutorial";

    private readonly ITestOutputHelper _output;

    public CombatTests(ITestOutputHelper output) => _output = output;

    private TestPlayer InTutorial()
    {
        var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterTutorial();
        return player;
    }

    [Fact]
    public void BasicAttackNamesTheWeaponsAbility()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterScene("Hub_Village");

        var attack = player.Attack(0);

        Assert.Equal(10001, attack.skillID); // one-handed basic attack
        Assert.Equal(1, attack.sourceObjectID);
    }

    [Fact]
    public void MonstersAroundThePlayerAreAnnouncedWithHealth()
    {
        using var player = InTutorial();

        var monsters = player.AnnouncedMonsters();

        Assert.NotEmpty(monsters);
        Assert.All(monsters, m => Assert.True(m.objectID >= 1000 && m.hp > 0));
    }

    [Fact]
    public void MonsterDiesAfterSeveralHitsAndLeavesTheScene()
    {
        using var player = InTutorial();
        var monster = player.AnnouncedMonsters()[0];

        int hits = player.Kill(monster.objectID);

        Assert.True(hits >= 1);
        // ObjectLeave is what makes the client play the death animation and remove the corpse.
        player.Take("On_ObjectLeave", m => m.Arg<vectorInt>(0).Contains(monster.objectID));
        Assert.True(player.Take<ulong>("On_AddExpNTF", 1) > 0);
    }

    [Fact]
    public void KarmaDropsWhereTheMonsterDiedAndCanBePickedUp()
    {
        using var player = InTutorial();
        var monster = player.AnnouncedMonsters()[0];
        player.Kill(monster.objectID);

        var shard = player.Take<SMoneyEnter>("On_moneyEnter");
        Assert.True(shard.money > 0);
        Assert.True(shard.pos.x != 0f || shard.pos.z != 0f, "karma without a position");
        Assert.NotEmpty(shard.Distribution); // the client renders one shard per entry

        player.Send(ProtocolBattle_SendRequest.PickupMoneyReq(shard.objectID));
        Assert.Equal(0, player.Take("On_PickupMoneyResult").Arg<EServerErrorType>(1).Get());
        Assert.Equal(shard.money, player.Take<SMoneyInfo>("On_UpdateMoneyInfo").Karma);
    }

    [Fact]
    public void KarmaCannotBePickedUpTwice()
    {
        using var player = InTutorial();
        player.Kill(player.AnnouncedMonsters()[0].objectID);
        var shard = player.Take<SMoneyEnter>("On_moneyEnter");

        player.Send(ProtocolBattle_SendRequest.PickupMoneyReq(shard.objectID));
        player.Send(ProtocolBattle_SendRequest.PickupMoneyReq(shard.objectID));

        var results = player.TakeAll("On_PickupMoneyResult");
        Assert.Equal(0, results[0].Arg<EServerErrorType>(1).Get());
        Assert.NotEqual(0, results[1].Arg<EServerErrorType>(1).Get());
    }

    [Fact]
    public void CharacterLevelsUpWhenExperienceReachesTheNextLevel()
    {
        using var player = InTutorial();
        player.Character.Exp = (ulong)player.World.Data.Level(2).ExpTotal - 1;

        player.Kill(player.AnnouncedMonsters()[0].objectID);

        Assert.Equal(2, player.Take<int>("On_LevelUp"));
        Assert.Equal(2, player.Character.Level);
        // New attributes for the new level, and health refilled.
        player.Take("On_UpdateBaseAttr");
        var attributes = player.Take<vectorAttrChange>("On_ChangeCharacterAttribNTF");
        int health = attributes.Cast<SAttributeChange>().First(a => a.attributeType.Get() == EAttributeType.ATTR_CurHP).value;
        Assert.Equal(player.World.Data.Level(2).MaxHp, health);
    }

    [Fact]
    public void AttackingNothingDoesNoHarm()
    {
        using var player = InTutorial();

        var attack = player.Attack(987654);

        Assert.Empty(attack.skillEffectVec);
    }
}

public sealed class ArmorTests
{
    private readonly ITestOutputHelper _output;

    public ArmorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void StarterGearAndDefenseMakeTheArmor()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        var data = player.Session.World.Data;

        // Cunning starter armor on level 1 (modifier 0.05): head 400, chest 800, legs 500 -> 85;
        // plus 20 DEF from the cunning start point * 5 = 100.
        Assert.Equal(185, Akaneiro.Server.Game.Combat.Armor(player.Session.Character, data));
        Assert.InRange(Akaneiro.Server.Game.Combat.DamageReduction(player.Session.Character, data), 0.08f, 0.09f);
    }

    [Fact]
    public void BetterArmorTakesMoreOfTheHit()
    {
        using var player = new TestPlayer(_output).LoginNewCharacter();
        var data = player.Session.World.Data;
        float before = Akaneiro.Server.Game.Combat.DamageReduction(player.Session.Character, data);

        foreach (var piece in player.Session.Character.Equipment.Where(e => data.ItemArmor.ContainsKey(e.ItemId)))
            piece.Level = 20;

        Assert.True(Akaneiro.Server.Game.Combat.DamageReduction(player.Session.Character, data) > before);
    }
}
