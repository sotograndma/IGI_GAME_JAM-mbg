using System;
using System.Collections.Generic;
using MBG.Core;
using UnityEngine;

namespace MBG.Kitchen
{
    /// <summary>
    /// Satu station dapur. Pemain mendekat, prompt muncul, tekan F.
    ///
    /// Logic memasak yang sesungguhnya belum ada di sini: untuk sekarang
    /// <see cref="Interact"/> hanya menyiarkan
    /// <see cref="GameEventBus.OnStationUsed"/> dan menulis Debug.Log. Sistem
    /// pesanan nanti tinggal subscribe ke event itu.
    ///
    /// Relevansi station ditentukan dari luar lewat <see cref="RelevanceCheck"/> —
    /// station tidak boleh tahu apa-apa tentang sistem pesanan. Selama belum ada
    /// yang mengisinya, semua station dianggap relevan.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public class KitchenStation : MonoBehaviour, IInteractable, IInteractableFocus
    {
        [Header("Identitas")]
        [SerializeField] StationType stationType = StationType.Prep;

        [Tooltip("Kosongkan untuk memakai prompt bawaan tipe station.")]
        [SerializeField] string promptText = "";

        [Tooltip("Prompt saat station belum boleh dipakai. Kosongkan untuk memakai " +
                 "bawaan tipe station — \"Catering belum siap\" untuk Handover, " +
                 "\"Belum saatnya\" untuk sisanya.")]
        [SerializeField] string irrelevantPromptText = "";

        [Header("Referensi")]
        [Tooltip("Titik berdiri pemain saat memakai station ini.")]
        [SerializeField] Transform playerStandPoint;

        [Tooltip("Sprite yang menyala saat station jadi target interaksi terdekat.")]
        [SerializeField] SpriteRenderer highlight;

        [Header("Highlight (diisi dari KitchenLayoutSO)")]
        [Range(0f, 1f)]
        [SerializeField] float highlightAlpha = 0.85f;
        [SerializeField] float highlightFadeSpeed = 12f;

        [Header("Debug")]
        [Tooltip("Paksa station ini dianggap tidak relevan, untuk menguji prompt " +
                 "\"Belum saatnya\" tanpa menunggu sistem pesanan.")]
        [SerializeField] bool debugForceIrrelevant = false;

        /// <summary>
        /// Diisi sistem pesanan nanti: kembalikan true kalau station bertipe itu
        /// adalah langkah yang sedang dibutuhkan. Null = semua station relevan.
        /// </summary>
        public static Func<StationType, bool> RelevanceCheck;

        /// <summary>
        /// Station yang sedang aktif di scene, dikunci per tipe. Dipakai UI untuk
        /// tahu ke arah mana pemain harus berjalan, tanpa perlu memegang referensi
        /// ke satu pun station.
        /// </summary>
        static readonly Dictionary<StationType, KitchenStation> _registry = new();

        /// <summary>Posisi station bertipe tertentu di world space.</summary>
        public static bool TryGetPosition(StationType type, out Vector3 position)
        {
            if (_registry.TryGetValue(type, out KitchenStation station) && station != null)
            {
                position = station.PlayerStandPoint.position;
                return true;
            }

            position = Vector3.zero;
            return false;
        }

        public StationType Type => stationType;

        /// <summary>Titik berdiri pemain; fallback ke posisi station sendiri.</summary>
        public Transform PlayerStandPoint => playerStandPoint != null ? playerStandPoint : transform;

        public bool IsRelevant =>
            !debugForceIrrelevant && (RelevanceCheck == null || RelevanceCheck(stationType));

        public string Prompt => IsRelevant ? ResolvedPrompt : ResolvedIrrelevantPrompt;

        string ResolvedPrompt =>
            string.IsNullOrWhiteSpace(promptText) ? stationType.GetDefaultPrompt() : promptText;

        string ResolvedIrrelevantPrompt =>
            string.IsNullOrWhiteSpace(irrelevantPromptText)
                ? stationType.GetDefaultIrrelevantPrompt()
                : irrelevantPromptText;

        bool _focused;
        float _alpha;

        void Awake() => ApplyHighlightAlpha(0f);

        void OnEnable() => _registry[stationType] = this;

        void OnDisable()
        {
            if (_registry.TryGetValue(stationType, out KitchenStation registered) && registered == this)
                _registry.Remove(stationType);

            // Area interior dimatikan lewat SetActive; jangan tinggalkan highlight
            // menyala saat pemain kembali nanti.
            _focused = false;
            ApplyHighlightAlpha(0f);
        }

        void Update()
        {
            float target = _focused ? highlightAlpha : 0f;
            if (Mathf.Approximately(_alpha, target)) return;

            float step = highlightFadeSpeed * GameClock.DeltaTime;
            ApplyHighlightAlpha(Mathf.MoveTowards(_alpha, target, step));
        }

        // ---- IInteractable ----------------------------------------------

        public void Interact(PlayerInteractor interactor)
        {
            if (!IsRelevant)
            {
                Debug.Log($"[KitchenStation] {stationType}: belum saatnya dipakai.", this);
                return;
            }

            Debug.Log($"[KitchenStation] {stationType} dipakai — \"{ResolvedPrompt}\".", this);

            AudioService.PlaySFX(SfxId.Pickup);
            GameEventBus.RaiseStationUsed(stationType);
        }

        // ---- IInteractableFocus ------------------------------------------

        public void OnFocusEnter() => _focused = true;

        public void OnFocusExit() => _focused = false;

        // ---- Zona interaksi ----------------------------------------------

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

        // ---- Internal -----------------------------------------------------

        void ApplyHighlightAlpha(float alpha)
        {
            _alpha = alpha;
            if (highlight == null) return;

            Color c = highlight.color;
            c.a = alpha;
            highlight.color = c;

            // Renderer dimatikan saat benar-benar transparan supaya tidak ikut
            // menambah draw call untuk sesuatu yang tak terlihat.
            highlight.enabled = alpha > 0.001f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            RelevanceCheck = null;
            _registry.Clear();
        }
    }
}
