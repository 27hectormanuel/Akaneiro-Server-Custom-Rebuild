using Akaneiro.Server.Data;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;
using Vector3 = UnityEngine.Vector3;

namespace Akaneiro.Server.Tests;

public sealed class AbilityTests
{
    private const int DarkHunter = 12101;   // starting ability on the caster: 300 energy, 10 s cooldown
    private const int Caltrops = 11701;     // starting ability: places an object, area radius 5
    private const int SelfTargetType = 3;   // Ability table TargetType: affects the caster

    private readonly ITestOutputHelper _output;

    public AbilityTests(ITestOutputHelper output) => _output = output;

    private TestPlayer InTutorial()
    {
        var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterTutorial();
        return player;
    }

    [Fact]
    public void AbilityCostsEnergyAndGoesOnCooldown()
    {
        using var player = InTutorial();
        var ability = player.World.Data.Abilities[DarkHunter];

        player.UseAbility(DarkHunter, new Vector3(1f, 0f, 2f));
        var begin = player.Take<SUseSkillResult>("On_BeginUseSkill");
        player.Take("On_UseFightSkillResult");

        Assert.Equal(DarkHunter, begin.skillID);
        var cost = (SAttributeChange)begin.attributeChangeVec[0];
        Assert.Equal(EAttributeType.ATTR_CurMP, cost.attributeType.Get());
        Assert.Equal(-ability.MpNeed, cost.value);

        player.UseAbility(DarkHunter, new Vector3(1f, 0f, 2f));
        Assert.Equal(EServerErrorType.eGS_SkillColding, player.Take("On_UseFightSkillFailed").Arg<EServerErrorType>(2).Get());
    }

    [Fact]
    public void AbilityIsUsableAgainAfterItsCooldown()
    {
        using var player = InTutorial();
        var ability = player.World.Data.Abilities[DarkHunter];
        player.UseAbility(DarkHunter, new Vector3(1f, 0f, 2f));
        player.ClearInbox();

        player.Wait(ability.CooldownMs / 1000.0 + 0.1);
        player.Session.CurrentMp = player.World.Data.Level(1).MaxMp; // only the cooldown is under test
        player.UseAbility(DarkHunter, new Vector3(1f, 0f, 2f));

        player.Take("On_BeginUseSkill");
        Assert.False(player.Has("On_UseFightSkillFailed"));
    }

    [Fact]
    public void AbilityNeedsEnoughEnergy()
    {
        using var player = InTutorial();
        player.Session.CurrentMp = 0;

        player.UseAbility(DarkHunter, new Vector3(1f, 0f, 2f));

        Assert.Equal(EServerErrorType.eGS_MpInadequate, player.Take("On_UseFightSkillFailed").Arg<EServerErrorType>(2).Get());
    }

    [Fact]
    public void UnlearnedAbilityIsRefused()
    {
        using var player = InTutorial();

        player.UseAbility(10701, new Vector3());

        Assert.Equal(EServerErrorType.eGS_SkillNotExist, player.Take("On_UseFightSkillFailed").Arg<EServerErrorType>(2).Get());
    }

    [Fact]
    public void AreaAbilityDamagesTheMonstersAtTheAimedPoint()
    {
        using var player = InTutorial();
        var monster = player.AnnouncedMonsters()[0];
        player.MoveTo(monster.pos);

        player.UseAbility(Caltrops, monster.pos);

        var result = player.Take<SUseSkillResult>("On_UseFightSkillResult");
        Assert.Contains(result.skillEffectVec.Cast<SSkillEffect>(), e => e.targetID == monster.objectID);
    }

    [Fact]
    public void AbilityWithOnlyADirectionHitsAroundThePlayer()
    {
        using var player = InTutorial();
        var monster = player.AnnouncedMonsters()[0];
        player.MoveTo(monster.pos);

        // Most abilities send a direction, not a place (PlayerAbilityBaseState.SendUseAbilityRequest).
        player.UseAbility(Caltrops, new Vector3(0f, 0f, 1f));

        var result = player.Take<SUseSkillResult>("On_UseFightSkillResult");
        Assert.Contains(result.skillEffectVec.Cast<SSkillEffect>(), e => e.targetID == monster.objectID);
    }

    [Fact]
    public void RangedShotHitsTheNamedTarget()
    {
        using var player = InTutorial();
        var monster = player.AnnouncedMonsters()[0];

        player.UseAbility(Caltrops, new Vector3(0f, 0f, 1f), monster.objectID);

        var result = player.Take<SUseSkillResult>("On_UseFightSkillResult");
        var effect = Assert.Single(result.skillEffectVec.Cast<SSkillEffect>());
        Assert.Equal(monster.objectID, effect.targetID);
    }

    [Fact]
    public void AbilitiesOnTheCasterDoNoDamage()
    {
        using var player = InTutorial();
        var selfAbility = player.World.Data.Abilities.Values.First(a => a.TargetType == SelfTargetType);
        player.Character.Abilities.Add(new AbilityData { Id = selfAbility.Id });
        player.Session.CurrentMp = 100000;
        var monster = player.AnnouncedMonsters()[0];
        player.MoveTo(monster.pos);

        player.UseAbility(selfAbility.Id, monster.pos);

        Assert.Empty(player.Take<SUseSkillResult>("On_UseFightSkillResult").skillEffectVec);
    }

    [Fact]
    public void AbilityObjectAppearsAndDisappearsAfterItsAnimation()
    {
        using var player = InTutorial();

        player.UseAbility(Caltrops, new Vector3(1f, 0f, 2f));

        var placed = player.Take<SSkillObjectEnter>("On_SkillObjectEnter");
        Assert.Equal(Caltrops, placed.skillID);
        player.Take("On_SkillObjectActive");

        player.Wait(player.World.Data.Abilities[Caltrops].AnimDurationMs / 1000.0 + 3);
        player.Take("On_ObjectLeave", m => m.Arg<vectorInt>(0).Contains(placed.objectID));
    }
}
