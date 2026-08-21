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

        private void Awake()
        {
            if (screen == null)
                Debug.LogError($"{nameof(screen)} is not assigned on {nameof(DialogueTapArea)}.", this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
                return;

            screen.RaiseSkipPressed();
        }
    }
}
