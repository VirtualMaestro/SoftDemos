using UnityEngine;

namespace Client.Adapters.Views
{
    /// <summary>Holds the animated colour of the fire.</summary>
    /// <remarks>
    /// Only the Animator writes <c>tint</c>. A second writer makes the crossfade non-deterministic.
    /// Each write goes to the renderers through a cached <see cref="MaterialPropertyBlock"/>, so
    /// live particles change colour in the same frame. Do not write <c>main.startColor</c>: it
    /// reaches only new particles and looks like a wipe. Smoke stays grey to keep the colour clear.
    /// </remarks>
    public sealed class FlameColorView : MonoBehaviour
    {
        // The animation clips bind to this field by name. Do not rename it: the clips fail silently.
        // The default is the FlameOrange colour, for the one frame before the Animator writes.
        [SerializeField] private Color tint = new(1f, 0.45f, 0.1f, 1f);
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem smokeParticles;
        [SerializeField] private ParticleSystem sparkParticles;
        [SerializeField] private ParticleSystem glowParticles;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private ParticleSystemRenderer _flameRenderer;
        private ParticleSystemRenderer _smokeRenderer;
        private ParticleSystemRenderer _sparkRenderer;
        private ParticleSystemRenderer _glowRenderer;
        private MaterialPropertyBlock _flameBlock;
        private MaterialPropertyBlock _smokeBlock;
        private MaterialPropertyBlock _sparkBlock;
        private MaterialPropertyBlock _glowBlock;
        private bool _isWired;

        public Color Tint => tint;

        private void Awake()
        {
            _isWired = _HasEveryReference();

            if (_isWired == false)
                return;

            _flameRenderer = flameParticles.GetComponent<ParticleSystemRenderer>();
            _smokeRenderer = smokeParticles.GetComponent<ParticleSystemRenderer>();
            _sparkRenderer = sparkParticles.GetComponent<ParticleSystemRenderer>();
            _glowRenderer = glowParticles.GetComponent<ParticleSystemRenderer>();
            _flameBlock = new MaterialPropertyBlock();
            _smokeBlock = new MaterialPropertyBlock();
            _sparkBlock = new MaterialPropertyBlock();
            _glowBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _ApplyTint();
        }

        // Unity calls this after the Animator writes the animated fields. It does not fire on
        // frames the Animator skips, which keeps this view off the per-frame path.
        private void OnDidApplyAnimationProperties()
        {
            _ApplyTint();
        }

        /// <summary>Gives the atlas sprites to the four particle systems.</summary>
        /// <remarks>
        /// The Texture Sheet Animation module resolves the packed rect itself. The glow layer
        /// reuses the flame frames with an additive material, so the stage owns one copy of each.
        /// </remarks>
        public void SetSprites(Sprite[] flameFrames, Sprite smoke, Sprite spark)
        {
            if (_isWired == false)
                return;

            _SetFrames(flameParticles, _flameRenderer, _flameBlock, flameFrames);
            _SetSprite(smokeParticles, _smokeRenderer, _smokeBlock, smoke);
            _SetSprite(sparkParticles, _sparkRenderer, _sparkBlock, spark);
            _SetFrames(glowParticles, _glowRenderer, _glowBlock, flameFrames);
            _ApplyTint();
        }

        /// <summary>Releases both sprite references: the module slot and the property block texture.</summary>
        /// <remarks>Call this before you destroy the sprite copies, or the particle systems keep dead references.</remarks>
        public void ClearSprites()
        {
            if (_isWired == false)
                return;

            _ClearSprite(flameParticles, _flameRenderer, _flameBlock);
            _ClearSprite(smokeParticles, _smokeRenderer, _smokeBlock);
            _ClearSprite(sparkParticles, _sparkRenderer, _sparkBlock);
            _ClearSprite(glowParticles, _glowRenderer, _glowBlock);
            _ApplyTint();
        }

        private void _ApplyTint()
        {
            if (_isWired == false)
                return;

            _flameBlock.SetColor(BaseColorId, tint);
            _flameRenderer.SetPropertyBlock(_flameBlock);
            _sparkBlock.SetColor(BaseColorId, tint);
            _sparkRenderer.SetPropertyBlock(_sparkBlock);
            _glowBlock.SetColor(BaseColorId, tint);
            _glowRenderer.SetPropertyBlock(_glowBlock);
        }

        private static void _SetSprite(ParticleSystem particles, ParticleSystemRenderer targetRenderer,
            MaterialPropertyBlock block, Sprite sprite)
        {
            var textureSheet = particles.textureSheetAnimation;
            textureSheet.SetSprite(0, sprite);

            if (sprite != null)
                block.SetTexture(BaseMapId, sprite.texture);

            targetRenderer.SetPropertyBlock(block);
        }

        private static void _SetFrames(ParticleSystem particles, ParticleSystemRenderer targetRenderer,
            MaterialPropertyBlock block, Sprite[] frames)
        {
            var textureSheet = particles.textureSheetAnimation;

            // SetSprite writes an existing slot. AddSprite grows the list. Use both, because the
            // scene can be authored with any number of slots.
            for (var i = 0; i < frames.Length; i++)
            {
                if (i < textureSheet.spriteCount)
                    textureSheet.SetSprite(i, frames[i]);
                else
                    textureSheet.AddSprite(frames[i]);
            }

            if (frames.Length > 0 && frames[0] != null)
                block.SetTexture(BaseMapId, frames[0].texture);

            targetRenderer.SetPropertyBlock(block);
        }

        private static void _ClearSprite(ParticleSystem particles,
            ParticleSystemRenderer targetRenderer, MaterialPropertyBlock block)
        {
            var textureSheet = particles.textureSheetAnimation;

            for (var i = 0; i < textureSheet.spriteCount; i++)
                textureSheet.SetSprite(i, null);
            block.Clear();
            targetRenderer.SetPropertyBlock(block);
        }

        private bool _HasEveryReference()
        {
            var isComplete = true;
            isComplete &= _Check(flameParticles, nameof(flameParticles));
            isComplete &= _Check(smokeParticles, nameof(smokeParticles));
            isComplete &= _Check(sparkParticles, nameof(sparkParticles));
            isComplete &= _Check(glowParticles, nameof(glowParticles));
            return isComplete;
        }

        private bool _Check(UnityEngine.Object reference, string fieldName)
        {
            if (reference != null)
                return true;

            Debug.LogError($"{fieldName} is not assigned on {nameof(FlameColorView)}.", this);
            return false;
        }
    }
}
