using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.VendingMachines;

namespace Content.IntegrationTests.Tests.Vending;

public sealed class VendingInteractionTest : InteractionTest
{
    private const string VendingMachineProtoId = "InteractionTestVendingMachine";

    private const string RestockBoxProtoId = "InteractionTestRestockBox";

    [TestPrototypes]
    private const string TestPrototypes = $@"
- type: vendingMachineInventory
  id: InteractionTestVendingInventory
  startingInventory:
    PassengerPDA: 5

- type: entity
  parent: BaseVendingMachineRestock
  id: {RestockBoxProtoId}
  components:
  - type: VendingMachineRestock
    canRestock:
    - InteractionTestVendingInventory
  - type: Sprite
    layers:
    - state: base
    - state: green_bit
      shader: unshaded
    - state: refill_ptech

- type: entity
  id: {VendingMachineProtoId}
  parent: VendingMachine
  components:
  - type: VendingMachine
    pack: InteractionTestVendingInventory
    offState: off
    brokenState: broken
    normalState: normal-unshaded
    ejectState: eject-unshaded
    denyState: deny-unshaded
  - type: Sprite
    sprite: Structures/Machines/VendingMachines/cart.rsi
    layers:
    - state: off
      map: [ enum.VendingMachineVisualLayers.Base ]
    - state: off
      map: [ enum.VendingMachineVisualLayers.BaseUnshaded ]
      shader: unshaded
    - state: panel
      map: [ enum.WiresVisualLayers.MaintenancePanel ]
";

    [Test]
    public async Task RestockTest()
    {
        var vendingSystem = SEntMan.System<VendingMachineSystem>();

        await SpawnTarget(VendingMachineProtoId);
        var vendorEnt = SEntMan.GetEntity(Target.Value);

        var items = vendingSystem.GetAllInventory(vendorEnt);

        Assert.That(items, Is.Not.Empty, $"{VendingMachineProtoId} spawned with no items.");
        Assert.That(items.First().Amount, Is.EqualTo(5), $"{VendingMachineProtoId} spawned with unexpected item count.");

        // Try to restock with the maintenance panel closed (nothing happens)
        await InteractUsing(RestockBoxProtoId);

        Assert.That(items.First().Amount, Is.EqualTo(5), "Restocked without opening maintenance panel.");

        // Open the maintenance panel
        await InteractUsing(Screw);

        // Restock the machine
        await InteractUsing(RestockBoxProtoId);

        Assert.That(items.First().Amount, Is.EqualTo(10), "Restocking resulted in unexpected item count.");
    }
}
