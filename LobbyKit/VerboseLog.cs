using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace LobbyKit
{
    // Routes high-volume debug/telemetry lines (e.g. [RPC] anticheat traffic, [CrashDetect] state
    // dumps) so they don't flood the live console on a headless server.
    //
    //   - Headless server (-batchmode): write ONLY to a dedicated file, UserData/LobbyKit-verbose.log,
    //     keeping the MelonLoader console/Latest.log clean for the operator.
    //   - Normal client / graphical host: behave exactly like MelonLogger.Msg (console as before).
    //
    // Genuine warnings/errors are NOT routed through here — call MelonLogger.Warning/Error directly so
    // they always reach the console regardless of headless mode.
    internal static class VerboseLog
    {
        private static readonly object _lock = new object();
        private static StreamWriter _writer;
        private static bool _failed;

        private static string FilePath =>
            Path.Combine(MelonEnvironment.UserDataDirectory, "LobbyKit-verbose.log");

        public static void Msg(string message)
        {
            // Graphical host / client: unchanged behaviour — straight to the MelonLoader console.
            if (!Application.isBatchMode)
            {
                MelonLogger.Msg(message);
                return;
            }

            // Headless server: file only.
            if (_failed)
                return;
            try
            {
                lock (_lock)
                {
                    if (_writer == null)
                        _writer = new StreamWriter(FilePath, append: true) { AutoFlush = true };
                    _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                }
            }
            catch (Exception ex)
            {
                _failed = true; // never let logging throw or spam; report once and fall back to silence
                MelonLogger.Warning($"[LobbyKit] VerboseLog file disabled ({ex.GetType().Name}: {ex.Message}); suppressing verbose output.");
            }
        }
    }
}
