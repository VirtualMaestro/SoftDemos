using TMPro;
using UnityEngine;

namespace Client.Adapters.Shell
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class FpsView : MonoBehaviour
    {
        private const float RefreshInterval = 0.5f;

        [SerializeField] private TMP_Text label;

        private float _elapsed;
        private int _frames;

        private void Awake()
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;

            if (_elapsed < RefreshInterval)
                return;

            label.SetText("{0:0} FPS", _frames / _elapsed);
            _elapsed = 0f;
            _frames = 0;
        }
    }
}
