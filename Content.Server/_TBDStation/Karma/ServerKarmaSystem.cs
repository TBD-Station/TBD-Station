using Content.Shared.GameTicking;
using Content.Shared._TBDStation.ServerKarma.Events;
using Content.Server.Popups;
using Content.Shared.Popups;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Server.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Content.Server.Roles;
using Content.Shared.Players;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Server.Objectives;
using Content.Shared.Cuffs.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Power;
using Content.Shared.Materials;
using Content.Shared.Lathe;
using Content.Server.Research.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Server.Administration.Components;
using Robust.Shared.Timing;

namespace Content.Server._TBDStation.ServerKarma;
/// <summary>
/// Connects <see cref="ServerKarmaManager"/> to the simulation state.
/// </summary>
public sealed class ServerKarmaSystem : EntitySystem
{
    [Dependency] private readonly ServerKarmaManager _karmaMan = default!;
    [Dependency] private readonly ActorSystem _actors = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedPlayerSystem _playerSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    // assholeMeter[user] -> (timeSinceAsshole, karmaMult)
    private Dictionary<NetUserId, Tuple<DateTime, float>> _assholeMeter = new Dictionary<NetUserId, Tuple<DateTime, float>>();
    private List<int> _departmentSuccess = new List<int>();

    public override void Initialize()
    {
        base.Initialize();
        _karmaMan.KarmaChange += OnKarmaChange;
        for (int i = 0; i < Enum.GetNames(typeof(DepStatDEvent.DepStatKey)).Length; i++)
            _departmentSuccess.Add(0);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEndCleanup);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEndText);
        SubscribeNetworkEvent<PlayerKarmaRequestEvent>(OnKarmaRequest);
        SubscribeLocalEvent<PlayerKarmaHitEvent>(OnKarmaHit);
        SubscribeLocalEvent<PlayerKarmaGriefEvent>(OnKarmaGrief);

        SubscribeLocalEvent<DepStatDEvent>(OnDepartmentSuccessChange);
    }

    #region RoundEnd

    private void OnDepartmentSuccessChange(DepStatDEvent ev)
    {
        _departmentSuccess[(int) ev.Type] += ev.Amount;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _karmaMan.KarmaChange -= OnKarmaChange;
    }

    private void OnRoundEndCleanup(RoundRestartCleanupEvent ev)
    {
        _karmaMan.Save();
        for (int i = 0; i < Enum.GetNames(typeof(DepStatDEvent.DepStatKey)).Length; i++)
            _departmentSuccess[i] = 0;
    }

    private void OnRoundEndText(RoundEndMessageEvent ev)
    {
        if (ev.RoundDuration < TimeSpan.FromMinutes(10))
        {
            _adminLogger.Add(LogType.Karma,
            LogImpact.High,
            $"NO end round karma change since round duration: {ev.RoundDuration} is shorter then 10 mins");
            return;
        }

        var query = EntityQueryEnumerator<MindContainerComponent>();
        var departmentProtos = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>();

        int antagsCount = 0;
        int custodyAntags = 0;
        int deadPlayers = 0;
        // NetUserId, userId, karmaSum, departments
        List<PlayerKarmaInfo> players = new List<PlayerKarmaInfo>();

        while (query.MoveNext(out var uid, out var mindContainer))
        {
            if (!IsValidPlayerForEndRoundReward(uid, mindContainer, out var mindId, out var mind) || mindId is null || mind is null)
                continue;

            // Is player - now calculate karma
            float karma = 40; // TODO setup job reward on yml//_jobs.GetKarma(session);

            var job = _jobs.MindTryGetJobName(mindId.Value);
            var departments = departmentProtos.Where(d => d.Roles.Contains(job)).ToList();
            if (mind.OriginalOwnerUserId is null)
                continue;
            players.Add(new PlayerKarmaInfo(mind.OriginalOwnerUserId.Value, uid, karma, departments));

            if (_role.MindIsAntagonist(mindId.Value))
            {
                antagsCount++;
                if (IsInCustody(mindId.Value))
                    custodyAntags++;
            }
            if (_mobStateSystem.IsDead(uid))
                deadPlayers++;
        }

        // These numbers need to be tweaked to hell, should not apply at all when low pop/department.
        float departmentSuccessSecurity = 15 * custodyAntags - 4 * antagsCount; // TODO check greentext antags
        // departmentSuccessSecurity = Math.Clamp(departmentSuccessSecurity, -15, 90);
        float departmentSuccessCargo = 0.0003f * _departmentSuccess[(int) DepStatDEvent.DepStatKey.LathePrint] - 10;
        // departmentSuccessCargo = Math.Clamp(departmentSuccessCargo, -15, 90);
        float departmentSuccessEngineering = 20 - 0.03f * _departmentSuccess[(int) DepStatDEvent.DepStatKey.PowerOff];
        // departmentSuccessEngineering = Math.Clamp(departmentSuccessEngineering, -15, 90);
        float departmentSuccessMedical = 3 * (0.5f * players.Count - deadPlayers);
        // departmentSuccessMedical = Math.Clamp(departmentSuccessMedical, -15, 90);
        float departmentSuccessScience = 2 * (_departmentSuccess[(int) DepStatDEvent.DepStatKey.ResearchTech] - 7);
        // departmentSuccessScience = Math.Clamp(departmentSuccessScience, -15, 90);

        float departmentSuccessAll = (departmentSuccessSecurity + departmentSuccessCargo + departmentSuccessEngineering + departmentSuccessMedical + departmentSuccessScience) / (5 + 2);
        _adminLogger.Add(LogType.Karma,
        LogImpact.Medium,
        $"Department Overall Success Karma bonus/decriment's are: Security:{departmentSuccessSecurity}, Cargo:{departmentSuccessCargo}, Engineering:{departmentSuccessEngineering}, Medical:{departmentSuccessMedical}, Science:{departmentSuccessScience}, Command/Silicon:{departmentSuccessAll}");

        if (ev.RoundDuration < TimeSpan.FromMinutes(30))
            _adminLogger.Add(LogType.Karma,
            LogImpact.Medium,
            $"LESS end round karma change since round duration: {ev.RoundDuration} is shorter then 30 mins");
        // Add department data to karma sums
        // Iterate over each player
        float _karmaEndRoundMultiplier = Math.Clamp(1.0f - (float)Math.Abs(ev.RoundDuration.TotalMinutes - 60) / 60, 0.3f, 1f);
        foreach (var player in players)
        {
            // Calculate job success karma based on departments
            var jobSuccessKarma = player.Departments.Sum(department => department.ID switch
            {
                "Command" or "Silicon" => departmentSuccessAll,
                "Security" => departmentSuccessSecurity,
                "Cargo" => departmentSuccessCargo,
                "Engineering" => departmentSuccessEngineering,
                "Medical" => departmentSuccessMedical,
                "Science" => departmentSuccessScience,
                _ => 0f
            });

            // Update player karma
            var karma = player.Karma + jobSuccessKarma / player.Departments.Count;
            karma *= _karmaEndRoundMultiplier;
            if (ev.RoundDuration < TimeSpan.FromMinutes(30)) karma *= 0.6f; // Reduced karma for short rounds

            // Log and update karma
            _adminLogger.Add(LogType.Karma, LogImpact.Medium, $"end round {ToPrettyString(player.EntityUid):actor} gaining or lossing {(int) karma} karma");
            _karmaMan.AddKarma(player.UserId, (int) karma);
        }
    }

    #endregion
    #region Griefing

    private void OnKarmaHit(PlayerKarmaHitEvent ev)
    {
        if (ev.User == ev.Target) // Don't lose karma for hitting yourself
            return;

        if (!_actors.TryGetSession(new EntityUid(ev.User), out ICommonSession? session))
            return;
        if (session == null)
            return;
        var netUserId = session.UserId;

        if (HasComp<KillSignComponent>(new EntityUid(ev.Target)))
            return; // Don't lose karma for hitting a person with the big RED KILL sign above them. Mayhaps we add karma.

        // Should not lose karma attacking someone you attacked
        // Should lose extra 3x karma if said person is crit and less 0.5x karma if their full health.
        // TODO: Should not lose karma if you attack nukie/wizard, should lose karma if you unprovoked attack heratic/traitor unless they have killed someone.
        var target = new EntityUid(ev.Target);
        if (!_actors.TryGetSession(target, out ICommonSession? hitSession))
            return;
        if (hitSession == null)
            return;

        float delta = GetMultiplier(session, hitSession, ev.Damage);
        if (_mobStateSystem.IsCritical(target)) // more if crit
            delta *= 3;
        else if (_mobStateSystem.IsDead(target)) // less if dead
            delta *= 0.5f;

        if ((int)delta == 0)
            return;
        _adminLogger.Add(LogType.Karma,
        LogImpact.Medium,
        $"{ToPrettyString(new EntityUid(ev.User)):actor} hit {ToPrettyString(target):subject} lossing {(int) delta} karma");
        _karmaMan.RemoveKarma(netUserId, (int)delta);
    }

    private void OnKarmaGrief(PlayerKarmaGriefEvent ev)
    {
        if (!_actors.TryGetSession(new EntityUid(ev.User), out ICommonSession? session))
            return;
        if (session == null)
            return;
        var netUserId = session.UserId;
        float dif = 0;
        switch (ev.Grief)
        {
            case PlayerKarmaGriefEvent.GriefType.Explosion:
                dif = 10 + GetMultiplier(session, 20);
                break;
            case PlayerKarmaGriefEvent.GriefType.OpenToxicCanister:
                dif = 10 + GetMultiplier(session, 40);
                break;
            case PlayerKarmaGriefEvent.GriefType.IgniteOthers:
                dif = GetMultiplier(session, (int) (8 * ev.Val));
                break;
            case PlayerKarmaGriefEvent.GriefType.Fire:
                break;
        }

        if ((int)dif <= 0)
            return;
        _adminLogger.Add(LogType.Karma,
        LogImpact.Medium,
        $"{ToPrettyString(new EntityUid(ev.User)):actor} griefed by {ev.Grief} lossing {(int) dif} karma");
        _karmaMan.RemoveKarma(netUserId, (int)dif);
        UpdateAssholeMeter(netUserId, dif);
    }

    #endregion
    #region HelpeMethods

    private void OnKarmaRequest(PlayerKarmaRequestEvent ev, EntitySessionEventArgs eventArgs)
    {
        var senderSession = eventArgs.SenderSession;
        var karma = _karmaMan.GetKarma(senderSession.UserId);
        RaiseNetworkEvent(new PlayerKarmaUpdateEvent(karma, karma), senderSession);
    }

    /// <summary>
    /// Calls event that when a player's karma is updated.
    /// Also handles popups
    /// </summary>
    private void OnKarmaChange(PlayerKarmaChangeEvent ev)
    {
        RaiseNetworkEvent(new PlayerKarmaUpdateEvent(ev.NewKarma, ev.OldKarma), ev.UserSes);
        if (ev.UserSes.AttachedEntity.HasValue)
        {
            var userEnt = ev.UserSes.AttachedEntity.Value;
            if (ev.NewKarma > ev.OldKarma)
                _popupSystem.PopupEntity("+" + _karmaMan.Stringify(ev.NewKarma - ev.OldKarma), userEnt, userEnt, PopupType.Medium);
            else if (ev.NewKarma < ev.OldKarma)
                _popupSystem.PopupEntity("-" + _karmaMan.Stringify(ev.OldKarma - ev.NewKarma), userEnt, userEnt, PopupType.MediumCaution);
            // I really wanted to do some fancy shit where we also display a little sprite next to the pop-up, but that gets pretty complex for such a simple interaction, so, you get this.
        }
    }

    // it is important to note that (int) rounds down floats
    /// <summary>
    /// ONLY USED FOR NEGATIVE KARMA LOSS
    /// </summary>
    /// <param name="session"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    private float GetMultiplier(ICommonSession session, int val)
    {
        if (_gameTicker.RunLevel == GameRunLevel.PostRound)
            return 0;

        float mult = 1;

        var stationMinuates = (float)_gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan).TotalMinutes;
        if (stationMinuates > 60)
        {
            mult = Math.Clamp(1.0f - (float)Math.Abs(stationMinuates - 60) / 60, 0.3f, 1f);
        }
        // TODO: deal with retaliation and allow it to happen without the retaliator losing karma.
        if (_playerSystem.ContentData(session) is not { Mind: { } mindId })
            return 0.5f * val;
        if (_role.MindIsAntagonist(mindId)) // TODO give diffrent antagnists diffrent mults. Nukies should always be 0x except when hitting each other. Traitors shouldn't be plasmaflooding, Nukies can
            return 0;
        if (!_jobs.MindTryGetJob(mindId, out var prototype))
            return 1 * val;
        return prototype.KarmaMult * val;
    }

    private float GetMultiplier(ICommonSession session, ICommonSession targetedSession, int damage)
    {
        float targetMult = GetAssholeMeter(targetedSession.UserId);
        // TODO: 0x if targetedSession is nukie
        return GetMultiplier(session, damage) * targetMult;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="asshole"></param>
    /// <param name="amount">Must be postive</param>.
    private void UpdateAssholeMeter(NetUserId asshole, float amount)
    {
        // 1 > karmaMult > 0: Lower means others lose less karma greifing this player.
        var karmaMult = 1 / (1 + amount * 0.3f);
        var now = DateTime.UtcNow;
        if (_assholeMeter.TryGetValue(asshole, out var proofOfAsshole))
        {
            TimeSpan timeSinceLastJerkMoment = now - proofOfAsshole.Item1;
            if (timeSinceLastJerkMoment.TotalMinutes > 10)
            {
                _assholeMeter[asshole] = new Tuple<DateTime, float>(now, proofOfAsshole.Item2 * karmaMult);
            }
        }
        _assholeMeter[asshole] = new Tuple<DateTime, float>(now, 1 * karmaMult);
    }
    /// <summary>
    ///
    /// </summary>
    /// <param name="asshole"></param>
    /// <param name="amount">1 > amount > 0: Lower means others lose less karma greifing this player.</param>.
    private float GetAssholeMeter(NetUserId asshole)
    {
        var now = DateTime.UtcNow;
        if (_assholeMeter.TryGetValue(asshole, out var proofOfAsshole))
        {
            TimeSpan timeSinceLastJerkMoment = now - proofOfAsshole.Item1;
            if (timeSinceLastJerkMoment.TotalMinutes > 10)
            {
                return proofOfAsshole.Item2;
            }
        }
        return 1;
    }

    private bool IsValidPlayerForEndRoundReward(EntityUid uid, MindContainerComponent mindContainer, out EntityUid? mindId, out MindComponent? mind)
    {
        mindId = null;
        mind = null;
        var isBorg = HasComp<BorgChassisComponent>(uid);
        if (!(HasComp<HumanoidAppearanceComponent>(uid)
            || HasComp<BorgBrainComponent>(uid)
            || isBorg))
            return false;

        if (!mindContainer.Mind.HasValue)
            return false;
        mindId = mindContainer.Mind.Value;
        mind = Comp<MindComponent>(mindId.Value);
        return mind is not null
            && (isBorg || !_mind.IsCharacterDeadIc(mind))
            && mind.OriginalOwnerUserId.HasValue
            && mind.Session is not null;
    }
    public sealed class PlayerKarmaInfo
    {
        public NetUserId UserId { get; }
        public EntityUid EntityUid { get; }
        public float Karma { get; }
        public List<DepartmentPrototype> Departments { get; }

        public PlayerKarmaInfo(NetUserId userId, EntityUid entityUid, float karma, List<DepartmentPrototype> departments)
        {
            UserId = userId;
            EntityUid = entityUid;
            Karma = karma;
            Departments = departments;
        }
    }

    #endregion
    #region Copied stuff
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    /// <summary>
    /// Returns whether a target is considered 'in custody' (cuffed on the shuttle).
    /// </summary>
    private bool IsInCustody(EntityUid mindId, MindComponent? mind = null)
    {
        if (!Resolve(mindId, ref mind))
            return false;

        // Ghosting will not save you
        bool originalEntityInCustody = false;
        EntityUid? originalEntity = GetEntity(mind.OriginalOwnedEntity);
        if (originalEntity.HasValue && originalEntity != mind.OwnedEntity)
        {
            originalEntityInCustody = TryComp<CuffableComponent>(originalEntity, out var origCuffed) && origCuffed.CuffedHandCount > 0
                && _emergencyShuttle.IsTargetEscaping(originalEntity.Value);
        }

        return originalEntityInCustody || (TryComp<CuffableComponent>(mind.OwnedEntity, out var cuffed) && cuffed.CuffedHandCount > 0
            && _emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value));
    }
    #endregion
}
