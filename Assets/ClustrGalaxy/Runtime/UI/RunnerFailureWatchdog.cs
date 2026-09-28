using System;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>
    /// Ends an opt-in qualification runner with exit code 2 on an unhandled exception. An exception
    /// otherwise stops the runner coroutine silently and leaves the player waiting for the harness timeout.
    /// </summary>
    public sealed class RunnerFailureWatchdog : IDisposable
    {
        private readonly string tag;
        private bool active = true;

        public RunnerFailureWatchdog(string tag)
        {
            this.tag = tag;
            Application.logMessageReceived += OnLog;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (!active || type != LogType.Exception) return;
            Dispose();
            Time.timeScale = 1;
            Debug.LogError("[" + tag + "] FAIL unhandled exception: " + message);
            Application.Quit(2);
        }

        public void Dispose()
        {
            if (!active) return;
            active = false;
            Application.logMessageReceived -= OnLog;
        }
    }
}
