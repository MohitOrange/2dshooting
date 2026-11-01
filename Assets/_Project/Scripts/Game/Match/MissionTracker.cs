namespace NeonBrawl
{
    /// <summary>
    /// Feeds your kills and match results into today's missions and celebrates a finished mission
    /// right away (toast + sound). Ignores demo matches and other fighters' kills.
    /// </summary>
    public sealed class MissionTracker
    {
        private readonly GameContext _ctx;
        private MatchController _match;
        private Fighter _nemesis;

        public MissionTracker(GameContext ctx)
        {
            _ctx = ctx;
            ctx.Events.Killed += OnKilled;
        }

        private void OnKilled(KillInfo k)
        {
            var m = _ctx.Match;
            if (m == null || m.Spectator || k.Victim == null) return;
            if (m != _match) { _match = m; _nemesis = null; }
            bool self = k.Killer == null || k.Killer == k.Victim;
            if (k.Victim.IsLocal)
            {
                if (!self) _nemesis = k.Killer;
                return;
            }
            if (self || !k.Killer.IsLocal || k.Outcome.TeamKill) return;

            var p = _ctx.Profile;
            InMatch(Missions.Report(p, MissionKind.Kills, 1));
            InMatch(Missions.Report(p, MissionKind.WeaponKills, 1, k.Weapon));
            InMatch(Missions.Report(p, MissionKind.Streak, k.Outcome.KillerStreak));
            if (k.Outcome.MultiKill == 2) InMatch(Missions.Report(p, MissionKind.Multi, 1)); // each multi-kill run counts once
            if (_nemesis != null && k.Victim == _nemesis)
            {
                _nemesis = null;
                InMatch(Missions.Report(p, MissionKind.Revenge, 1));
            }
        }

        private void InMatch(MissionDef done)
        {
            if (done == null) return;
            _ctx.Events.RaiseToast("MISSION COMPLETE!  " + done.Text.ToUpperInvariant());
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.7f);
        }

        /// <summary>Called once per finished match you played (before the results screen).</summary>
        public void OnMatchEnded(bool won, int place)
        {
            var p = _ctx.Profile;
            AfterMatch(Missions.Report(p, MissionKind.Matches, 1));
            if (won) AfterMatch(Missions.Report(p, MissionKind.Wins, 1));
            if (place >= 1 && place <= 3) AfterMatch(Missions.Report(p, MissionKind.Top3, 1));
        }

        private void AfterMatch(MissionDef done)
        {
            if (done == null) return;
            _ctx.UI.Toast("MISSION COMPLETE!  " + done.Text.ToUpperInvariant());
            _ctx.Sfx.PlayUi(SfxId.UiLevelUp, 0.7f);
        }
    }
}
