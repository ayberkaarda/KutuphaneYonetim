using KutuphaneYonetim.UI.Infrastructure;
using KutuphaneYonetim.UI.Tests.Support;
using Microsoft.Win32;

[assembly: AssemblyFixture(typeof(NoDialogs))]

namespace KutuphaneYonetim.UI.Tests.Support;

/// <summary>
/// Safety net for the whole test assembly: a message window would be a real window that waits for a
/// click, so every dialog request is answered with "No" and recorded instead of being shown.
/// </summary>
public sealed class NoDialogs : IDisposable
{
    private static readonly List<string> Requests = [];

    public NoDialogs()
    {
        StartSystemEventsOnTheirOwnThread();
        Dialogs.Override = (text, caption, _, _) =>
        {
            lock (Requests)
            {
                Requests.Add(caption + ": " + text);
            }

            return DialogResult.No;
        };
    }

    /// <summary>Dialog requests made so far, for tests that check one was (or was not) asked.</summary>
    public static IReadOnlyList<string> Requested
    {
        get
        {
            lock (Requests)
            {
                return [.. Requests];
            }
        }
    }

    public void Dispose() => Dialogs.Override = null;

    /// <summary>
    /// Window handles of grids, lists and date pickers subscribe to <see cref="SystemEvents"/>. The first
    /// subscriber decides where its hidden window lives: on an STA test thread, which never pumps
    /// messages and ends after the test, later broadcasts can block. Subscribing once from an MTA
    /// thread makes SystemEvents run its own thread with a message loop instead.
    /// </summary>
    private static void StartSystemEventsOnTheirOwnThread()
    {
        var thread = new Thread(() => SystemEvents.UserPreferenceChanged += Ignore) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        thread.Join();

        static void Ignore(object? sender, UserPreferenceChangedEventArgs e)
        {
        }
    }
}
