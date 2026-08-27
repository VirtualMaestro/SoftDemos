using Client.Adapters.Shell.Views;
using NUnit.Framework;
using UnityEngine;

namespace Client.Adapters.Tests
{
    /// <summary>Presses the shell's buttons the way a player does, for tests driving the boot scene.</summary>
    /// <remarks>
    /// These tests used to write <c>OpenDemoCommand</c> straight into the world. Under the phase
    /// contract that no longer works, and the reason is worth stating: a command written from a
    /// test coroutine is written outside every phase, so whether the frame's <c>Cleanup</c> deletes
    /// it before the next <c>Sim</c> reads it depends on where in the frame the coroutine happened
    /// to resume. It is exactly the defect <c>DEU0130</c> reports in product code, and the fix is
    /// the same one: record the press on the view and let the Input phase turn it into a command.
    /// <para>Which also makes these tests drive the real path — button to command to navigation —
    /// instead of starting halfway along it.</para>
    /// </remarks>
    internal static class ShellInput
    {
        public static void PressDemo(int demoIndex)
        {
            var menu = Object.FindFirstObjectByType<MenuScreen>(FindObjectsInactive.Include);
            Assert.That(menu, Is.Not.Null, "The boot scene must contain a MenuScreen.");
            menu.RequestedDemoIndex = demoIndex;
        }

        public static void PressClose()
        {
            var hud = Object.FindFirstObjectByType<DemoHudView>(FindObjectsInactive.Include);
            Assert.That(hud, Is.Not.Null, "The boot scene must contain a DemoHudView.");
            hud.CloseRequested = true;
        }
    }
}
