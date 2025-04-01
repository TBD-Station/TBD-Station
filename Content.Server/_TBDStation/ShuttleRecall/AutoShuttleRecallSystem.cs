using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.RoundEnd;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.Zombies;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Zombies;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Globalization;

namespace Content.Server._TBDStation.SlurFilter;
public sealed class AutoShuttleRecallSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ZombieSystem _zombie = default!;
    [Dependency] protected readonly GameTicker GameTicker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    private readonly TimeSpan _endCheckDelay = TimeSpan.FromMinutes(1);
    private TimeSpan _endCheck;
    private TimeSpan _callShuttle;
    private TimeSpan _endRound;
    private int _totalHumans = 0;

    public override void Initialize()
    {
        base.Initialize();
        _endCheck = _endCheckDelay + _timing.CurTime;
        _callShuttle = TimeSpan.FromMinutes(100) + _timing.CurTime;
        _endRound = TimeSpan.FromMinutes(125) + _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_endCheck > _timing.CurTime)
            return;
        CheckRoundEnd();
        _endCheck = _endCheckDelay + _timing.CurTime;
    }

    /// <summary>
    ///     The big kahoona function for checking if the round is gonna end
    /// </summary>
    private void CheckRoundEnd()
    {
        var x = GetHealthyAndDeadHumans();
        var healthy = x.Item1;
        var dead = x.Item2; // Some reason doesn't count ghosts
        _totalHumans = Math.Max(healthy + dead, _totalHumans);
        var fractionDead = Math.Abs(1 - (float)healthy / _totalHumans);

        if (fractionDead >= 1) // Just end if absolutely no one is alive
        {
            EndRoundLogAndAnnouce("NT detects no life signs on the station, and has decided to sever connection with the station.",
                $"Shift ended INSTANTLY since everyone is dead. {fractionDead} of crew dead. {healthy} alive, {dead} dead.");
            _roundEnd.EndRound(); // end round instantly
        }
        // else if (healthy + dead == 0) // No one is alive or dead - Removed for time being since it breaks integration tests, and the people can just vote to restart the round. - https://github.com/Bilbo367/TBD-Station/blob/5e1169b101df94e2f767ebc9d503734fa4af193a/Content.IntegrationTests/Pair/TestPair.Recycle.cs#L199
        // {
        //     EndRoundLogAndAnnouce("NT detects no life on the station, and has decided to sever connection with the station.",
        //         $"Shift ended INSTANTLY since no one is alive or dead.");
        //     _roundEnd.EndRound(); // end round instantly
        // }
        else if (_timing.CurTime > _endRound)
        {
            EndRoundLogAndAnnouce("NT detects irregular length of shift, and has decided to sever connection with the station.",
                $"Shift ended INSTANTLY due to hard time out. curTime={_timing.CurTime}, endRoundTime={_endRound}");
            _roundEnd.EndRound(); // end round instantly
        }
        else if (fractionDead > 0.5f && !_roundEnd.IsRoundEndRequested())
        {
            EndRoundLogAndAnnouce("Majority of crew dead, automatic shuttle called.",
                $"Round ended due to majority of crew dead. {fractionDead} of crew dead. {healthy} alive, {dead} dead.");
            _roundEnd.RequestRoundEnd(null, false);
        }
        else if (_timing.CurTime > _callShuttle && !_roundEnd.IsRoundEndRequested())
        {
            EndRoundLogAndAnnouce("Shift ended, please finish your procedures and prepare for departure.",
                $"Shift ended due to time out. curTime={_timing.CurTime}, callShuttleTime={_callShuttle}");
            _roundEnd.RequestRoundEnd(null, false);
        }
    }

    private void EndRoundLogAndAnnouce(string annoucment, string logMessage)
    {
        foreach (var station in _station.GetStations())
        {
            _chat.DispatchStationAnnouncement(station, annoucment, colorOverride: Color.Crimson);
        }
        _adminLogger.Add(LogType.ShuttleCalled,
        LogImpact.High,
        $"{logMessage}");
    }

    /// <summary>
    /// Gets the list of humans who are alive, and are on a station.
    /// Flying off via a shuttle disqualifies you.
    /// </summary>
    /// <returns></returns>
    private Tuple<int, int> GetHealthyAndDeadHumans(bool includeOffStation = true)
    {
        var healthy = 0;
        var dead = 0;

        var stationGrids = new HashSet<EntityUid>();
        if (!includeOffStation)
        {
            foreach (var station in _station.GetStationsSet())
            {
                if (TryComp<StationDataComponent>(station, out var data) && _station.GetLargestGrid(data) is { } grid)
                    stationGrids.Add(grid);
            }
        }

        var players = AllEntityQuery<HumanoidAppearanceComponent, ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out _, out var mob, out var xform))
        {
            if (!includeOffStation && !stationGrids.Contains(xform.GridUid ?? EntityUid.Invalid))
                continue;

            if (_mobState.IsAlive(uid, mob))
                healthy++;
            if (_mobState.IsIncapacitated(uid, mob)) // Count those in crit as dead
                dead++;
        }
        // var ghosts = AllEntityQuery<GhostComponent>();
        // while (ghosts.MoveNext(out var uid, out var ghost))
        // {
        //     if (ghost.TimeOfDeath == TimeSpan.Zero || ghost.CanGhostInteract) // Timespan zero means they observed. ghost.CanGhostInteract means they aghosted.
        //         continue;
        //     if (ghost.CanReturnToBody) // If they can return to body, they are not dead.
        //         dead++;
        // }
        return new Tuple<int, int>(healthy, dead);
    }
}
