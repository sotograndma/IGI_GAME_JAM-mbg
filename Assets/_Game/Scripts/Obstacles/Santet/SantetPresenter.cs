using System;
using MBG.Data;
using MBG.UI;
using TMPro;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Sisi visual gangguan Santet: overlay layar, penunjuk objektif, dan dukun di
    /// dunia luar.
    ///
    /// Sama seperti OrmasEncounterController, <see cref="SantetObstacle"/> yang
    /// memutuskan kapan semuanya terjadi; class ini hanya mengeksekusi, sehingga
    /// logic gangguan tetap plain C# yang mudah di-tick.
    /// </summary>
    [DisallowMultipleComponent]
    public class SantetPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] SantetConfigSO config;

        [Header("Referensi (diisi Tools > MBG > Build Santet Setup)")]
        [SerializeField] SantetOverlay overlay;
        [SerializeField] ObjectiveMarker objectiveMarker;

        [Tooltip("Titik munculnya dukun di ExteriorRoot/EncounterPoints.")]
        [SerializeField] Transform casterPoint;

        [SerializeField] Sprite placeholderSprite;

        public static SantetPresenter Instance { get; private set; }

        public SantetConfigSO Config => config != null ? config : SantetConfigSO.Fallback;

        /// <summary>Pemain menekan F pada dukun.</summary>
        public event Action OnConfronted;

        /// <summary>Transform dukun yang sedang ada, untuk dituju penunjuk objektif.</summary>
        public Transform DukunTransform => _dukun != null ? _dukun.transform : null;

        GameObject _dukun;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Santet] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            DespawnDukun();
            HideOverlay();
            HideObjective();
            Instance = null;
        }

        internal void Confront() => OnConfronted?.Invoke();

        // ---- Overlay ------------------------------------------------------------

        public void ShowOverlay()
        {
            if (overlay != null) overlay.Show(Config);
            else Debug.LogWarning("[Santet] SantetOverlay belum diikat.", this);
        }

        public void HideOverlay()
        {
            if (overlay != null) overlay.Hide();
        }

        // ---- Objektif ------------------------------------------------------------

        public void ShowObjective()
        {
            if (objectiveMarker == null) return;

            objectiveMarker.Show(Config.objectiveText, DukunTransform);
        }

        public void HideObjective()
        {
            if (objectiveMarker != null) objectiveMarker.Hide();
        }

        // ---- Dukun ----------------------------------------------------------------

        public void SpawnDukun()
        {
            DespawnDukun();

            if (casterPoint == null)
            {
                Debug.LogWarning("[Santet] SantetCasterPoint belum diikat. " +
                                 "Jalankan Tools > MBG > Build Exterior Encounters.", this);
                return;
            }

            SantetConfigSO cfg = Config;

            _dukun = new GameObject("Dukun_Santet");
            _dukun.transform.SetParent(casterPoint, false);
            _dukun.transform.localPosition = Vector3.zero;

            CreateBox("Body", new Vector3(0f, cfg.dukunHeight * 0.5f, 0f),
                      new Vector2(cfg.dukunWidth, cfg.dukunHeight), cfg.dukunColor, 3);

            SpriteRenderer highlight = CreateBox("Highlight", new Vector3(0f, cfg.dukunHeight * 0.5f, 0f),
                new Vector2(cfg.dukunWidth + 0.3f, cfg.dukunHeight + 0.3f),
                new Color(1f, 1f, 1f, 0f), 2);
            highlight.enabled = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_dukun.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, cfg.dukunHeight + 0.3f, 0f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 0.85f;
            label.text = "DUKUN";
            label.rectTransform.sizeDelta = new Vector2(4f, 0.7f);

            var renderer = labelGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 5;

            var trigger = _dukun.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = cfg.dukunTriggerSize;
            trigger.offset = new Vector2(0f, cfg.dukunTriggerSize.y * 0.5f);

            var npc = _dukun.AddComponent<DukunNPC>();
            npc.Configure(cfg.dukunPrompt, highlight);

            Debug.Log("[Santet] Dukun muncul di luar rumah.", this);
        }

        public void DespawnDukun()
        {
            if (_dukun == null) return;

            Destroy(_dukun);
            _dukun = null;
        }

        SpriteRenderer CreateBox(string name, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_dukun.transform, false);
            go.transform.localPosition = localPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = placeholderSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            if (placeholderSprite != null)
            {
                Vector3 spriteSize = placeholderSprite.bounds.size;
                if (spriteSize.x > 0f && spriteSize.y > 0f)
                    go.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            }

            return renderer;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
