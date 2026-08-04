using MBG.Data;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// Efek layar saat santet menyerang: lapisan merah, vignette gelap di tepi, dan
    /// lapisan kelabu yang meniru saturasi turun.
    ///
    /// Sengaja memakai tiga UI Image fullscreen, BUKAN post-processing volume —
    /// desaturasi sungguhan butuh shader atau URP Volume, dan itu menambah
    /// ketergantungan yang tidak sepadan untuk satu efek. Konsekuensinya: warna
    /// tidak benar-benar dibuat abu-abu, hanya ditumpuk lapisan kelabu.
    /// </summary>
    [DisallowMultipleComponent]
    public class SantetOverlay : MonoBehaviour
    {
        [Header("Referensi (diisi Tools > MBG > Build Santet Setup)")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Image tintImage;
        [SerializeField] Image vignetteImage;
        [SerializeField] Image desaturateImage;

        public bool IsVisible => _targetAlpha > 0.01f;

        SantetConfigSO _config;
        float _targetAlpha;
        float _currentAlpha;
        float _pulseTime;

        void Awake() => ApplyAlpha(0f);

        /// <summary>Nyalakan overlay memakai warna dari config.</summary>
        public void Show(SantetConfigSO config)
        {
            _config = config != null ? config : SantetConfigSO.Fallback;

            if (tintImage != null) tintImage.color = _config.overlayColor;
            if (vignetteImage != null) vignetteImage.color = _config.vignetteColor;
            if (desaturateImage != null) desaturateImage.color = _config.desaturateColor;

            _pulseTime = 0f;
            _targetAlpha = 1f;
        }

        public void Hide() => _targetAlpha = 0f;

        void Update()
        {
            SantetConfigSO cfg = _config != null ? _config : SantetConfigSO.Fallback;

            float fadeSpeed = cfg.overlayFadeDuration > 0f ? 1f / cfg.overlayFadeDuration : 10f;

            // Waktu unscaled: efek ini harus tetap hidup walau clock gameplay melambat
            // atau berhenti karena gangguan itu sendiri.
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, fadeSpeed * Time.unscaledDeltaTime);

            if (_currentAlpha <= 0.001f)
            {
                ApplyAlpha(0f);
                return;
            }

            _pulseTime += Time.unscaledDeltaTime * cfg.overlayPulseSpeed;
            float pulse = 1f - cfg.overlayPulseAmount * 0.5f * (1f + Mathf.Sin(_pulseTime));

            ApplyAlpha(_currentAlpha * pulse);
        }

        void ApplyAlpha(float alpha)
        {
            if (group == null) return;

            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Hanya boleh mematikan GameObject lapisan, bukan GameObject komponen ini
            // sendiri — kalau tidak, Update berhenti dan overlay tak bisa dinyalakan lagi.
            if (group.gameObject == gameObject) return;

            bool visible = alpha > 0.001f;
            if (group.gameObject.activeSelf != visible) group.gameObject.SetActive(visible);
        }
    }
}
