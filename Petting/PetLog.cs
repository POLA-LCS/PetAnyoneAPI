using Terraria;
using Terraria.ModLoader;

namespace PetAnyone;

/// <summary>
/// Internal logger for the API surface. Error output is throttled per owner mod and context so a
/// misbehaving consumer cannot flood the log. Falls back to the API mod logger and then the
/// console when the owner mod is no longer loaded. Main thread only, like the rest of the API.
/// </summary>
internal static class PetLog
{
    /// <summary>Internal name of this API mod, used for the logger fallback chain.</summary>
    internal const string ApiModName = "PetAnyoneAPI";

    private const int ImmediateLogLimit = 3;
    private const long ThrottleWindowTicks = 300;

    private static readonly Dictionary<(string Owner, string Context), ErrorThrottle> throttles = new();

    /// <summary>
    /// Logs an exception for one owner and context. The first three errors log immediately, later
    /// ones at most once per <see cref="ThrottleWindowTicks"/> game ticks, including how many were
    /// suppressed since the previous log.
    /// </summary>
    internal static void Error(Mod? owner, string context, Exception exception)
    {
        string ownerName = owner?.Name ?? "(none)";
        if (!throttles.TryGetValue((ownerName, context), out ErrorThrottle? throttle))
        {
            throttle = new ErrorThrottle();
            throttles[(ownerName, context)] = throttle;
        }

        long suppressed = throttle.Record((long)Main.GameUpdateCount);
        if (suppressed < 0)
            return;

        string message = $"[{ApiModName}] {context} failed for '{ownerName}': {exception}";
        if (suppressed > 0)
            message += $" ({suppressed} similar error(s) suppressed)";

        Write(owner, message, isError: true);
    }

    /// <summary>Logs an informational message for one owner, without throttling.</summary>
    internal static void Info(Mod? owner, string message)
    {
        Write(owner, $"[{ApiModName}] {message}", isError: false);
    }

    /// <summary>Drops every throttle entry. Safe to call any number of times.</summary>
    internal static void Clear()
    {
        throttles.Clear();
    }

    private static void Write(Mod? owner, string message, bool isError)
    {
        if (owner is not null && PetRegistry.IsOwnerLoaded(owner))
        {
            if (isError)
                owner.Logger.Error(message);
            else
                owner.Logger.Info(message);
            return;
        }

#if PETANYONE_MOD_BUILD
        if (PetAnyoneAPI.PetAnyoneMod.Instance is Mod self)
        {
            if (isError)
                self.Logger.Error(message);
            else
                self.Logger.Info(message);
            return;
        }
#endif

        if (ModLoader.TryGetMod(ApiModName, out Mod apiMod))
        {
            if (isError)
                apiMod.Logger.Error(message);
            else
                apiMod.Logger.Info(message);
            return;
        }

        if (isError)
            Console.Error.WriteLine(message);
        else
            Console.WriteLine(message);
    }

    /// <summary>Per owner and context counter implementing the 3 immediate, then 1 per window rule.</summary>
    private sealed class ErrorThrottle
    {
        private int totalCount;
        private long lastLogTick;
        private int suppressedSinceLog;

        /// <summary>
        /// Records one error at <paramref name="now"/>. Returns the number of previously suppressed
        /// errors to report, or -1 when this error is itself suppressed.
        /// </summary>
        internal int Record(long now)
        {
            totalCount++;
            if (totalCount <= ImmediateLogLimit)
            {
                lastLogTick = now;
                return 0;
            }

            if (now - lastLogTick < ThrottleWindowTicks)
            {
                suppressedSinceLog++;
                return -1;
            }

            int suppressed = suppressedSinceLog;
            suppressedSinceLog = 0;
            lastLogTick = now;
            return suppressed;
        }
    }
}
