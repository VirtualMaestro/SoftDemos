using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Client.Simulation.Tests
{
    /// <summary>Guards the WebGL size settings. Nothing else fails if one of them reverts.</summary>
    /// <remarks>
    /// Read every value through <see cref="SerializedObject"/>, never a typed reference.
    /// This asmdef must not reference URP or TextMesh Pro.
    /// <para>
    /// The build profiles override player settings on purpose, so a <c>PlayerSettings</c> read
    /// reports the active profile, not the one that ships. Everything a profile owns is read from
    /// the shipping profile instead — see <see cref="_ShippingValue"/>.
    /// </para>
    /// </remarks>
    public sealed class BuildOptimizationGuardTests
    {
        private const string UrpAssetPath =
            "Assets/Client/Settings/Rendering/URP/URP2D_RPAsset.asset";

        private const string UrpRendererPath =
            "Assets/Client/Settings/Rendering/URP/URP2D_Renderer.asset";

        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string SubsetFontPath =
            "Assets/Client/Content/Shared/Fonts/LiberationSans-Subset SDF.asset";

        /// <summary>The profile the WebGL build ships from. Its overrides are the ones that count.</summary>
        private const string ShippingProfilePath =
            "Assets/Settings/Build Profiles/Web - Release.asset";

        private const string LinkXmlPath = "Assets/Client/Source/Runtime/link.xml";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string AudioManagerPath = "ProjectSettings/AudioManager.asset";

        /// <summary><c>Master</c> is intentional. It costs build time and gives the smallest player.</summary>
        [Test]
        public void WebGlPlayerSettings_StayOptimised()
        {
            _AssertShipping("managedStrippingLevel", (int)ManagedStrippingLevel.High);
            _AssertShipping("il2cppCompilerConfiguration", (int)Il2CppCompilerConfiguration.Master);
            _AssertShipping("il2cppCodeGeneration", (int)Il2CppCodeGeneration.OptimizeSize);
            _AssertShipping("stripEngineCode", 1);
        }

        /// <summary>GitHub Pages does not send <c>Content-Encoding: br</c>. Brotli needs the fallback.</summary>
        [Test]
        public void WebGlCompression_IsBrotliWithFallback()
        {
            _AssertShipping("webGLCompressionFormat", (int)WebGLCompressionFormat.Brotli);
            _AssertShipping("webGLDecompressionFallback", 1);
        }

        /// <summary>The splash screen adds 2.67 MB of built-in textures.</summary>
        [Test]
        public void SplashScreen_IsDisabled()
        {
            _AssertShipping("m_ShowUnitySplashScreen", 0);
            _AssertShipping("m_ShowUnitySplashLogo", 0);
        }

        [Test]
        public void UrpAsset_HasNoHdrShadowsOrLensFlare()
        {
            Assert.That(_Property(UrpAssetPath, "m_SupportsHDR").boolValue, Is.False);
            Assert.That(_Property(UrpAssetPath, "m_MainLightShadowsSupported").boolValue, Is.False);
            Assert.That(_Property(UrpAssetPath, "m_AdditionalLightShadowsSupported").boolValue, Is.False);
            Assert.That(_Property(UrpAssetPath, "m_SupportDataDrivenLensFlare").boolValue, Is.False);
            Assert.That(_Property(UrpAssetPath, "m_SupportScreenSpaceLensFlare").boolValue, Is.False);
            Assert.That(_Property(UrpAssetPath, "m_UseAdaptivePerformance").boolValue, Is.False);

            // MsaaQuality is a sample count, not an index. Disabled is 1.
            Assert.That(_Property(UrpAssetPath, "m_MSAA").intValue, Is.EqualTo(1));
        }

        /// <summary>This field alone pulls in FilmGrain, SMAA and UberPost. About 2.9 MB.</summary>
        [Test]
        public void Urp2DRenderer_ShipsNoPostProcessData()
        {
            Assert.That(_Property(UrpRendererPath, "m_PostProcessData").objectReferenceValue,
                Is.Null);
        }

        [Test]
        public void QualityLevels_HaveShadowsDisabled()
        {
            var levels = _Property(QualitySettingsPath, "m_QualitySettings");

            Assert.That(levels.arraySize, Is.GreaterThan(0));

            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var name = level.FindPropertyRelative("name").stringValue;

                Assert.That(level.FindPropertyRelative("shadows").intValue, Is.Zero,
                    $"Quality level '{name}' still has shadows enabled.");
            }
        }

        /// <summary>Automatic keeps every variant. A 2D project has no lightmaps and no fog.</summary>
        [Test]
        public void GraphicsSettings_StripLightmapAndFogVariants()
        {
            Assert.That(_Property(GraphicsSettingsPath, "m_LightmapStripping").intValue,
                Is.EqualTo(1), "Lightmap stripping is not set to Custom.");
            Assert.That(_Property(GraphicsSettingsPath, "m_FogStripping").intValue,
                Is.EqualTo(1), "Fog stripping is not set to Custom.");

            var keepFlags = new[]
            {
                "m_LightmapKeepPlain", "m_LightmapKeepDirCombined", "m_LightmapKeepDynamicPlain",
                "m_LightmapKeepDynamicDirCombined", "m_LightmapKeepShadowMask",
                "m_LightmapKeepSubtractive", "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2"
            };

            foreach (var flag in keepFlags)
                Assert.That(_Property(GraphicsSettingsPath, flag).boolValue, Is.False,
                    $"'{flag}' is still keeping a variant set nothing in this project renders.");
        }

        /// <summary>Unity ships these shaders even if no material uses them. Keep only the 2D defaults.</summary>
        [Test]
        public void GraphicsSettings_AlwaysIncludeOnlyTheSpriteAndUiDefaults()
        {
            var shaders = _Property(GraphicsSettingsPath, "m_AlwaysIncludedShaders");
            var names = Enumerable.Range(0, shaders.arraySize)
                .Select(i => shaders.GetArrayElementAtIndex(i).objectReferenceValue)
                .Select(shader => shader == null ? "<missing>" : shader.name)
                .ToArray();

            Assert.That(names, Is.EquivalentTo(new[] { "Sprites/Default", "UI/Default" }),
                $"Always Included shaders drifted: [{string.Join(", ", names)}].");
        }

        /// <summary>The project has no audio. A clear flag ships the full FMOD runtime.</summary>
        [Test]
        public void AudioEngine_IsDisabled()
        {
            Assert.That(_Property(AudioManagerPath, "m_DisableAudio").boolValue, Is.True);
        }

        /// <summary>The default font must be the subset. The old atlas must not be in <c>Resources/</c>.</summary>
        /// <remarks>Unity ships everything in <c>Resources/</c>, referenced or not.</remarks>
        [Test]
        public void TmpDefaultFont_IsTheSubsetAndTheOldAssetIsGone()
        {
            var defaultFont = _Property(TmpSettingsPath, "m_defaultFontAsset").objectReferenceValue;

            Assert.That(defaultFont, Is.Not.Null, "TMP Settings has no default font asset.");
            Assert.That(AssetDatabase.GetAssetPath(defaultFont), Is.EqualTo(SubsetFontPath));

            Assert.That(_Property(TmpSettingsPath, "m_fallbackFontAssets").arraySize, Is.Zero,
                "A fallback font would drag a second atlas into the build.");

            var strays = AssetDatabase.FindAssets("LiberationSans")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.Contains("/Resources/"))
                .ToArray();

            Assert.That(strays, Is.Empty,
                $"A LiberationSans asset is back under Resources: [{string.Join(", ", strays)}].");
        }

        /// <summary>JsonUtility builds the Magic Words DTOs by reflection. High stripping removes them.</summary>
        /// <remarks>The failure is silent: an empty dialogue, no exception, no log.</remarks>
        [Test]
        public void LinkXml_PreservesTheSimulationAssembly()
        {
            Assert.That(File.Exists(LinkXmlPath), Is.True, $"'{LinkXmlPath}' is missing.");
            Assert.That(File.ReadAllText(LinkXmlPath), Does.Contain("Client.Simulation"));
        }

        /// <summary>Asserts one value of the shipping profile's player-settings override.</summary>
        private static void _AssertShipping(string key, int expected)
        {
            Assert.That(_ShippingValue(key), Is.EqualTo(expected.ToString()),
                $"'{key}' drifted in '{ShippingProfilePath}'.");
        }

        /// <summary>Reads one player-settings value out of the shipping build profile.</summary>
        /// <remarks>
        /// A profile stores its override as raw YAML lines, each one prefixed with <c>'| '</c>.
        /// A plain setting sits on its own line; a per-platform setting is a map whose WebGL entry
        /// sits one indent deeper. Returns the raw text, so the caller compares against the
        /// serialized number rather than a typed enum the profile never holds.
        /// </remarks>
        private static string _ShippingValue(string key)
        {
            var overrides = _Property(ShippingProfilePath, "m_PlayerSettingsYaml.m_Settings");

            Assert.That(overrides.arraySize, Is.GreaterThan(0),
                $"'{ShippingProfilePath}' no longer overrides player settings. Either restore the " +
                "override or move these guards back onto the PlayerSettings API.");

            var lines = Enumerable.Range(0, overrides.arraySize)
                .Select(i => overrides.GetArrayElementAtIndex(i).FindPropertyRelative("line")
                    .stringValue)
                .Select(line => line.StartsWith("| ", StringComparison.Ordinal)
                    ? line.Substring(2)
                    : line)
                .ToArray();

            var head = Array.FindIndex(lines,
                line => line.StartsWith($"  {key}:", StringComparison.Ordinal));

            Assert.That(head, Is.GreaterThanOrEqualTo(0),
                $"'{ShippingProfilePath}' has no '{key}'.");

            var inline = lines[head].Substring(key.Length + 3).Trim();

            if (inline.Length > 0)
                return inline;

            var webGl = lines.Skip(head + 1)
                .TakeWhile(line => line.StartsWith("    ", StringComparison.Ordinal))
                .FirstOrDefault(line => line.TrimStart().StartsWith("WebGL:", StringComparison.Ordinal));

            Assert.That(webGl, Is.Not.Null,
                $"'{key}' has no WebGL entry in '{ShippingProfilePath}'.");

            return webGl.Trim().Substring("WebGL:".Length).Trim();
        }

        /// <summary>Reads one serialized property from an asset path.</summary>
        /// <remarks>Also handles the <c>ProjectSettings/</c> singletons, which <c>LoadAssetAtPath</c> cannot open.</remarks>
        private static SerializedProperty _Property(string assetPath, string propertyPath)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);

            Assert.That(objects, Is.Not.Empty, $"'{assetPath}' holds no object.");

            var property = new SerializedObject(objects[0]).FindProperty(propertyPath);

            Assert.That(property, Is.Not.Null, $"'{assetPath}' has no '{propertyPath}'.");
            return property;
        }
    }
}
