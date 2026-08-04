using System.Collections;
using UnityEngine;

namespace MBG.UI
{
    /// <summary>
    /// Base class semua panel UI. Menangani satu hal saja: menampilkan dan
    /// menyembunyikan diri dengan fade lewat CanvasGroup.
    ///
    /// Fade memakai waktu unscaled — bukan <c>GameClock.DeltaTime</c> — supaya
    /// panel tetap muncul mulus justru pada saat clock gameplay sedang di-pause
    /// (pause menu, hasil QTE, game over).
    ///
    /// Turunan mengisi kontennya lewat <see cref="OnShow"/> / <see cref="OnHide"/>,
    /// bukan dengan meng-override Show/Hide.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public abstract class UIPanel : MonoBehaviour
    {
        [Header("UIPanel")]
        [Tooltip("Panel terlihat begitu scene mulai. Biasanya hanya HUD.")]
        [SerializeField] bool visibleOnAwake = false;

        [Tooltip("Durasi fade dalam detik. Isi -1 untuk memakai nilai dari UIStyle.")]
        [SerializeField] float fadeDurationOverride = -1f;

        [Tooltip("Nonaktifkan GameObject setelah fade-out selesai, supaya panel " +
                 "tersembunyi tidak ikut memakan biaya layout dan raycast.")]
        [SerializeField] bool deactivateWhenHidden = true;

        [Tooltip("Panel menangkap klik mouse saat terlihat. Matikan untuk overlay " +
                 "informatif seperti HUD yang tidak boleh memblokir dunia di belakangnya.")]
        [SerializeField] bool blockRaycastsWhenVisible = true;

        CanvasGroup _group;
        Coroutine _fade;
        bool _cached;

        /// <summary>True setelah <see cref="Show"/>, walau fade-in belum selesai.</summary>
        public bool IsVisible { get; private set; }

        public CanvasGroup Group
        {
            get { EnsureCached(); return _group; }
        }

        /// <summary>Durasi fade efektif: override lokal kalau diisi, kalau tidak dari UIStyle.</summary>
        public float FadeDuration
        {
            get
            {
                if (fadeDurationOverride >= 0f) return fadeDurationOverride;
                UIStyle style = UIManager.Style;
                return style != null ? style.panelFadeDuration : 0.15f;
            }
        }

        protected virtual void Awake()
        {
            EnsureCached();

            // allowDeactivate sengaja false: Awake berjalan di tengah SetActive(true)
            // yang dipanggil Show(), jadi menonaktifkan GameObject di sini akan
            // membatalkan Show itu sendiri dan membuat coroutine fade gagal start.
            // Penonaktifan hanya boleh terjadi lewat Hide().
            ApplyInstant(visibleOnAwake, allowDeactivate: false);
        }

        protected virtual void OnDestroy()
        {
            if (UIManager.Instance != null) UIManager.Instance.Unregister(this);
        }

        /// <summary>Tampilkan panel. Aman dipanggil berulang kali.</summary>
        public void Show(bool instant = false)
        {
            EnsureCached();

            // GameObject yang nonaktif tidak bisa menjalankan coroutine, jadi
            // aktifkan dulu — Awake turunan ikut jalan di sini.
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            bool wasVisible = IsVisible;
            IsVisible = true;

            if (instant || FadeDuration <= 0f) ApplyInstant(true);
            else StartFade(1f);

            if (!wasVisible) OnShow();
        }

        /// <summary>Sembunyikan panel. Aman dipanggil berulang kali.</summary>
        public void Hide(bool instant = false)
        {
            EnsureCached();

            bool wasVisible = IsVisible;
            IsVisible = false;

            if (instant || FadeDuration <= 0f || !gameObject.activeInHierarchy)
            {
                ApplyInstant(false);
            }
            else
            {
                _group.interactable = false;
                _group.blocksRaycasts = false;
                StartFade(0f);
            }

            if (wasVisible) OnHide();
        }

        public void Toggle(bool instant = false)
        {
            if (IsVisible) Hide(instant);
            else Show(instant);
        }

        /// <summary>Dipanggil sekali setiap kali panel berpindah ke keadaan terlihat.</summary>
        protected virtual void OnShow() { }

        /// <summary>Dipanggil sekali setiap kali panel berpindah ke keadaan tersembunyi.</summary>
        protected virtual void OnHide() { }

        void EnsureCached()
        {
            if (_cached && _group != null) return;

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _cached = true;
        }

        void ApplyInstant(bool visible, bool allowDeactivate = true)
        {
            StopFade();

            IsVisible = visible;
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible && blockRaycastsWhenVisible;

            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
            else if (!visible && allowDeactivate && deactivateWhenHidden && gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        void StartFade(float target)
        {
            StopFade();
            _fade = StartCoroutine(FadeRoutine(target));
        }

        void StopFade()
        {
            if (_fade == null) return;
            StopCoroutine(_fade);
            _fade = null;
        }

        IEnumerator FadeRoutine(float target)
        {
            float duration = FadeDuration;
            float from = _group.alpha;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }

            _group.alpha = target;
            _fade = null;

            bool visible = target > 0.99f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible && blockRaycastsWhenVisible;

            if (!visible && deactivateWhenHidden) gameObject.SetActive(false);
        }
    }
}
