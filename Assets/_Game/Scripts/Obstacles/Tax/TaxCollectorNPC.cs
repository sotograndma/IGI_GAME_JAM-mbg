using MBG.Core;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Petugas pungli placeholder di dunia luar. Hanya target interaksi; seluruh
    /// naskah dan aturannya ada di <see cref="IllegalTaxObstacle"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class TaxCollectorNPC : MonoBehaviour, IInteractable, IInteractableFocus
    {
        [SerializeField] string promptText = "Layani petugas";
        [SerializeField] SpriteRenderer highlight;

        [Range(0f, 1f)]
        [SerializeField] float highlightAlpha = 0.8f;

        [SerializeField] float highlightFadeSpeed = 12f;

        public string Prompt => promptText;

        bool _focused;
        float _alpha;

        public void Configure(string prompt, SpriteRenderer highlightRenderer)
        {
            if (!string.IsNullOrWhiteSpace(prompt)) promptText = prompt;
            highlight = highlightRenderer;
        }

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
            TaxPresenter presenter = TaxPresenter.Instance;
            if (presenter == null)
            {
                Debug.LogWarning("[Pungli] TaxPresenter tidak ada di scene.", this);
                return;
            }

            presenter.Meet();
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
