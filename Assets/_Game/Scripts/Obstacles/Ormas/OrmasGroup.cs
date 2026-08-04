using MBG.Core;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Grup ormas placeholder yang nongkrong di depan rumah. Satu-satunya tugasnya:
    /// menjadi target interaksi "[F] Hadapi mereka".
    ///
    /// Grup tidak tahu apa-apa tentang gangguan yang sedang berjalan — ia hanya
    /// memberi tahu <see cref="OrmasEncounterController"/> bahwa pemain menghampiri.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class OrmasGroup : MonoBehaviour, IInteractable, IInteractableFocus
    {
        [SerializeField] string promptText = "Hadapi mereka";
        [SerializeField] SpriteRenderer highlight;

        [Range(0f, 1f)]
        [SerializeField] float highlightAlpha = 0.8f;

        [SerializeField] float highlightFadeSpeed = 12f;

        public string Prompt => promptText;

        bool _focused;
        float _alpha;

        public void Configure(string prompt) => promptText = string.IsNullOrWhiteSpace(prompt) ? promptText : prompt;

        public void SetHighlight(SpriteRenderer renderer) => highlight = renderer;

        void Awake() => ApplyHighlight(0f);

        void OnDisable()
        {
            _focused = false;
            ApplyHighlight(0f);
        }

        void Update()
        {
            float target = _focused ? highlightAlpha : 0f;
            if (Mathf.Approximately(_alpha, target)) return;

            ApplyHighlight(Mathf.MoveTowards(_alpha, target, highlightFadeSpeed * GameClock.DeltaTime));
        }

        public void Interact(PlayerInteractor interactor)
        {
            OrmasEncounterController controller = OrmasEncounterController.Instance;
            if (controller == null)
            {
                Debug.LogWarning("[Ormas] OrmasEncounterController tidak ada di scene.", this);
                return;
            }

            controller.Confront();
        }

        public void OnFocusEnter() => _focused = true;

        public void OnFocusExit() => _focused = false;

        void OnTriggerEnter2D(Collider2D other)
        {
            var interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null) interactor.Add(this);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null) interactor.Remove(this);
        }

        void ApplyHighlight(float alpha)
        {
            _alpha = alpha;
            if (highlight == null) return;

            Color c = highlight.color;
            c.a = alpha;
            highlight.color = c;
            highlight.enabled = alpha > 0.001f;
        }
    }
}
