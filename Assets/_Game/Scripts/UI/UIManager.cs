using System;
using System.Collections.Generic;
using MBG.Core;
using UnityEngine;

namespace MBG.UI
{
    /// <summary>
    /// Pendaftar semua <see cref="UIPanel"/> di bawah UICanvas dan satu-satunya
    /// pintu untuk membuka/menutup panel. Sistem gameplay memanggil
    /// <c>UIManager.Instance.ShowPanel&lt;QTEPanel&gt;()</c> — bukan menyimpan
    /// referensi langsung ke panel-nya.
    ///
    /// Singleton per-scene, tanpa DontDestroyOnLoad, seperti service lain.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        [Header("Tema")]
        [Tooltip("Palet warna & ukuran font untuk seluruh UI. Diisi otomatis oleh " +
                 "Tools > MBG > Build UI Hierarchy.")]
        [SerializeField] UIStyle style;

        public static UIManager Instance { get; private set; }

        /// <summary>Tema aktif. Null-safe: panel punya fallback sendiri.</summary>
        public static UIStyle Style => Instance != null ? Instance.style : null;

        readonly Dictionary<Type, UIPanel> _panels = new();

        static bool _missingStyleWarned;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[UIManager] Sudah ada instance di '{Instance.name}'. Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            RegisterPanelsInChildren();

            // Panel muncul karena state berubah, bukan karena sistem gameplay
            // memanggil UI. Disubscribe di Awake (bukan OnEnable) supaya tetap
            // jalan walau panelnya sendiri sedang nonaktif.
            GameEventBus.OnGameStateChanged += HandleGameStateChanged;

            if (style == null && !_missingStyleWarned)
            {
                _missingStyleWarned = true;
                Debug.LogWarning("[UIManager] UIStyle belum di-assign. Jalankan Tools > MBG > Build UI Hierarchy. " +
                                 "Sementara UI memakai nilai default bawaan komponen.", this);
            }
        }

        void OnDestroy()
        {
            GameEventBus.OnGameStateChanged -= HandleGameStateChanged;
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Pemetaan GameState ke panel.
        /// TODO: Paused -> PausePanel, GameOver -> GameOverPanel,
        /// DaySummary -> DaySummaryPanel begitu isinya ada.
        /// </summary>
        void HandleGameStateChanged(GameState previous, GameState next)
        {
            if (next == GameState.InQTE) ShowPanel<QTEPanel>();
            else if (previous == GameState.InQTE) HidePanel<QTEPanel>();

            // HUD hanya untuk state bermain. InObstacle sengaja TIDAK termasuk —
            // begitu indikator gangguan diisi, state itu mungkin perlu ditambahkan.
            bool hudVisible = next == GameState.Playing || next == GameState.InQTE;
            if (hudVisible) ShowPanel<HUDPanel>();
            else HidePanel<HUDPanel>();
        }

        /// <summary>
        /// Daftarkan ulang seluruh panel di bawah GameObject ini, termasuk yang
        /// sedang nonaktif. Idempoten — panel yang sudah terdaftar tidak dobel.
        /// </summary>
        public void RegisterPanelsInChildren()
        {
            _panels.Clear();

            var found = GetComponentsInChildren<UIPanel>(includeInactive: true);
            for (int i = 0; i < found.Length; i++)
                Register(found[i]);
        }

        public void Register(UIPanel panel)
        {
            if (panel == null) return;

            Type key = panel.GetType();
            if (_panels.TryGetValue(key, out UIPanel existing) && existing != null && existing != panel)
            {
                Debug.LogWarning($"[UIManager] Ada dua {key.Name} di scene ('{existing.name}' dan '{panel.name}'). " +
                                 "Yang kedua diabaikan.", panel);
                return;
            }

            _panels[key] = panel;
        }

        public void Unregister(UIPanel panel)
        {
            if (panel == null) return;

            Type key = panel.GetType();
            if (_panels.TryGetValue(key, out UIPanel existing) && existing == panel)
                _panels.Remove(key);
        }

        public T GetPanel<T>() where T : UIPanel
        {
            if (_panels.TryGetValue(typeof(T), out UIPanel panel) && panel != null)
                return (T)panel;

            Debug.LogWarning($"[UIManager] Panel {typeof(T).Name} tidak terdaftar. " +
                             "Jalankan Tools > MBG > Build UI Hierarchy.", this);
            return null;
        }

        public T ShowPanel<T>(bool instant = false) where T : UIPanel
        {
            T panel = GetPanel<T>();
            if (panel != null) panel.Show(instant);
            return panel;
        }

        public T HidePanel<T>(bool instant = false) where T : UIPanel
        {
            T panel = GetPanel<T>();
            if (panel != null) panel.Hide(instant);
            return panel;
        }

        public bool IsPanelVisible<T>() where T : UIPanel
        {
            return _panels.TryGetValue(typeof(T), out UIPanel panel) && panel != null && panel.IsVisible;
        }

        /// <summary>Tutup semua panel. <paramref name="except"/> dibiarkan apa adanya.</summary>
        public void HideAll(bool instant = false, UIPanel except = null)
        {
            foreach (UIPanel panel in _panels.Values)
            {
                if (panel == null || panel == except) continue;
                panel.Hide(instant);
            }
        }

        /// <summary>Tutup semua panel kecuali satu tipe tertentu — misal sisakan HUD.</summary>
        public void HideAllExcept<T>(bool instant = false) where T : UIPanel
        {
            _panels.TryGetValue(typeof(T), out UIPanel keep);
            HideAll(instant, keep);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Instance = null;
            _missingStyleWarned = false;
        }
    }
}
