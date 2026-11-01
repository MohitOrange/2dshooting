using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// One shared start-up for Unity Gaming Services: initialize + anonymous sign-in, done once and
    /// reused by online rooms, friends and voice. Started at app launch so the first ONLINE tap (and the
    /// first match) never waits for it. "-nbprofile NAME" gives a second copy on the same PC its own player.
    /// </summary>
    public static class UgsBoot
    {
        private static Task _signIn;

        // the editor keeps statics between play sessions (domain reload is off); start clean each time
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _signIn = null;

        public static bool SignedIn =>
            UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>This player's id (empty until signed in).</summary>
        public static string PlayerId => SignedIn ? AuthenticationService.Instance.PlayerId : "";

        /// <summary>Signs in once; a failed attempt (no internet) is retried by the next call.</summary>
        public static Task SignInAsync()
        {
            if (SignedIn) return Task.CompletedTask;
            if (_signIn == null || _signIn.IsFaulted || _signIn.IsCanceled) _signIn = DoSignIn();
            return _signIn;
        }

        private static async Task DoSignIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var options = new InitializationOptions();
                string profile = CommandLine.Value("-nbprofile");
                if (!string.IsNullOrEmpty(profile)) options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>
        /// Keeps the account name in step with the game name. Friends see it as "NAME#1234" (the number
        /// is added by Unity and keeps names unique). Returns the full name, or "" on failure.
        /// </summary>
        public static async Task<string> SyncPlayerNameAsync(string gameName)
        {
            try
            {
                await SignInAsync();
                var auth = AuthenticationService.Instance;
                string wanted = AccountName(gameName);
                string current = auth.PlayerName ?? "";
                if (string.IsNullOrEmpty(current)) current = await auth.GetPlayerNameAsync() ?? "";
                int hash = current.LastIndexOf('#');
                string bare = hash > 0 ? current.Substring(0, hash) : current;
                if (!string.Equals(bare, wanted, StringComparison.OrdinalIgnoreCase)) current = await auth.UpdatePlayerNameAsync(wanted);
                return current ?? "";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NeonBrawl] player name: " + e.Message);
                return AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerName ?? "" : "";
            }
        }

        /// <summary>Account names take no spaces: "LIL LASER" becomes "LIL_LASER".</summary>
        public static string AccountName(string gameName)
        {
            var sb = new System.Text.StringBuilder(16);
            foreach (char c in NetIO.CleanName(gameName).ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') sb.Append(c);
                else if (c == ' ' && sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
                if (sb.Length >= 20) break;
            }
            return sb.Length > 0 ? sb.ToString().Trim('_') : "PLAYER";
        }

        /// <summary>"MOHIT_K#4821" shown as "MOHIT K#4821".</summary>
        public static string Display(string accountName) => string.IsNullOrEmpty(accountName) ? "PLAYER" : accountName.Replace('_', ' ');

        /// <summary>"MOHIT K#4821" without the number, for tight spaces.</summary>
        public static string ShortName(string accountName)
        {
            string d = Display(accountName);
            int hash = d.LastIndexOf('#');
            return hash > 0 ? d.Substring(0, hash) : d;
        }
    }
}
