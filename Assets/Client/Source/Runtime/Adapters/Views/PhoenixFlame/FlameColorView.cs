using UnityEngine;

namespace Game.Adapters.Views
{
    /// <summary>
    /// The animated colour of the fire. <b>Only the Animator writes <c>tint</c></b>; a second writer
    /// would make the crossfade non-deterministic and unassertable. Every write reaches the flame
    /// and spark renderers through one cached <see cref="MaterialPropertyBlock"/> each, so particles
    /// that are already alive retint on the same frame — writing <c>main.startColor</c> instead would
    /// only reach particles born after the write and read as a wipe, not a blend.
    ///
    /// Smoke is deliberately left untinted: grey smoke over a coloured flame is what makes the colour
    /// legible.
    /// </summary>
    public sealed class FlameColorView : MonoBehaviour
    {
        // The animation clips address this field by name (tint.r/g/b/a). Renaming it breaks them
        // silently — a curve whose binding no longer resolves is simply not applied. The default is
        // the FlameOrange clip colour so the one frame before the Animator's first write shows the
        // starting colour rather than a flash of black or magenta.
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

        // Unity calls this after the Animator has written the animated fields for the frame. It is
        // the only place `tint` becomes visible, and it does not fire on frames the Animator skips —
        // which is what keeps this view off the per-frame allocation path.
        private void OnDidApplyAnimationProperties()
        {
            _ApplyTint();
        }

        /// <summary>
        /// Hands the atlas sprites to the four systems. The Texture Sheet Animation module resolves
        /// the packed rect itself, so nothing here depends on the atlas packing flags; the property
        /// block carries the atlas page the sprite lives on. The glow layer reuses the flame frames
        /// through its additive material — that is what shapes the hot core like the fire instead
        /// of a soft ball — so the stage still owns exactly one copy of each sprite.
        /// </summary>
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

        /// <summary>
        /// Drops both references to the sprites — the module's slot and the property block's texture.
        /// The stage calls this <b>before</b> it destroys the atlas sprite copies; the other order
        /// leaves a particle system pointing at a destroyed sprite, which renders untextured and logs
        /// one warning per system on the next open.
        /// </summary>
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

            // SetSprite only writes an existing slot; AddSprite grows the list. Together they make
            // the call independent of how many slots the scene happens to be authored with.
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
