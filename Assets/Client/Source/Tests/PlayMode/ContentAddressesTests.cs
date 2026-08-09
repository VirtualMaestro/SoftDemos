using System.Collections;
using System.Linq;
using Game.Adapters.Services;
using Game.Simulation.Ports;
using NUnit.Framework;
using TMPro;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.U2D;

namespace Game.Adapters.Tests
{
    public sealed class ContentAddressesTests
    {
        private const float TimeoutSeconds = 15f;

        private static readonly AddressCase[] _cases =
        {
            new("art/magic-words/emoji"),
            new("art/ace-of-shadows/atlas", "card-back"),
            new("art/magic-words/atlas", "mw-bubble"),
            new("art/menu/ui-atlas", "ui-icon-ace-of-shadows"),
            new("art/menu/background"),
            new("art/phoenix-flame/atlas", "flame_0"),
            new("art/shared/ui-atlas", "ui-button"),
            new("art/ace-of-shadows/background"),
            new("art/magic-words/background"),
            new("art/phoenix-flame/background")
        };

        private AddressablesAssetService _source;

        [SetUp]
        public void SetUp()
        {
            _source = new AddressablesAssetService(new UnityLogService("Test.Content"));
        }

        [TearDown]
        public void TearDown()
        {
            _source.Dispose();
            _source = null;
        }

        [Test]
        public void AddressMap_HasExactlyOneCasePerEntry()
        {
            var addresses = AddressableAssetSettingsDefaultObject.Settings.groups
                .Where(group => group != null && group.Name != "Scenes")
                .SelectMany(group => group.entries)
                .Select(entry => entry.address)
                .ToArray();

            Assert.That(addresses, Has.Length.EqualTo(_cases.Length));
            Assert.That(addresses, Is.EquivalentTo(_cases.Select(addressCase => addressCase.Address)));
        }

        [UnityTest]
        public IEnumerator Address_Resolves([ValueSource(nameof(_cases))] AddressCase addressCase)
        {
            var requestId = _source.BeginLoad(addressCase.Address);
            yield return _PollUntilSettled(requestId);

            try
            {
                Assert.That(_source.Poll(requestId), Is.EqualTo(AsyncOpStatus.Done),
                    $"Loading '{addressCase.Address}' should reach Done.");

                var handleId = _source.ResolveHandle(requestId);
                Assert.That(handleId, Is.Not.Zero,
                    $"Loading '{addressCase.Address}' must resolve a non-zero handle.");
                Assert.That(_source.TryGetAsset(handleId, out var asset), Is.True,
                    $"Handle #{handleId} for '{addressCase.Address}' must resolve.");
                Assert.That(asset, Is.Not.Null,
                    $"Address '{addressCase.Address}' resolved to null.");

                if (addressCase.SampleSpriteName != null)
                {
                    Assert.That(asset, Is.TypeOf<SpriteAtlas>());
                    var sprite = ((SpriteAtlas)asset).GetSprite(addressCase.SampleSpriteName);
                    Assert.That(sprite, Is.Not.Null,
                        $"Atlas '{addressCase.Address}' is missing sprite '{addressCase.SampleSpriteName}'.");
                    Object.DestroyImmediate(sprite);
                }
            }
            finally
            {
                _source.Release(requestId);
            }

            Assert.That(_source.OpenRequestCount, Is.Zero);
            Assert.That(_source.HeldAssetCount, Is.Zero);
        }

        /// <summary>
        /// The dialogue payload contains U+2019 and the project font is now a static subset atlas,
        /// which renders anything outside its character table as a blank with no error and no log.
        /// Asked through <see cref="TMP_Settings"/> rather than a Resources path: the font moved out
        /// of Resources with the subset, and the question worth asking is whether whatever the
        /// project defaults to covers the character — not whether one particular asset still exists.
        /// </summary>
        [Test]
        public void DefaultFont_CoversRightSingleQuotationMark()
        {
            var font = TMP_Settings.defaultFontAsset;

            Assert.That(font, Is.Not.Null);
            Assert.That(font.HasCharacter('’'), Is.True);
        }

        private IEnumerator _PollUntilSettled(int requestId)
        {
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;

            while (_source.Poll(requestId) == AsyncOpStatus.Pending)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Request #{requestId} never left Pending within {TimeoutSeconds}s.");
                yield return null;
            }
        }

        public sealed class AddressCase
        {
            public AddressCase(string address, string sampleSpriteName = null)
            {
                Address = address;
                SampleSpriteName = sampleSpriteName;
            }

            public string Address { get; }
            public string SampleSpriteName { get; }

            public override string ToString() => Address;
        }
    }
}
