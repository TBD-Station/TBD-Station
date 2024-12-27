using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Traits.Assorted;

/// <summary>
/// This component allows triggering any emotion at random intervals.
/// </summary>
[RegisterComponent, Access(typeof(EmotionLoopSystem)), AutoGenerateComponentPause]
public sealed partial class EmotionLoopComponent : Component
{
    /// <summary>
    /// A minimum interval between emotions.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan MinTimeBetweenEmotions = TimeSpan.FromSeconds(60);

    /// <summary>
    /// A maximum interval between emotions.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenEmotions = TimeSpan.FromSeconds(90);

    /// <summary> 
    /// A set of emotes that will be randomly picked from.
    /// </summary> 
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<ProtoId<EmotePrototype>> Emotes = new();

    [AutoPausedField]
    public TimeSpan NextIncidentTime;
}
