using MBG.Core;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Dukun placeholder di dunia luar. Tugasnya hanya menjadi target interaksi
    /// "[F] Hentikan santetnya"; ia tidak tahu apa-apa soal gangguan yang berjalan.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class DukunNPC : MonoBehaviour, IInteractable, IInteractableFocus
    {
        [SerializeField] string promptText = "Hentikan santetnya";
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
            SantetPresenter presenter = SantetPresenter.Instance;
            if (presenter == null)
            {
                Debug.LogWarning("[Santet] SantetPresenter tidak ada di scene.", this);
                return;
            }

            presenter.Confront();
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
