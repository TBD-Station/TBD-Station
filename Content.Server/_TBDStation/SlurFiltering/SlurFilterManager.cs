
using System.Text.RegularExpressions;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Content.Server.Chat.Managers;
using Robust.Shared.Network;

namespace Content.Server._TBDStation.SlurFilter
{
    public sealed class SlurFilterManager : IPostInjectInit
    {
        [Dependency] private readonly IChatManager _chatManager = default!;
        private static readonly string Pattern = @"badword";
        private static readonly Regex _slurRegex = new Regex(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private HashSet<NetUserId> mutedPlayers = new HashSet<NetUserId>();

        public void PostInject()
        {
        }
        public bool ContainsSlur(ICommonSession player, string message)
        {
            if (mutedPlayers.Contains(player.UserId))
            {
                _chatManager.DispatchServerMessage(player, "You are Admin Muted!");
                return true;
            }
            bool containsSlur = _slurRegex.IsMatch(message);
            if (containsSlur)
            {
                var feedback = Loc.GetString("server-slur-detected-warning");
                _chatManager.DispatchServerMessage(player, feedback);
            }
            return containsSlur;
        }

        internal bool ContainsSlur(string message)
        {
            bool containsSlur = _slurRegex.IsMatch(message);
            return containsSlur;
        }

        // TODO add way to add time to mute and persist beyond round restart.
        internal void Mute(NetUserId? target, uint minutes)
        {
            if (target.HasValue)
                mutedPlayers.Add(target.Value);
            // DateTimeOffset? expires = null;
            // if (minutes > 0)
            // {
            //     expires = DateTimeOffset.Now + TimeSpan.FromMinutes(minutes);
            // }
        }
    }
}
