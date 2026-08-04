using System;
using System.Collections.Generic;
using MBG.Data;
using MBG.UI;
using TMPro;
using UnityEngine;

namespace MBG.Obstacles
{
    /// <summary>
    /// Sisi visual gangguan Pajak Ilegal: petugas di dunia luar dan panel dialognya.
    /// <see cref="IllegalTaxObstacle"/> yang memutuskan kapan semuanya muncul.
    /// </summary>
    [DisallowMultipleComponent]
    public class TaxPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] IllegalTaxConfigSO config;

        [Header("Referensi (diisi Tools > MBG > Build Tax Setup)")]
        [Tooltip("Titik munculnya petugas di ExteriorRoot/EncounterPoints.")]
        [SerializeField] Transform collectorPoint;

        [SerializeField] DialoguePanel dialoguePanel;
        [SerializeField] Sprite placeholderSprite;

        public static TaxPresenter Instance { get; private set; }

        public IllegalTaxConfigSO Config => config != null ? config : IllegalTaxConfigSO.Fallback;

        /// <summary>Pemain menekan F pada petugas.</summary>
        public event Action OnMet;

        GameObject _collector;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Pungli] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            DespawnCollector();
            HideDialogue();
            Instance = null;
        }

        internal void Meet() => OnMet?.Invoke();

        // ---- Dialog ---------------------------------------------------------------

        public void PlayDemands(Action onChallengeAccepted)
        {
            TaxDialogueSO dialogue = Config.dialogue;
            if (dialoguePanel == null || dialogue == null || !dialogue.HasContent)
            {
                // Tanpa naskah, langsung ke tantangan supaya alurnya tidak buntu.
                onChallengeAccepted?.Invoke();
                return;
            }

            dialoguePanel.Play(dialogue.speakerName, dialogue.demandLines,
                               dialogue.challengePrompt, onChallengeAccepted);
        }

        public void PlayClosing(string line, Action onFinished)
        {
            TaxDialogueSO dialogue = Config.dialogue;
            string speaker = dialogue != null ? dialogue.speakerName : "Petugas (?)";

            if (dialoguePanel == null || string.IsNullOrWhiteSpace(line))
            {
                onFinished?.Invoke();
                return;
            }

            dialoguePanel.PlaySingle(speaker, line, onFinished);
        }

        public void HideDialogue()
        {
            if (dialoguePanel != null && dialoguePanel.IsShowing) dialoguePanel.Hide();
        }

        // ---- Petugas ----------------------------------------------------------------

        public void SpawnCollector()
        {
            DespawnCollector();

            if (collectorPoint == null)
            {
                Debug.LogWarning("[Pungli] TaxCollectorPoint belum diikat. " +
                                 "Jalankan Tools > MBG > Build Exterior Encounters.", this);
                return;
            }

            IllegalTaxConfigSO cfg = Config;

            _collector = new GameObject("Tax_Collector");
            _collector.transform.SetParent(collectorPoint, false);
            _collector.transform.localPosition = Vector3.zero;

            CreateBox("Body", new Vector3(0f, cfg.npcHeight * 0.5f, 0f),
                      new Vector2(cfg.npcWidth, cfg.npcHeight), cfg.npcColor, 3);

            SpriteRenderer highlight = CreateBox("Highlight", new Vector3(0f, cfg.npcHeight * 0.5f, 0f),
                new Vector2(cfg.npcWidth + 0.3f, cfg.npcHeight + 0.3f),
                new Color(1f, 1f, 1f, 0f), 2);
            highlight.enabled = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_collector.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, cfg.npcHeight + 0.3f, 0f);

            var label = labelGo.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 0.8f;
            label.text = "PETUGAS (?)";
            label.rectTransform.sizeDelta = new Vector2(5f, 0.7f);

            var renderer = labelGo.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 5;

            var trigger = _collector.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = cfg.npcTriggerSize;
            trigger.offset = new Vector2(0f, cfg.npcTriggerSize.y * 0.5f);

            var npc = _collector.AddComponent<TaxCollectorNPC>();
            npc.Configure(cfg.npcPrompt, highlight);

            Debug.Log("[Pungli] Petugas menunggu di luar rumah.", this);
        }

        public void DespawnCollector()
        {
            if (_collector == null) return;

            Destroy(_collector);
            _collector = null;
        }

        SpriteRenderer CreateBox(string name, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_collector.transform, false);
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
