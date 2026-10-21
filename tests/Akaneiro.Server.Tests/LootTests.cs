using Akaneiro.Server.Data;
using Akaneiro.Server.Tests.Harness;
using Xunit.Abstractions;

namespace Akaneiro.Server.Tests;

public sealed class LootTests
{
    private readonly ITestOutputHelper _output;

    public LootTests(ITestOutputHelper output) => _output = output;

    /// <summary>Kills the monsters of the first mission map one by one until one drops an item.</summary>
    private static SItemEnter HuntForLoot(TestPlayer player)
    {
        // Mission monsters wait behind combat triggers, so walk through the level like a player:
        // into the next trigger, then fight whatever came.
        var triggers = new Queue<Akaneiro.Server.Game.TriggerInfo>(player.Scene.Triggers.Where(t => t.TriggerOn));
        int killed = 0;
        while (true)
        {
            var monster = player.Scene.Monsters.FirstOrDefault(m => m.IsActive);
            if (monster == null)
            {
                if (triggers.Count == 0)
                    throw new Xunit.Sdk.XunitException($"no monster dropped anything ({killed} killed)");
                player.MoveTo(triggers.Dequeue().Center);
                player.Wait(5); // packs arrive one after the other
                continue;
            }

            player.MoveTo(monster.Position); // walking up is what makes the server announce it
            player.Kill(monster.ObjectId);
            killed++;
            if (player.Has("On_ItemEnter"))
                return player.Take<SItemEnter>("On_ItemEnter");
        }
    }

    private TestPlayer OnFirstMission()
    {
        var player = new TestPlayer(_output).LoginNewCharacter();
        player.EnterScene("A1_M1");
        return player;
    }

    [Fact]
    public void MonstersDropItemsIntoTheWorld()
    {
        using var player = OnFirstMission();

        var drop = HuntForLoot(player);

        Assert.True(drop.iteminfo.ID > 0);
        Assert.True(drop.pos.x != 0f || drop.pos.z != 0f, "item without a position");
    }

    [Fact]
    public void PickedUpItemGoesIntoTheBagAndLeavesTheGround()
    {
        using var player = OnFirstMission();
        var drop = HuntForLoot(player);

        player.Send(ProtocolBattle_SendRequest.PickupItemReq(drop.objectID));

        Assert.Equal(0, player.Take("On_PickupItemSuccAck").Arg<EServerErrorType>(1).Get());
        var update = player.Take("On_NotifyItemUpdate");
        Assert.Equal(2, update.Arg<byte>(0)); // the bag
        Assert.Equal(drop.iteminfo.ID, update.Arg<SItemInfo>(1).ID);
        // Only ObjectLeave takes the item (and the arrow above it) out of the world.
        player.Take("On_ObjectLeave", m => m.Arg<vectorInt>(0).Contains(drop.objectID));
        Assert.Contains(player.Character.Bag, i => i.ItemId == drop.iteminfo.ID);
    }

    [Fact]
    public void ItemCannotBePickedUpTwice()
    {
        using var player = OnFirstMission();
        var drop = HuntForLoot(player);

        player.Send(ProtocolBattle_SendRequest.PickupItemReq(drop.objectID));
        player.Send(ProtocolBattle_SendRequest.PickupItemReq(drop.objectID));

        player.Take("On_PickupItemSuccAck");
        player.Take("On_PickupItemFailed");
    }

    [Fact]
    public void FullBagLeavesTheItemOnTheGround()
    {
        using var player = OnFirstMission();
        var drop = HuntForLoot(player);
        for (int slot = 0; slot < 40; slot++)
            player.Character.Bag.Add(new ItemData { ItemId = 1, Slot = slot });

        player.Send(ProtocolBattle_SendRequest.PickupItemReq(drop.objectID));

        Assert.Equal(EServerErrorType.eItemError_BagFull, player.Take("On_PickupItemFailed").Arg<EServerErrorType>(1).Get());
        Assert.False(player.Has("On_ObjectLeave", m => m.Arg<vectorInt>(0).Contains(drop.objectID)));
    }
}
