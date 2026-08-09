using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Simulation.Tests
{
    /// <summary>
    /// Locks in the settings the WebGL Build Optimization milestone chose. Every one of them is a
    /// value in a settings asset with no code behind it, so nothing else in the project would
    /// notice if it were reverted — a reverted flag costs megabytes in a build nobody runs until
    /// release day.
    ///
    /// <b>Everything is read through <see cref="SerializedObject"/> over
    /// <see cref="AssetDatabase"/>, never through a typed reference.</b>
    /// <c>Game.Simulation.Tests.asmdef</c> is <c>overrideReferences: true</c> and references
    /// neither URP nor TextMesh Pro; adding either to reach a strongly typed property would widen
    /// the boundary that <see cref="ArchitectureTests"/> exists to defend.
    /// </summary>
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

        /// <summary>
        /// The precondition every <see cref="PlayerSettings"/> assertion below depends on, which is
        /// why it comes first. <c>PlayerSettings</c> reads route through the active build profile,
        /// so a profile carrying serialized overrides makes those assertions describe the profile
        /// rather than the project — the baseline build shipped a 33 MB wasm exactly that way,
        /// with Brotli and Release sitting unread in ProjectSettings.asset. With no profile
        /// overriding anything, the global settings are the single source of truth and a plain
        /// File > Build and Run cannot miss them.
        /// </summary>
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

        /// <summary>
        /// <c>Release</c> rather than <c>Master</c> is deliberate. Master adds link-time
        /// optimization, which buys a few percent of wasm at the cost of a build slow enough to
        /// stop anyone from measuring anything; the size work in this milestone is dominated by
        /// stripping, the splash screen and the font atlas, none of which LTO touches. Master is a
        /// release-day switch, and if it is ever flipped this assertion is the place to say so.
        /// </summary>
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

        /// <summary>
        /// GitHub Pages does not serve <c>Content-Encoding: br</c>, so Brotli without the
        /// decompression fallback is a build that loads locally and fails on the hosted link.
        /// The two belong together and are asserted together.
        /// </summary>
        [Test]
        public void WebGlCompression_IsBrotliWithFallback()
        {
            Assert.That(PlayerSettings.WebGL.compressionFormat,
                Is.EqualTo(WebGLCompressionFormat.Brotli));
            Assert.That(PlayerSettings.WebGL.decompressionFallback, Is.True);
        }

        /// <summary>2.67 MB of built-in texture — the largest single asset in the baseline.</summary>
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

            // MsaaQuality is a sample count, not an index: Disabled = 1, _2x = 2, _4x = 4, _8x = 8.
            Assert.That(_Property(UrpAssetPath, "m_MSAA").intValue, Is.EqualTo(1));
        }

        /// <summary>
        /// The single reference worth ~2.9 MB: ten FilmGrain textures, the SMAA area texture and
        /// UberPost enter the build through this field and nothing else.
        /// </summary>
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

        /// <summary>
        /// Lightmap and fog stripping default to Automatic, which keeps every variant. There are no
        /// lightmaps and no fog in a 2D project, so Custom with every keep flag cleared is the
        /// honest setting.
        /// </summary>
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

        /// <summary>
        /// Always Included shaders are the ones Unity ships whether or not a material references
        /// them. Only the sprite and uGUI defaults earn that here; the legacy, cubemap and Android
        /// ETC1 entries do not apply to a 2D URP project.
        /// </summary>
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

        /// <summary>
        /// No AudioClip and no AudioSource exists anywhere in the project; with this flag clear the
        /// whole FMOD runtime ships in the wasm for nothing.
        /// </summary>
        [Test]
        public void AudioEngine_IsDisabled()
        {
            Assert.That(_Property(AudioManagerPath, "m_DisableAudio").boolValue, Is.True);
        }

        /// <summary>
        /// The subset atlas replaced a 2.26 MB asset that lived in <c>Resources/</c> and therefore
        /// shipped whether or not anything referenced it. Both halves matter: the default font has
        /// to be the subset, and the old asset has to be gone rather than merely unreferenced.
        /// </summary>
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

        /// <summary>
        /// High stripping removes what the linker cannot see referenced, and the Magic Words DTOs
        /// are only ever built by JsonUtility. Losing them is silent — an empty dialogue, no
        /// exception, no log — so the file's existence is worth asserting on its own.
        /// </summary>
        [Test]
        public void LinkXml_PreservesTheSimulationAssembly()
        {
            Assert.That(File.Exists(LinkXmlPath), Is.True, $"'{LinkXmlPath}' is missing.");
            Assert.That(File.ReadAllText(LinkXmlPath), Does.Contain("Game.Simulation"));
        }

        /// <summary>
        /// Reads one serialized property from an asset by path. Handles both project assets and the
        /// <c>ProjectSettings/</c> singletons, which hold their single object at index 0 and are not
        /// reachable through <c>LoadAssetAtPath</c>.
        /// </summary>
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
