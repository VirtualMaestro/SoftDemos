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

        private const string BuildProfileFolder = "Assets/Settings/Build Profiles";
        private const string LinkXmlPath = "Assets/Client/Source/Runtime/link.xml";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string AudioManagerPath = "ProjectSettings/AudioManager.asset";

        /// <summary>Runs first. <c>PlayerSettings</c> reads go through the active build profile.</summary>
        /// <remarks>A profile with overrides makes every other test here describe the profile, not the project.</remarks>
        [Test]
        public void BuildProfiles_DoNotOverridePlayerSettings()
        {
            var profiles = AssetDatabase.FindAssets(string.Empty, new[] { BuildProfileFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => path.EndsWith(".asset"))
                .ToArray();

            Assert.That(profiles, Is.Not.Empty,
                $"No build profile found under '{BuildProfileFolder}'.");

            foreach (var path in profiles)
            {
                var overrides = _Property(path, "m_PlayerSettingsYaml.m_Settings");

                Assert.That(overrides, Is.Not.Null,
                    $"'{path}' has no m_PlayerSettingsYaml — is it still a BuildProfile?");
                Assert.That(overrides.arraySize, Is.Zero,
                    $"'{path}' overrides player settings ({overrides.arraySize} lines). Remove the " +
                    "override so the global settings stay the single source of truth.");
            }
        }

        /// <summary><c>Release</c> is intentional. <c>Master</c> adds link-time optimization and a much slower build.</summary>
        [Test]
        public void WebGlPlayerSettings_StayOptimised()
        {
            var web = NamedBuildTarget.WebGL;

            Assert.That(PlayerSettings.GetManagedStrippingLevel(web),
                Is.EqualTo(ManagedStrippingLevel.High));
            Assert.That(PlayerSettings.GetIl2CppCompilerConfiguration(web),
                Is.EqualTo(Il2CppCompilerConfiguration.Release));
            Assert.That(PlayerSettings.GetIl2CppCodeGeneration(web),
                Is.EqualTo(Il2CppCodeGeneration.OptimizeSize));
            Assert.That(PlayerSettings.stripEngineCode, Is.True);
        }

        /// <summary>GitHub Pages does not send <c>Content-Encoding: br</c>. Brotli needs the fallback.</summary>
        [Test]
        public void WebGlCompression_IsBrotliWithFallback()
        {
            Assert.That(PlayerSettings.WebGL.compressionFormat,
                Is.EqualTo(WebGLCompressionFormat.Brotli));
            Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.True);
        }

        /// <summary>The splash screen adds 2.67 MB of built-in textures.</summary>
        [Test]
        public void SplashScreen_IsDisabled()
        {
            Assert.That(PlayerSettings.SplashScreen.show, Is.False);
            Assert.That(PlayerSettings.SplashScreen.showUnityLogo, Is.False);
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
