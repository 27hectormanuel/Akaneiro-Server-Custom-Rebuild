using Akaneiro.Server.Data;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

/// <summary>The craftsman's upgrades (UpgradeRecipes.Recipes).</summary>
public sealed class CraftingTests
{
    private const byte Bag = 2;
    private const int Katana = 1001, LevelMaterial = 9001, FrostMaterial = 8001;

    private readonly ITestOutputHelper _output;

    public CraftingTests(ITestOutputHelper output) => _output = output;

    private TestPlayer InVillage(int karma = 100000)
    {
        var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterScene("Hub_Village");
        player.Session.Account.Karma = karma;
        player.ClearInbox();
        return player;
    }

    private static void Craft(TestPlayer player, uint slot, int craftType, int current, int money) =>
        player.Send(ProtocolGame_SendRequest.CraftingItem(Bag, slot, new ECraftType(craftType), new EMoneyType(money), current,
            new mapCraftMaterial(), buyMaterial: false));

    [Fact]
    public void RaisingTheLevelUsesTheRecipesMaterial()
    {
        using var player = InVillage();
        ItemData katana = new ItemData { ItemId = Katana, Level = 1, Slot = 0 };
        player.Session.Character.Bag.Add(katana);
        player.Session.Character.Bag.Add(new ItemData { ItemId = LevelMaterial, Count = 3, Slot = 1 });

        // "Level 2": one 9001, 5 crystals with a sure success.
        Craft(player, 0, ECraftType.eCraftType_Level, current: 1, EMoneyType.eMoneyType_FK);

        var ok = player.Take("On_CraftingItemOK");
        Assert.Equal(2u, ok.Arg<SItemInfo>(2).level);
        Assert.Equal(2, katana.Level);
        Assert.Equal(2, player.Session.Character.Bag.Single(i => i.ItemId == LevelMaterial).Count);
    }

    [Fact]
    public void KarmaCraftingCostsItsPrice()
    {
        using var player = InVillage(karma: 1000);
        player.Session.Character.Bag.Add(new ItemData { ItemId = Katana, Level = 2, Slot = 0 });
        player.Session.Character.Bag.Add(new ItemData { ItemId = LevelMaterial, Count = 1, Slot = 1 });

        // "Level 3": one 9001 and 200 karma at 99 %.
        Craft(player, 0, ECraftType.eCraftType_Level, current: 2, EMoneyType.eMoneyType_SK);

        Assert.Equal(800, player.Session.Account.Karma);
        Assert.DoesNotContain(player.Session.Character.Bag, i => i.ItemId == LevelMaterial);
        Assert.True(player.Has("On_CraftingItemOK") || player.Has("On_CraftingItemFailed"));
    }

    [Fact]
    public void AnElementGoesUpOneStep()
    {
        using var player = InVillage();
        ItemData katana = new ItemData { ItemId = Katana, Level = 5, Element = 102, Slot = 0 }; // Frost II
        player.Session.Character.Bag.Add(katana);
        player.Session.Character.Bag.Add(new ItemData { ItemId = FrostMaterial, Count = 5, Slot = 1 });

        Craft(player, 0, ECraftType.eCraftType_WeaponElement, current: 102, EMoneyType.eMoneyType_FK);

        Assert.True(player.Has("On_CraftingItemOK"));
        Assert.Equal(103, katana.Element); // Frost III, 5 x 8001
        Assert.DoesNotContain(player.Session.Character.Bag, i => i.ItemId == FrostMaterial);
    }

    [Fact]
    public void AttributesAnItemDoesNotHaveCannotBeRaised()
    {
        using var player = InVillage();
        player.Session.Character.Bag.Add(new ItemData { ItemId = Katana, Level = 5, Slot = 0 });

        Craft(player, 0, ECraftType.eCraftType_WeaponElement, current: 0, EMoneyType.eMoneyType_FK);

        Assert.True(player.Has("On_CraftingItemFailed"));
    }

    [Fact]
    public void KarmaCraftingNeedsTheMaterials()
    {
        using var player = InVillage();
        player.Session.Character.Bag.Add(new ItemData { ItemId = Katana, Level = 1, Slot = 0 });

        Craft(player, 0, ECraftType.eCraftType_Level, current: 1, EMoneyType.eMoneyType_SK);

        Assert.Equal(EServerErrorType.eItemCraftError_ParamError, player.Take<EServerErrorType>("On_CraftingItemFailed").Get());
        Assert.Equal(100000, player.Session.Account.Karma);
    }
}
