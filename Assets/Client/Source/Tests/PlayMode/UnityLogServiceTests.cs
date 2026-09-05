using System.Text.RegularExpressions;
using Client.Adapters.Shared.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Adapters.Tests
{
    /// <summary>The logger writes a line once, so no caller needs a flag to say it once.</summary>
    /// <remarks>DEU0113's premise, measured on the one logger the consumer ships.</remarks>
    public sealed class UnityLogServiceTests
    {
        [Test]
        public void ARepeatedWarning_ReachesTheConsoleOnce()
        {
            var log = new UnityLogService("Test.Log");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[Client\]\[Test\.Log\] same line"));

            log.Warn("same line");
            log.Warn("same line");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ARepeatedError_ReachesTheConsoleOnce()
        {
            var log = new UnityLogService("Test.Log");
            LogAssert.Expect(LogType.Error, new Regex(@"\[Client\]\[Test\.Log\] same error"));

            log.Error("same error");
            log.Error("same error");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ALineWithANewNumber_IsANewLine()
        {
            var log = new UnityLogService("Test.Log");
            LogAssert.Expect(LogType.Warning, new Regex(@"ran out after 1"));
            LogAssert.Expect(LogType.Warning, new Regex(@"ran out after 2"));

            log.Warn("ran out after 1");
            log.Warn("ran out after 2");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AChannelLogger_KeepsItsOwnMemory()
        {
            var root = new UnityLogService();
            var child = root.ForChannel("Test.Child");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Client\] shared$"));
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Client\]\[Test\.Child\] shared$"));

            root.Warn("shared");
            child.Warn("shared");

            LogAssert.NoUnexpectedReceived();
        }
    }
}
