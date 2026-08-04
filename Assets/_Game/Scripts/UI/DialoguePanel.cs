using System;
using System.Collections.Generic;
using MBG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Dialog sederhana: potret, nama pembicara, dan baris teks yang dilewati satu
    /// per satu dengan Spasi.
    ///
    /// Sengaja BUKAN dialogue tree — tidak ada percabangan, hanya deretan baris dan
    /// paling banyak satu pilihan di akhir. Panel ini yang membaca input Spasi dan F
    /// karena keduanya memang bagian dari dialognya sendiri.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialoguePanel : MonoBehaviour
    {
        [Header("Referensi (diisi Tools > MBG > Build Tax Setup)")]
        [Tooltip("CanvasGroup di CHILD, bukan di GameObject ini — kalau tidak, Update " +
                 "berhenti saat panel disembunyikan.")]
        [SerializeField] CanvasGroup group;

        [SerializeField] Image portrait;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] TMP_Text hintLabel;

        public static DialoguePanel Instance { get; private set; }

        public bool IsShowing { get; private set; }

        /// <summary>
        /// Potret pembicara. Masih kotak berwarna — begitu ada sprite karakter,
        /// tinggal isi <c>sprite</c>-nya tanpa mengubah alur dialog.
        /// </summary>
        public Image Portrait => portrait;

        IReadOnlyList<string> _lines;
        string _choicePrompt;
        Action _onFinished;
        int _index;
        bool _atChoice;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Dialog] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;
            Apply(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Tampilkan deretan baris. Kalau <paramref name="choicePrompt"/> diisi,
        /// baris terakhir menunggu tombol F; kalau kosong, Spasi menutup panel.
        /// </summary>
        public void Play(string speaker, IReadOnlyList<string> lines, string choicePrompt, Action onFinished)
        {
            if (lines == null || lines.Count == 0)
            {
                onFinished?.Invoke();
                return;
            }

            _lines = lines;
            _choicePrompt = choicePrompt;
            _onFinished = onFinished;
            _index = 0;
            _atChoice = false;

            if (nameLabel != null) nameLabel.text = speaker;

            Apply(true);
            ShowCurrentLine();
        }

        /// <summary>Satu baris penutup; Spasi menutupnya.</summary>
        public void PlaySingle(string speaker, string line, Action onFinished)
            => Play(speaker, new[] { line }, null, onFinished);

        public void Hide()
        {
            _lines = null;
            _onFinished = null;
            _atChoice = false;
            Apply(false);
        }

        void Update()
        {
            if (!IsShowing || _lines == null) return;

            if (_atChoice)
            {
                if (InputService.InteractPressed) Finish();
                return;
            }

            if (!InputService.ConfirmPressed) return;

            _index++;

            if (_index < _lines.Count)
            {
                ShowCurrentLine();
                return;
            }

            // Sudah lewat baris terakhir: tawarkan pilihan, atau tutup saja.
            if (string.IsNullOrWhiteSpace(_choicePrompt))
            {
                Finish();
                return;
            }

            _atChoice = true;
            _index = _lines.Count - 1;
            ShowCurrentLine();
        }

        void ShowCurrentLine()
        {
            if (bodyLabel != null) bodyLabel.text = _lines[Mathf.Clamp(_index, 0, _lines.Count - 1)];

            if (hintLabel == null) return;

            hintLabel.text = _atChoice
                ? $"[F] {_choicePrompt}"
                : "[Spasi] lanjut";
        }

        void Finish()
        {
            Action callback = _onFinished;

            Hide();
            AudioService.PlaySFX(SfxId.UiClick);

            callback?.Invoke();
        }

        void Apply(bool visible)
        {
            IsShowing = visible;
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Hanya boleh mematikan GameObject grup, bukan GameObject komponen ini.
            if (group.gameObject == gameObject) return;
            if (group.gameObject.activeSelf != visible) group.gameObject.SetActive(visible);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
