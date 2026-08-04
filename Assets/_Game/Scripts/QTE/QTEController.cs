using System.Collections;
using System.Collections.Generic;
using MBG.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MBG.QTE
{
    /// <summary>
    /// Pintu masuk tunggal semua QTE. Menerima <see cref="QTERequest"/>, memilih
    /// module sesuai <see cref="QTEType"/>, membekukan pemain, dan mengembalikan
    /// keadaan seperti semula begitu sesi selesai.
    ///
    /// Alur satu sesi:
    /// bekukan pemain -> GameState jadi InQTE -> module di-tick tiap frame dengan
    /// GameClock.DeltaTime -> hasil keluar -> tahan sebentar (resultHoldSeconds)
    /// supaya teks hasil sempat terbaca -> state kembali, pemain bergerak lagi ->
    /// GameEventBus.OnQTEResult -> callback onComplete.
    ///
    /// Yang TIDAK dilakukan di sini: menyentuh Time.timeScale dan memanggil panel
    /// UI secara langsung. Panel muncul karena UIManager mendengar perubahan
    /// GameState.
    /// </summary>
    [DisallowMultipleComponent]
    public class QTEController : MonoBehaviour
    {
        [Header("Preset")]
        [Tooltip("Dipakai debug key F2. Diisi otomatis dengan QTE_Normal oleh " +
                 "Tools > MBG > Build QTE Setup.")]
        [SerializeField] QTEConfigSO defaultConfig;

        [Header("Debug")]
        [Tooltip("F2 memicu satu TimingBarQTE dengan preset di atas, dari mana saja.")]
        [SerializeField] bool enableDebugKey = true;

        public static QTEController Instance { get; private set; }

        readonly Dictionary<QTEType, IQTEModule> _modules = new();

        IQTEModule _active;
        QTERequest _request;
        GameState _stateBeforeQTE = GameState.Playing;
        PlayerController2D _player;
        Coroutine _finishRoutine;

        public bool IsRunning => _active != null && _active.IsRunning;

        /// <summary>Module yang sedang berjalan — dibaca QTEPanel untuk menggambar bar.</summary>
        public IQTEModule ActiveModule => _active;

        /// <summary>Instruksi sesi yang sedang berjalan.</summary>
        public string ActiveLabel { get; private set; } = "";

        public QTEConfigSO DefaultConfig => defaultConfig;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[QTEController] Sudah ada instance di '{Instance.name}'. " +
                                 $"Komponen di '{name}' diabaikan.", this);
                return;
            }

            Instance = this;

            RegisterModule(new TimingBarQTE());
            // TODO: RhythmQTE (santet) dan MashQTE menyusul — cukup daftarkan di sini.
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            // Jangan tinggalkan pemain beku kalau scene dibongkar di tengah sesi.
            if (IsRunning) RestorePlayerAndState();
            Instance = null;
        }

        void Update()
        {
            if (enableDebugKey && InputService.WasKeyPressedThisFrame(Key.F2))
                TriggerDebugQTE();

            if (IsRunning) _active.Tick(GameClock.DeltaTime);
        }

        void RegisterModule(IQTEModule module)
        {
            _modules[module.Type] = module;
            module.OnFinished += HandleFinished;
        }

        // ---- API ----------------------------------------------------------

        /// <summary>
        /// Mulai satu sesi QTE. Mengembalikan false kalau ditolak (sesi lain masih
        /// berjalan, module belum ada, atau config kosong).
        /// </summary>
        public bool Begin(QTERequest request)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[QTEController] Masih ada QTE berjalan — permintaan baru diabaikan.", this);
                return false;
            }

            if (request.config == null)
            {
                Debug.LogError("[QTEController] QTERequest tanpa QTEConfigSO — sesi ditolak.", this);
                return false;
            }

            if (!_modules.TryGetValue(request.type, out IQTEModule module))
            {
                Debug.LogError($"[QTEController] Belum ada module untuk {request.type}.", this);
                return false;
            }

            _request = request;
            _active = module;
            ActiveLabel = string.IsNullOrWhiteSpace(request.label) ? "Tekan SPASI!" : request.label;

            FreezePlayer(true);

            GameManager manager = GameManager.Instance;
            if (manager != null)
            {
                _stateBeforeQTE = manager.State;
                manager.ChangeState(GameState.InQTE);
            }

            AudioService.PlaySFX(SfxId.QteStart);
            module.StartQTE(request);
            return true;
        }

        /// <summary>Batalkan sesi berjalan. Pemanggil tetap menerima hasil CriticalMiss.</summary>
        public void CancelActive()
        {
            if (_active == null) return;

            _active.Cancel();
            HandleFinished(QTEResult.Failed());
        }

        // ---- Internal ------------------------------------------------------

        void HandleFinished(QTEResult result)
        {
            if (_active == null) return;

            _active = null;

            if (_finishRoutine != null) StopCoroutine(_finishRoutine);
            _finishRoutine = StartCoroutine(FinishRoutine(result));
        }

        IEnumerator FinishRoutine(QTEResult result)
        {
            // Tahan sebentar dengan waktu unscaled: pemain harus sempat membaca
            // hasilnya walau clock gameplay kebetulan sedang di-pause.
            float hold = _request.config != null ? _request.config.resultHoldSeconds : 0f;
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

            RestorePlayerAndState();

            GameEventBus.RaiseQTEResult(result.grade);

            var callback = _request.onComplete;
            _request = default;
            ActiveLabel = "";
            _finishRoutine = null;

            callback?.Invoke(result);
        }

        void RestorePlayerAndState()
        {
            FreezePlayer(false);

            GameManager manager = GameManager.Instance;
            if (manager != null && manager.State == GameState.InQTE)
                manager.ChangeState(_stateBeforeQTE);
        }

        void FreezePlayer(bool frozen)
        {
            if (_player == null)
                _player = FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include);

            if (_player != null) _player.SetFrozen(frozen);
        }

        void TriggerDebugQTE()
        {
            if (IsRunning) return;

            if (defaultConfig == null)
            {
                Debug.LogWarning("[QTEController] Debug F2: Default Config belum diisi. " +
                                 "Jalankan Tools > MBG > Build QTE Setup.", this);
                return;
            }

            Begin(QTERequest.TimingBar(defaultConfig, "Tes QTE (F2)",
                result => Debug.Log($"[QTEController] Debug QTE selesai: {result}", this)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;
    }
}
