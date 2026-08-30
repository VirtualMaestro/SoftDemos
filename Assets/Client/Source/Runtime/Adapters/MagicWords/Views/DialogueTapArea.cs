using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Client.Adapters.MagicWords.Views
{
    [RequireComponent(typeof(Graphic))]
    public sealed class DialogueTapArea : MonoBehaviour, IPointerClickHandler
    {
        [FormerlySerializedAs("sceneView")]
        [SerializeField] private MagicWordsScreen screen;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;

            screen.SkipRequested = true;
        }

        private void OnValidate()
        {
            Debug.Assert(screen != null, $"'{nameof(screen)}' is not assigned on {nameof(DialogueTapArea)}.", this);
        }
    }
}
