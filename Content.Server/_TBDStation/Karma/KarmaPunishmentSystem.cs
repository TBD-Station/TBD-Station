using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Timing;
using Content.Server.Database;
using Robust.Server.Player;
using Content.Shared._TBDStation.ServerKarma.Events;
using Content.Server.Administration.Commands;
using Content.Server.Administration.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Electrocution;
using Content.Server.Explosion.EntitySystems;
using Content.Server.GhostKick;
using Content.Server.Medical;
using Content.Server.Nutrition.EntitySystems;
using Content.Server.Pointing.Components;
using Content.Server.Polymorph.Systems;
using Content.Server.Popups;
using Content.Server.Speech.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Tabletop;
using Content.Server.Temperature.Components;
using Content.Shared.Administration.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Clumsy;
using Content.Shared.Clothing.Components;
using Content.Shared.Cluwne;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Electrocution;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared.Speech.Components;
using Content.Server.Administration.Systems;
using Content.Shared.Administration.Logs;
using Content.Server.Chat.Managers;

namespace Content.Server._TBDStation.ServerKarma;
public sealed partial class KarmaPunishmentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly BodySystem _bodySystem = default!;
    [Dependency] private readonly CreamPieSystem _creamPieSystem = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocutionSystem = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorageSystem = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly FlammableSystem _flammableSystem = default!;
    [Dependency] private readonly GhostKickManager _ghostKickManager = default!;
    [Dependency] private readonly SharedGodmodeSystem _sharedGodmodeSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private readonly PolymorphSystem _polymorphSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThresholdSystem = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly VomitSystem _vomitSystem = default!;
    [Dependency] private readonly WeldableSystem _weldableSystem = default!;
    [Dependency] private readonly SharedContentEyeSystem _eyeSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SuperBonkSystem _superBonkSystem = default!;
    [Dependency] private readonly SlipperySystem _slipperySystem = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly ServerKarmaManager _karmaMan = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    // public void PostInject()
    // {
    // }
    public override void Initialize()
    {
        base.Initialize();
        _karmaMan.KarmaChange += DeterminePunishment;
    }
    public override void Shutdown()
    {
        base.Shutdown();
        _karmaMan.KarmaChange -= DeterminePunishment;
    }

    private void DeterminePunishment(PlayerKarmaChangeEvent ev)
    {
        if (ev.NewKarma >= ev.OldKarma || !ev.UserSes.AttachedEntity.HasValue)
            return;
        EntityUid target = ev.UserSes.AttachedEntity.Value;
        if (!EntityManager.TryGetComponent(target, out ActorComponent? actor))
            return;
        var player = actor.PlayerSession;
        // 1984.
        if (HasComp<MapComponent>(target) || HasComp<MapGridComponent>(target))
            return;

        // Pick punishment type
        var (nothing, bitter, harm, harsh, nasty, kill) = ev.NewKarma switch
        {
            > 0 =>                  (0, 0, 0, 0, 0, 0),
            > -200 and <= 0 =>      (90, 10, 0, 0, 0, 0),
            > -600 and <= -200 =>   (75, 15, 5, 5, 0, 0),
            > -850 and <= -600 =>   (60, 15, 10, 10, 2, 3),
            > -1000 and <= -850 =>  (40, 15, 15, 10, 10, 10),
            > -1200 and <= -1000 => (40, 20, 20, 10, 0, 10),
            <= -1200 =>             (25, 0, 25, 0, 25, 25),
        };

        int totalWeight = nothing + bitter + harsh + nasty + harm + kill;
        int attempts = 0;
        bool gotSmitted = false;
        while (!gotSmitted && attempts++ < 9)
        {
            int i = _random.Next(totalWeight);
            if (i < nothing)
                return;
            i -= nothing;

            Func<(Func<EntityUid, bool>, string)> smiteGrabFunction;
            if (i < bitter)
                smiteGrabFunction = AnyBitterSmite;
            else if (i < bitter + harsh)
                smiteGrabFunction = AnyHarshSmite;
            else if (i < bitter + harsh + nasty)
                smiteGrabFunction = AnyNastySmite;
            else if (i < bitter + harsh + nasty + harm)
                smiteGrabFunction = AnyHarmSmite;
            else
                smiteGrabFunction = AnyKillSmite;

            // Activate specific punishment
            var (smiteFunc, smiteName) = smiteGrabFunction();
            gotSmitted = smiteFunc(target);

            LogImpact impact = (smiteGrabFunction == AnyKillSmite || smiteGrabFunction == AnyNastySmite) ? LogImpact.High
                : (smiteGrabFunction == AnyHarshSmite || smiteGrabFunction == AnyHarmSmite) ? LogImpact.Medium
                : LogImpact.Low;
            _adminLogger.Add(LogType.Karma,
                impact,
                $"{ToPrettyString(target):actor} smitted by {smiteName}, and gotSmitted={gotSmitted}");
            if (gotSmitted)
                _chatManager.DispatchServerMessage(player, "Your actions have consequences!", true);
        }
    }

    #region Bitter
    /// Stuff that hardly sucks and is easily fixable
    private (Func<EntityUid, bool>, string) AnyBitterSmite()
    {
        Func<EntityUid, bool> BitterCreamPie = target =>
        {
            if (TryComp<CreamPiedComponent>(target, out var creamPied))
            {
                _creamPieSystem.SetCreamPied(target, creamPied, true);
                return true;
            }
            return false;
        };
        var smites = new (Func<EntityUid, bool>, string)[]
        {
            (BitterCreamPie, nameof(BitterCreamPie)),
            (BitterZoom, nameof(BitterZoom)),
            (BitterSlip, nameof(BitterSlip)),
            (BitterSlow, nameof(BitterSlow)),
            (BitterSpeakBackwards, nameof(BitterSpeakBackwards)),
        };

        var (smite, smiteName) = smites[_random.Next(smites.Length)];
        return (smite, smiteName);
    }

    private bool BitterZoom(EntityUid target)
    {
        var eye = EnsureComp<ContentEyeComponent>(target);
        _eyeSystem.SetZoom(target, eye.TargetZoom * -1, ignoreLimits: true);
        return true;
    }

    private bool BitterSlip(EntityUid target)
    {
        var hadSlipComponent = EnsureComp(target, out SlipperyComponent slipComponent);
        if (!hadSlipComponent)
        {
            slipComponent.SuperSlippery = true;
            slipComponent.ParalyzeTime = 5;
            slipComponent.LaunchForwardsMultiplier = 20;
        }

        _slipperySystem.TrySlip(target, slipComponent, target, requiresContact: false);
        if (!hadSlipComponent)
        {
            RemComp(target, slipComponent);
        }
        return true;
    }

    private bool BitterSlow(EntityUid target)
    {
        float slowDown = 0.95f;
        var movementSpeed = EnsureComp<MovementSpeedModifierComponent>(target);
        (movementSpeed.BaseSprintSpeed, movementSpeed.BaseWalkSpeed) = (movementSpeed.BaseSprintSpeed * slowDown, movementSpeed.BaseWalkSpeed * slowDown);

        Dirty(target, movementSpeed);

        _popupSystem.PopupEntity("You feel a bit slower", target,
            target, PopupType.LargeCaution);
        return true;
    }

    private bool BitterSpeakBackwards(EntityUid target)
    {
        EnsureComp<BackwardsAccentComponent>(target);
        return true;
    }
    #endregion
    #region Harsh
    // Quite annoying things that can be immpossible to fix
    private (Func<EntityUid, bool>, string) AnyHarshSmite()
    {
        Func<EntityUid, bool> HarshCluwne = target => { EnsureComp<CluwneComponent>(target); return true; };
        var smites = new (Func<EntityUid, bool>, string)[]
        {
            (HarshCluwne, nameof(HarshCluwne)),
            (HarshByeHand, nameof(HarshByeHand)),
            (HarshLockInLocker, nameof(HarshLockInLocker)),
            (HarshMaid, nameof(HarshMaid)),
            (HarshMessySpeach, nameof(HarshMessySpeach)),
            (HarshSlow, nameof(HarshSlow)),
            (HarshSwapRunAndWalk, nameof(HarshSwapRunAndWalk)),
        };

        var (smite, smiteName) = smites[_random.Next(smites.Length)];
        return (smite, smiteName);
    }


    private bool HarshMessySpeach(EntityUid target)
    {
        EnsureComp<BarkAccentComponent>(target);
        EnsureComp<BleatingAccentComponent>(target);
        EnsureComp<FrenchAccentComponent>(target);
        EnsureComp<GermanAccentComponent>(target);
        EnsureComp<LizardAccentComponent>(target);
        EnsureComp<MobsterAccentComponent>(target);
        EnsureComp<MothAccentComponent>(target);
        EnsureComp<OwOAccentComponent>(target);
        EnsureComp<SkeletonAccentComponent>(target);
        EnsureComp<SouthernAccentComponent>(target);
        EnsureComp<SpanishAccentComponent>(target);
        EnsureComp<StutteringAccentComponent>(target);
        EnsureComp<PirateAccentComponent>(target);

        if (_random.Next(0, 8) == 0)
        {
            EnsureComp<BackwardsAccentComponent>(target); // was asked to make this at a low chance idk
        }
        return true;
    }

    private bool HarshByeHand(EntityUid target)
    {
        if (TryComp<BodyComponent>(target, out var body1))
        {
            var baseXform1 = Transform(target);
            foreach (var part in _bodySystem.GetBodyChildrenOfType(target, BodyPartType.Hand, body1))
            {
                _transformSystem.AttachToGridOrMap(part.Id);
                break;
            }
            _popupSystem.PopupEntity(Loc.GetString("admin-smite-remove-hands-self"), target,
                target, PopupType.LargeCaution);
            _popupSystem.PopupCoordinates(Loc.GetString("admin-smite-remove-hands-other", ("name", target)), baseXform1.Coordinates,
                Filter.PvsExcept(target), true, PopupType.Medium);
            return true;
        }
        return false;
    }

    private bool HarshMaid(EntityUid target)
    {
        SetOutfitCommand.SetOutfit(target, "JanitorMaidGear", EntityManager, (_, clothing) =>
        {
            if (HasComp<ClothingComponent>(clothing))
                EnsureComp<UnremoveableComponent>(clothing);
            EnsureComp<ClumsyComponent>(target);
        });
        return true;
    }

    private bool HarshSwapRunAndWalk(EntityUid target)
    {
        var movementSpeed = EnsureComp<MovementSpeedModifierComponent>(target);
        (movementSpeed.BaseSprintSpeed, movementSpeed.BaseWalkSpeed) = (movementSpeed.BaseWalkSpeed, movementSpeed.BaseSprintSpeed);

        Dirty(target, movementSpeed);

        _popupSystem.PopupEntity(Loc.GetString("admin-smite-run-walk-swap-prompt"), target,
            target, PopupType.LargeCaution);
        return true;
    }

    private bool HarshLockInLocker(EntityUid target)
    {
        var xform = Transform(target);
        var locker = Spawn("ClosetMaintenance", xform.Coordinates);
        if (TryComp<EntityStorageComponent>(locker, out var storage))
        {
            _entityStorageSystem.ToggleOpen(target, locker, storage);
            _entityStorageSystem.Insert(target, locker, storage);
            _entityStorageSystem.ToggleOpen(target, locker, storage);
        }
        _weldableSystem.SetWeldedState(locker, true);
        return true;
    }

    private bool HarshSlow(EntityUid target)
    {
        float slowDown = 0.6f;
        var movementSpeed = EnsureComp<MovementSpeedModifierComponent>(target);
        (movementSpeed.BaseSprintSpeed, movementSpeed.BaseWalkSpeed) = (movementSpeed.BaseSprintSpeed * slowDown, movementSpeed.BaseWalkSpeed * slowDown);

        Dirty(target, movementSpeed);

        _popupSystem.PopupEntity("You feel a quite a bit slower", target,
            target, PopupType.LargeCaution);
        return true;
    }
    #endregion
    #region Nasty
    // Fate worse then or about as bad as death
    private (Func<EntityUid, bool>, string) AnyNastySmite()
    {
        Func<EntityUid, bool> NastyMonkeySmite = target => { _polymorphSystem.PolymorphEntity(target, "AdminMonkeySmite"); return true; };
        Func<EntityUid, bool> NastyDisposalsSmite = target => { _polymorphSystem.PolymorphEntity(target, "AdminDisposalsSmite"); return true; };
        Func<EntityUid, bool> NastyBreadSmite = target => { _polymorphSystem.PolymorphEntity(target, "AdminBreadSmite"); return true; };
        Func<EntityUid, bool> NastyMouseSmite = target => { _polymorphSystem.PolymorphEntity(target, "AdminMouseSmite"); return true; };

        var smites = new (Func<EntityUid, bool>, string)[]
        {
            (NastyMonkeySmite, nameof(NastyMonkeySmite)),
            (NastyDisposalsSmite, nameof(NastyDisposalsSmite)),
            (NastyBreadSmite, nameof(NastyBreadSmite)),
            (NastyMouseSmite, nameof(NastyMouseSmite)),
            (NastyByeHands, nameof(NastyByeHands)),
            (NastyByeStomach, nameof(NastyByeStomach)),
            (NastyPinball, nameof(NastyPinball)),
        };

        var (smite, smiteName) = smites[_random.Next(smites.Length)];
        return (smite, smiteName);
    }

    private bool NastyByeHands(EntityUid target)
    {
        var baseXform2 = Transform(target);
        foreach (var part in _bodySystem.GetBodyChildrenOfType(target, BodyPartType.Hand))
        {
            _transformSystem.AttachToGridOrMap(part.Id);
        }
        _popupSystem.PopupEntity(Loc.GetString("admin-smite-remove-hands-self"), target,
            target, PopupType.LargeCaution);
        _popupSystem.PopupCoordinates(Loc.GetString("admin-smite-remove-hands-other", ("name", target)), baseXform2.Coordinates,
            Filter.PvsExcept(target), true, PopupType.Medium);
        return true;
    }

    private bool NastyByeStomach(EntityUid target)
    {
        if (TryComp<BodyComponent>(target, out var body2))
        {
            foreach (var entity in _bodySystem.GetBodyOrganEntityComps<StomachComponent>((target, body2)))
            {
                QueueDel(entity.Owner);
            }

            _popupSystem.PopupEntity(Loc.GetString("admin-smite-stomach-removal-self"), target,
                target, PopupType.LargeCaution);
            return true;
        }

        return false;
    }

    private bool NastyPinball(EntityUid target) // Caused server error on restart
    {
        PhysicsComponent? physics;
        if (TryComp<PhysicsComponent>(target, out physics))
        {
            var xform2 = Transform(target);
            var fixtures = Comp<FixturesComponent>(target);
            xform2.Anchored = false; // Just in case.

            _physics.SetBodyType(target, BodyType.Dynamic, body: physics);
            _physics.SetBodyStatus(target, physics, BodyStatus.InAir);
            _physics.WakeBody(target, manager: fixtures, body: physics);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                _physics.SetHard(target, fixture, false, manager: fixtures);
            }

            _physics.SetLinearVelocity(target, _random.NextVector2(8.0f, 8.0f), manager: fixtures, body: physics);
            _physics.SetAngularVelocity(target, MathF.PI * 12, manager: fixtures, body: physics);
            _physics.SetLinearDamping(target, physics, 0f);
            _physics.SetAngularDamping(target, physics, 0f);
            return true;
        }
        return false;
    }
    #endregion
    #region Harm
    // Stuff that damages the player without a kill
    private (Func<EntityUid, bool>, string) AnyHarmSmite()
    {
        var smites = new (Func<EntityUid, bool>, string)[]
        {
            (HarmBleeding, nameof(HarmBleeding)),
            (HarmBurn, nameof(HarmBurn)),
            (HarmElectricute, nameof(HarmElectricute)),
            (HarmBleeding, nameof(HarmBleeding)),
        };

        var (smite, smiteName) = smites[_random.Next(smites.Length)];
        return (smite, smiteName);
    }

    private bool HarmBurn(EntityUid target)
    {
        if (TryComp<FlammableComponent>(target, out var flammable))
        {
            // Fuck you. Burn Forever.
            flammable.FireStacks = flammable.MaximumFireStacks;
            _flammableSystem.Ignite(target, target);
            var xform5 = Transform(target);
            _popupSystem.PopupEntity(Loc.GetString("admin-smite-set-alight-self"), target,
                target, PopupType.LargeCaution);
            _popupSystem.PopupCoordinates(Loc.GetString("admin-smite-set-alight-others", ("name", target)), xform5.Coordinates,
                Filter.PvsExcept(target), true, PopupType.MediumCaution);
            return true;
        }
        return false;
    }

    private bool HarmBleeding(EntityUid target)
    {
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _bloodstreamSystem.TryModifyBleedAmount(target, 8);
            var xform4 = Transform(target);
            _popupSystem.PopupEntity(Loc.GetString("You feel a sharpness in the air, blood rushes out!"), target,
                target, PopupType.MediumCaution);
            return true;
        }
        return false;
    }

    private bool HarmElectricute(EntityUid target)
    {
        if (TryComp<DamageableComponent>(target, out var damageable) &&
                            HasComp<MobStateComponent>(target))
        {
            int damageToDeal = 35;

            if (_inventorySystem.TryGetSlots(target, out var slotDefinitions))
            {
                foreach (var slot in slotDefinitions)
                {
                    if (!_inventorySystem.TryGetSlotEntity(target, slot.Name, out var slotEnt))
                        continue;

                    RemComp<InsulatedComponent>(slotEnt.Value); // Fry the gloves.
                }
            }

            _electrocutionSystem.TryDoElectrocution(target, null, damageToDeal,
                TimeSpan.FromSeconds(30), refresh: true, ignoreInsulation: true);
            return true;
        }
        return false;
    }
    #endregion
    #region Kill
    /// Kill should for the most part kill the person and possibly round remove them.
    private (Func<EntityUid, bool>, string) AnyKillSmite()
    {
        Func<EntityUid, bool> KillSign = target => { EnsureComp<KillSignComponent>(target); return true; };
        Func<EntityUid, bool> KillPointingArrow = target => { EnsureComp<PointingArrowAngeringComponent>(target); return true; };

        var smites = new (Func<EntityUid, bool>, string)[]
        {
            (KillAsh, nameof(KillAsh)),
            (KillByeBlood, nameof(KillByeBlood)),
            (KillByeLungs, nameof(KillByeLungs)),
            (KillByeOrgans, nameof(KillByeOrgans)),
            (KillElectricute, nameof(KillElectricute)),
            (KillGibBoom, nameof(KillGibBoom)),
            (KillTooFast, nameof(KillTooFast)),
            (KillYeet, nameof(KillYeet)),
            (KillSign, nameof(KillSign)),
            (KillPointingArrow, nameof(KillPointingArrow)),
        };

        var (smite, smiteName) = smites[_random.Next(smites.Length)];
        return (smite, smiteName);
    }

    private bool KillByeBlood(EntityUid target)
    {
        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            _bloodstreamSystem.SpillAllSolutions(target, bloodstream);
            var xform4 = Transform(target);
            _popupSystem.PopupEntity(Loc.GetString("admin-smite-remove-blood-self"), target,
                target, PopupType.LargeCaution);
            _popupSystem.PopupCoordinates(Loc.GetString("admin-smite-remove-blood-others", ("name", target)), xform4.Coordinates,
                Filter.PvsExcept(target), true, PopupType.MediumCaution);
            return true;
        }
        return false;
    }

    private bool KillElectricute(EntityUid target)
    {
        if (TryComp<DamageableComponent>(target, out var damageable) &&
                            HasComp<MobStateComponent>(target))
        {
            int damageToDeal;
            if (!_mobThresholdSystem.TryGetThresholdForState(target, MobState.Critical, out var criticalThreshold))
            {
                // We can't crit them so try killing them.
                if (!_mobThresholdSystem.TryGetThresholdForState(target, MobState.Dead,
                        out var deadThreshold))
                    return false;// whelp.
                damageToDeal = deadThreshold.Value.Int() - (int) damageable.TotalDamage;
            }
            else
            {
                damageToDeal = criticalThreshold.Value.Int() - (int) damageable.TotalDamage;
            }

            if (damageToDeal <= 0)
                damageToDeal = 100; // murder time.

            if (_inventorySystem.TryGetSlots(target, out var slotDefinitions))
            {
                foreach (var slot in slotDefinitions)
                {
                    if (!_inventorySystem.TryGetSlotEntity(target, slot.Name, out var slotEnt))
                        continue;

                    RemComp<InsulatedComponent>(slotEnt.Value); // Fry the gloves.
                }
            }

            _electrocutionSystem.TryDoElectrocution(target, null, damageToDeal,
                TimeSpan.FromSeconds(30), refresh: true, ignoreInsulation: true);
            return true;
        }
        return false;
    }

    private bool KillByeOrgans(EntityUid target)
    {
        if (TryComp<BodyComponent>(target, out var body))
        {
            _vomitSystem.Vomit(target, -1000, -1000); // You feel hollow!
            var organs = _bodySystem.GetBodyOrganEntityComps<TransformComponent>((target, body));
            var baseXform = Transform(target);
            foreach (var organ in organs)
            {
                if (HasComp<BrainComponent>(organ.Owner) || HasComp<EyeComponent>(organ.Owner))
                    continue;

                _transformSystem.PlaceNextTo((organ.Owner, organ.Comp1), (target, baseXform));
            }

            _popupSystem.PopupEntity(Loc.GetString("admin-smite-vomit-organs-self"), target,
                target, PopupType.LargeCaution);
            _popupSystem.PopupCoordinates(Loc.GetString("admin-smite-vomit-organs-others", ("name", target)), baseXform.Coordinates,
                Filter.PvsExcept(target), true, PopupType.MediumCaution);
            return true;
        }
        return false;
    }

    private bool KillGibBoom(EntityUid target)
    {
        var coords = _transformSystem.GetMapCoordinates(target);
        Timer.Spawn(_gameTiming.TickPeriod,
            () => _explosionSystem.QueueExplosion(coords, ExplosionSystem.DefaultExplosionPrototypeId,
                4, 1, 2, target, maxTileBreak: 0), // it gibs, damage doesn't need to be high.
            CancellationToken.None);

        _bodySystem.GibBody(target);
        return true;
    }

    private bool KillYeet(EntityUid target)
    {
        if (TryComp<PhysicsComponent>(target, out var physics))
        {
            var xform3 = Transform(target);
            var fixtures = Comp<FixturesComponent>(target);
            xform3.Anchored = false; // Just in case.
            _physics.SetBodyType(target, BodyType.Dynamic, manager: fixtures, body: physics);
            _physics.SetBodyStatus(target, physics, BodyStatus.InAir);
            _physics.WakeBody(target, manager: fixtures, body: physics);

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard)
                    continue;

                _physics.SetRestitution(target, fixture, 1.1f, false, fixtures);
            }

            _fixtures.FixtureUpdate(target, manager: fixtures, body: physics);

            _physics.SetLinearVelocity(target, _random.NextVector2(1.5f, 1.5f), manager: fixtures, body: physics);
            _physics.SetAngularVelocity(target, MathF.PI * 12, manager: fixtures, body: physics);
            _physics.SetLinearDamping(target, physics, 0f);
            _physics.SetAngularDamping(target, physics, 0f);
            return true;
        }
        return false;
    }

    private bool KillByeLungs(EntityUid target) // Not guranteed kill, super annoying if it doesn't kill you
    {
        if (TryComp<BodyComponent>(target, out var body3))
        {
            foreach (var entity in _bodySystem.GetBodyOrganEntityComps<LungComponent>((target, body3)))
            {
                QueueDel(entity.Owner);
            }

            _popupSystem.PopupEntity(Loc.GetString("admin-smite-lung-removal-self"), target,
                target, PopupType.LargeCaution);
            return true;
        }
        return false;
    }

    private bool KillAsh(EntityUid target)
    {
        EntityManager.QueueDeleteEntity(target);
        Spawn("Ash", Transform(target).Coordinates);
        _popupSystem.PopupEntity(Loc.GetString("admin-smite-turned-ash-other", ("name", target)), target, PopupType.LargeCaution);
        return true;
    }

    private bool KillTooFast(EntityUid target)
    {
        var movementSpeed = EnsureComp<MovementSpeedModifierComponent>(target);
        _movementSpeedModifierSystem?.ChangeBaseSpeed(target, 400, 8000, 40, movementSpeed);

        _popupSystem.PopupEntity(Loc.GetString("admin-smite-super-speed-prompt"), target,
            target, PopupType.LargeCaution);
        return true;
    }
    #endregion
    #region Removed

    // private bool HarshWeightlessness(EntityUid target) // Doesn't work makes them jitter a bunch not fun
    // {
    //     bool got_smitted;
    //     var grav = EnsureComp<MovementIgnoreGravityComponent>(target);
    //     grav.Weightless = true;

    //     Dirty(target, grav);
    //     got_smitted = true;
    //     return got_smitted;
    // }



    // private bool BitterCatEars(EntityUid target, bool got_smitted) // Some people might like this
    // {
    //     if (TryComp<InventoryComponent>(target, out var inventory))
    //     {
    //         var ears = Spawn("ClothingHeadHatCatEars", Transform(target).Coordinates);
    //         EnsureComp<UnremoveableComponent>(ears);
    //         _inventorySystem.TryUnequip(target, "head", true, true, false, inventory);
    //         _inventorySystem.TryEquip(target, ears, "head", true, true, false, inventory);
    //         got_smitted = true;
    //     }

    //     return got_smitted;
    // }
    #endregion
}
