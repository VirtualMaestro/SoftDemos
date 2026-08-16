using UnityEngine;

namespace Client.Adapters.Views
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CardView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite _back;
        private Sprite _face;
        private bool _faceShown;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Configure(Sprite back, Sprite face)
        {
            _back = back;
            _face = face;
            ResetToBack();
        }

        public void ResetToBack()
        {
            transform.localEulerAngles = Vector3.zero;
            _renderer.flipX = false;
            _renderer.sprite = _back;
            _faceShown = false;
        }

        public void SetSortingOrder(int sortingOrder)
        {
            _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Called from the move tween's OnUpdate exactly once per rendered frame. Must not touch
        /// the ECS world.
        /// </summary>
        public void OnMoveProgress(float normalized)
        {
            transform.localEulerAngles = new Vector3(0f, normalized * 180f, 0f);

            if (_faceShown || normalized < 0.5f)
                return;

            _renderer.sprite = _face;
            _renderer.flipX = true;
            _faceShown = true;
        }

        public void MoveEnded()
        {
            transform.localEulerAngles = new Vector3(0f, _renderer.sprite == _face ? 180f : 0f, 0f);
            _faceShown = false;
        }
    }
}
