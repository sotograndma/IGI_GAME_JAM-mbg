using System;
using MBG.Data;
using TMPro;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Sisi visual gangguan Ormas: memunculkan grup di depan rumah, memunculkan
    /// penerobos sebentar di dalam dapur, dan membersihkan semuanya setelah selesai.
    ///
    /// <see cref="OrmasObstacle"/> yang memutuskan kapan semua itu terjadi; class
    /// ini hanya mengeksekusi. Pemisahan ini membuat logic gangguan tetap berupa
    /// plain C# yang mudah di-tick, sementara urusan GameObject tinggal di sini.
    /// </summary>
    [DisallowMultipleComponent]
    public class OrmasEncounterController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] OrmasConfigSO config;

        [Header("Referensi (diisi Tools > MBG > Build Exterior Encounters)")]
        [Tooltip("Titik kumpul ormas di depan rumah.")]
        [SerializeField] Transform spawnPoint;

        [Tooltip("Tempat penerobos muncul di dalam dapur — biasanya dekat pintu.")]
        [SerializeField] Transform intruderPoint;

        [SerializeField] Sprite placeholderSprite;

        public static OrmasEncounterController Instance { get; private set; }

        public OrmasConfigSO Config => config != null ? config : OrmasConfigSO.Fallback;

        /// <summary>Pemain menekan F pada grup ormas.</summary>
        public event Action OnConfronted;

        GameObject _group;
        GameObject _intruder;
        float _intruderTimer;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Ormas] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            DespawnGroup();
            DespawnIntruder();
            Instance = null;
        }

        void Update()
        {
            if (_intruderTimer <= 0f) return;

            _intruderTimer -= Time.unscaledDeltaTime;
            if (_intruderTimer <= 0f) DespawnIntruder();
        }

        internal void Confront() => OnConfronted?.Invoke();

        // ---- Grup di luar rumah -------------------------------------------------

        public void SpawnGroup()
        {
            DespawnGroup();

            if (spawnPoint == null)
            {
                Debug.LogWarning("[Ormas] OrmasSpawnPoint belum diikat. " +
                                 "Jalankan Tools > MBG > Build Exterior Encounters.", this);
                return;
            }

            OrmasConfigSO cfg = Config;

            _group = new GameObject("Ormas_Group");
            _group.transform.SetParent(spawnPoint, false);
            _group.transform.localPosition = Vector3.zero;

            int count = cfg.GetMemberCount();
            float span = (count - 1) * cfg.memberSpacing;

            for (int i = 0; i < count; i++)
            {
                float x = -span * 0.5f + i * cfg.memberSpacing;
                CreateMember(_group.transform, new Vector3(x, cfg.memberHeight * 0.5f, 0f), cfg, i);
            }

            SpriteRenderer highlight = CreateBox(_group.transform, "Highlight",
                new Vector3(0f, cfg.memberHeight * 0.5f, 0f),
                new Vector2(span + cfg.memberWidth + 0.4f, cfg.memberHeight + 0.4f),
                new Color(1f, 1f, 1f, 0f), 0);
            highlight.enabled = false;

            var trigger = _group.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = cfg.encounterTriggerSize;
            trigger.offset = new Vector2(0f, cfg.encounterTriggerSize.y * 0.5f);

            var group = _group.AddComponent<OrmasGroup>();
            group.Configure(cfg.encounterPrompt);
            group.SetHighlight(highlight);

            Debug.Log($"[Ormas] {count} orang berkumpul di depan rumah.", this);
        }

        public void DespawnGroup()
        {
            if (_group == null) return;

            Destroy(_group);
            _group = null;
        }

        void CreateMember(Transform parent, Vector3 localPosition, OrmasConfigSO cfg, int index)
        {
            SpriteRenderer body = CreateBox(parent, $"Ormas_{index + 1}", localPosition,
                new Vector2(cfg.memberWidth, cfg.memberHeight), cfg.memberColor, 2);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(body.transform.parent, false);
            labelGo.transform.localPosition = localPosition + new Vector3(0f, cfg.memberHeight * 0.5f + 0.28f, 0f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 0.8f;
            label.text = "ORMAS";
            label.rectTransform.sizeDelta = new Vector2(3f, 0.6f);

            var renderer = labelGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 4;
        }

        // ---- Penerobos di dalam dapur ---------------------------------------------

        /// <summary>Ormas masuk sebentar, lalu pergi lagi.</summary>
        public void SpawnIntruder()
        {
            DespawnIntruder();

            if (intruderPoint == null) return;

            OrmasConfigSO cfg = Config;

            _intruder = new GameObject("Ormas_Intruder");
            _intruder.transform.SetParent(intruderPoint, false);
            _intruder.transform.localPosition = Vector3.zero;

            CreateBox(_intruder.transform, "Body", new Vector3(0f, cfg.memberHeight * 0.5f, 0f),
                new Vector2(cfg.memberWidth, cfg.memberHeight), cfg.memberColor, 6);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_intruder.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, cfg.memberHeight + 0.3f, 0f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 0.9f;
            label.text = "ORMAS MASUK!";
            label.color = new Color(0.95f, 0.3f, 0.25f);
            label.rectTransform.sizeDelta = new Vector2(5f, 0.8f);

            var renderer = labelGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 8;

            _intruderTimer = cfg.intruderVisitDuration;
        }

        void DespawnIntruder()
        {
            _intruderTimer = 0f;
            if (_intruder == null) return;

            Destroy(_intruder);
            _intruder = null;
        }

        SpriteRenderer CreateBox(Transform parent, string name, Vector3 localPosition,
                                 Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
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
