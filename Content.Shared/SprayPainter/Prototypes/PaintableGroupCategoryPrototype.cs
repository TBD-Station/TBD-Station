using Robust.Shared.Prototypes;

namespace Content.Shared.SprayPainter.Prototypes;

[Prototype]
public sealed partial class PaintableGroupCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}
