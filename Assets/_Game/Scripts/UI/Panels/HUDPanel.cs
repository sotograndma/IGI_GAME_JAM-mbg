using MBG.Catering;
using MBG.Core;
using MBG.Kitchen;
using MBG.Obstacles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MBG.UI
{
    /// <summary>
    /// HUD gameplay: kartu pesanan di kiri atas, uang & skor di kanan atas, timer
    /// deadline di tengah atas, petunjuk langkah berikutnya di bawah tengah, dan
    /// slot indikator gangguan di kanan bawah.
    ///
    /// Seluruh datanya masuk lewat <see cref="GameEventBus"/>. HUD tidak pernah
    /// menyentuh CateringController — objek <see cref="OrderRuntime"/> yang dibaca
    /// tiap frame adalah yang dikirimkan event OnOrderStarted, bukan hasil mencari
    /// sistem lain.
    ///
    /// Langganan event dipasang di Awake (bukan OnEnable) supaya kejadian yang
    /// terjadi saat HUD sedang disembunyikan — misal pesanan gagal saat layar
    /// ringkasan hari terbuka — tidak terlewat.
    /// </summary>
    public class HUDPanel : UIPanel
    {
        [Header("Kartu pesanan (kiri atas)")]
        [SerializeField] CanvasGroup orderCard;
        [SerializeField] TMP_Text recipientLabel;
        [SerializeField] TMP_Text recipeLabel;
        [SerializeField] RectTransform progressFill;
        [SerializeField] TMP_Text progressLabel;
        [SerializeField] RectTransform qualityFill;
        [SerializeField] TMP_Text qualityLabel;

        [Header("Uang & skor (kanan atas)")]
        [SerializeField] TMP_Text goldLabel;
        [SerializeField] TMP_Text scoreLabel;

        [Header("Timer (tengah atas)")]
        [SerializeField] RectTransform timerRoot;
        [SerializeField] TMP_Text timerLabel;

        [Header("Langkah berikutnya (bawah tengah)")]
        [SerializeField] TMP_Text nextStepLabel;
        [SerializeField] GameObject arrowLeft;
        [SerializeField] GameObject arrowRight;

        [Tooltip("Selisih X (unit dunia) yang masih dianggap \"sudah sampai\", sehingga panah dimatikan.")]
        [SerializeField] float arrowDeadZone = 0.8f;

        [Header("Gangguan (kanan bawah)")]
        [SerializeField] RectTransform obstacleSlot;
        [SerializeField] CanvasGroup obstacleGroup;
        [SerializeField] TMP_Text obstacleIconLabel;
        [SerializeField] TMP_Text obstacleNameLabel;
        [SerializeField] RectTransform obstacleUrgencyFill;

        [Header("Teks melayang")]
        [SerializeField] RectTransform floatingTextRoot;
        [SerializeField] TMP_Text floatingText;

        [Header("Referensi dunia")]
        [Tooltip("Dipakai untuk menentukan arah panah. Diisi otomatis oleh Tools > MBG > Build HUD.")]
        [SerializeField] Transform playerTransform;

        /// <summary>
        /// Tempat indikator gangguan akan dipasang. Masih kosong — sistem obstacle
        /// nanti yang mengisinya, tanpa perlu mengubah tata letak HUD lagi.
        /// </summary>
        public RectTransform ObstacleSlot => obstacleSlot;

        OrderRuntime _order;

        int _portionsDone;
        int _portionsTotal;
        float _displayedProgress;
        float _displayedQuality = 1f;

        Vector2 _floatingHome;
        float _floatingTimer;

        int _gold;
        int _score;

        protected override void Awake()
        {
            base.Awake();

            if (floatingTextRoot != null) _floatingHome = floatingTextRoot.anchoredPosition;

            Subscribe();
            ApplyStyle();
            ShowOrderCard(false);
            HideFloatingText();
            RefreshCurrency();

            if (obstacleGroup != null) obstacleGroup.alpha = 0f;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        protected override void OnShow() => ApplyStyle();

        void Subscribe()
        {
            GameEventBus.OnOrderStarted += HandleOrderStarted;
            GameEventBus.OnOrderProgress += HandleOrderProgress;
            GameEventBus.OnOrderCompleted += HandleOrderCompleted;
            GameEventBus.OnOrderFailed += HandleOrderFailed;
            GameEventBus.OnGoldChanged += HandleGoldChanged;
            GameEventBus.OnScoreChanged += HandleScoreChanged;
        }

        void Unsubscribe()
        {
            GameEventBus.OnOrderStarted -= HandleOrderStarted;
            GameEventBus.OnOrderProgress -= HandleOrderProgress;
            GameEventBus.OnOrderCompleted -= HandleOrderCompleted;
            GameEventBus.OnOrderFailed -= HandleOrderFailed;
            GameEventBus.OnGoldChanged -= HandleGoldChanged;
            GameEventBus.OnScoreChanged -= HandleScoreChanged;
        }

        // ---- Event ---------------------------------------------------------

        void HandleOrderStarted(OrderRuntime order)
        {
            _order = order;

            _portionsDone = 0;
            _portionsTotal = order != null ? order.totalPortions : 0;
            _displayedProgress = 0f;
            _displayedQuality = 1f;

            if (order != null && order.source != null)
            {
                SetText(recipientLabel, order.source.RecipientName);
                SetText(recipeLabel, order.source.RecipeName);
            }

            ShowOrderCard(order != null);
            RefreshProgressLabel();
        }

        void HandleOrderProgress(int done, int total)
        {
            int delta = done - _portionsDone;

            _portionsDone = done;
            _portionsTotal = total;
            RefreshProgressLabel();

            if (delta > 0) ShowFloatingText($"+{delta} porsi");
        }

        void HandleOrderCompleted(OrderResult result) => ClearOrder();

        void HandleOrderFailed(OrderRuntime order) => ClearOrder();

        void HandleGoldChanged(int value)
        {
            _gold = value;
            RefreshCurrency();
        }

        void HandleScoreChanged(int value)
        {
            _score = value;
            RefreshCurrency();
        }

        void ClearOrder()
        {
            _order = null;
            ShowOrderCard(false);
            SetText(nextStepLabel, "");
            SetActive(arrowLeft, false);
            SetActive(arrowRight, false);
        }

        // ---- Frame ---------------------------------------------------------

        void Update()
        {
            float lerpSpeed = UIManager.Style != null ? UIManager.Style.hudBarLerpSpeed : 5f;
            float step = Time.unscaledDeltaTime * lerpSpeed;

            UpdateProgressBar(step);
            UpdateQualityBar(step);
            UpdateTimer();
            UpdateNextStepHint();
            UpdateObstacleIndicator();
            UpdateFloatingText();
        }

        /// <summary>
        /// Indikator gangguan aktif. Urgency dibaca dari ObstacleManager tiap frame —
        /// mengirimkannya lewat event bus setiap frame hanya akan jadi kebisingan.
        /// </summary>
        void UpdateObstacleIndicator()
        {
            if (obstacleGroup == null) return;

            IObstacle active = ObstacleManager.Instance != null ? ObstacleManager.Instance.Active : null;

            if (active == null)
            {
                obstacleGroup.alpha = 0f;
                return;
            }

            obstacleGroup.alpha = 1f;

            if (obstacleIconLabel != null) obstacleIconLabel.text = active.Type.GetIcon();
            if (obstacleNameLabel != null) obstacleNameLabel.text = active.Type.GetLabel();

            float urgency = active.UrgencyNormalized;
            SetFill(obstacleUrgencyFill, urgency);

            UIStyle style = UIManager.Style;
            if (style == null) return;

            // Bar penuh berarti konsekuensi buruk terjadi, jadi warnanya memanas
            // seiring urgency naik.
            Color color = Color.Lerp(style.goodColor, style.dangerColor, urgency);
            SetImageColor(obstacleUrgencyFill, color);

            if (obstacleIconLabel != null) obstacleIconLabel.color = color;
            if (obstacleNameLabel != null) obstacleNameLabel.color = style.textPrimary;
        }

        void UpdateProgressBar(float step)
        {
            float target = _portionsTotal > 0 ? Mathf.Clamp01(_portionsDone / (float)_portionsTotal) : 0f;

            // Lerp, bukan snap: bar menyusul angka sebenarnya supaya kenaikan porsi terbaca.
            _displayedProgress = Mathf.MoveTowards(_displayedProgress, target, step);
            SetFill(progressFill, _displayedProgress);
        }

        void UpdateQualityBar(float step)
        {
            float target = _order != null ? _order.AverageQuality : _displayedQuality;
            _displayedQuality = Mathf.MoveTowards(_displayedQuality, target, step);

            SetFill(qualityFill, _displayedQuality);

            UIStyle style = UIManager.Style;
            if (_order == null || style == null) return;

            FoodQuality tier = _order.QualityTier;
            Color color = style.GetQualityColor(tier);

            SetImageColor(qualityFill, color);
            if (qualityLabel != null)
            {
                qualityLabel.text = $"Kualitas: {tier.GetLabel()}";
                qualityLabel.color = color;
            }
        }

        void UpdateTimer()
        {
            if (timerLabel == null) return;

            if (_order == null)
            {
                timerLabel.text = "--:--";
                ResetTimerVisual();
                return;
            }

            timerLabel.text = FormatTime(_order.timeRemaining);

            UIStyle style = UIManager.Style;
            if (style == null) return;

            bool urgent = _order.TimeRemaining01 <= style.timerWarningThreshold;
            timerLabel.color = urgent ? style.dangerColor : style.textPrimary;

            if (timerRoot == null) return;

            if (!urgent)
            {
                timerRoot.localScale = Vector3.one;
                return;
            }

            // Denyut memakai waktu unscaled supaya tetap hidup walau clock gameplay
            // sedang di-pause.
            float pulse = 1f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * style.timerPulseSpeed)) * style.timerPulseAmount;
            timerRoot.localScale = new Vector3(pulse, pulse, 1f);
        }

        void ResetTimerVisual()
        {
            UIStyle style = UIManager.Style;
            if (style != null) timerLabel.color = style.textMuted;
            if (timerRoot != null) timerRoot.localScale = Vector3.one;
        }

        void UpdateNextStepHint()
        {
            if (nextStepLabel == null) return;

            if (_order == null)
            {
                SetText(nextStepLabel, "Belum ada pesanan.");
                SetActive(arrowLeft, false);
                SetActive(arrowRight, false);
                return;
            }

            StationType target;
            string action;

            if (_order.state == OrderState.ReadyToDeliver)
            {
                target = StationType.Handover;
                action = "Serahkan catering";
            }
            else
            {
                var step = _order.CurrentStep;
                if (step == null)
                {
                    SetText(nextStepLabel, "");
                    SetActive(arrowLeft, false);
                    SetActive(arrowRight, false);
                    return;
                }

                target = step.station;
                action = step.actionLabel;
            }

            SetText(nextStepLabel, $"Berikutnya: {action} di {target.GetLocationPhrase()}");
            UpdateArrows(target);
        }

        /// <summary>
        /// Panah menunjuk ke arah station tujuan relatif posisi pemain. Posisi
        /// station dibaca dari registry KitchenStation, bukan dari sistem catering.
        /// </summary>
        void UpdateArrows(StationType target)
        {
            if (arrowLeft == null && arrowRight == null) return;

            if (playerTransform == null || !KitchenStation.TryGetPosition(target, out Vector3 stationPos))
            {
                SetActive(arrowLeft, false);
                SetActive(arrowRight, false);
                return;
            }

            float dx = stationPos.x - playerTransform.position.x;

            SetActive(arrowLeft, dx < -arrowDeadZone);
            SetActive(arrowRight, dx > arrowDeadZone);
        }

        // ---- Teks melayang --------------------------------------------------

        void ShowFloatingText(string message)
        {
            if (floatingText == null || floatingTextRoot == null) return;

            UIStyle style = UIManager.Style;

            floatingText.text = message;
            floatingText.color = style != null ? style.successColor : Color.white;

            floatingTextRoot.gameObject.SetActive(true);
            floatingTextRoot.anchoredPosition = _floatingHome;

            _floatingTimer = style != null ? style.floatingTextDuration : 1.1f;
        }

        void UpdateFloatingText()
        {
            if (_floatingTimer <= 0f || floatingTextRoot == null || floatingText == null) return;

            UIStyle style = UIManager.Style;
            float duration = style != null ? style.floatingTextDuration : 1.1f;
            float rise = style != null ? style.floatingTextRise : 55f;

            _floatingTimer -= Time.unscaledDeltaTime;

            if (_floatingTimer <= 0f)
            {
                HideFloatingText();
                return;
            }

            float progress = 1f - Mathf.Clamp01(_floatingTimer / Mathf.Max(0.01f, duration));

            floatingTextRoot.anchoredPosition = _floatingHome + new Vector2(0f, rise * progress);

            Color c = floatingText.color;
            c.a = 1f - progress * progress;
            floatingText.color = c;
        }

        void HideFloatingText()
        {
            _floatingTimer = 0f;
            if (floatingTextRoot == null) return;

            floatingTextRoot.anchoredPosition = _floatingHome;
            floatingTextRoot.gameObject.SetActive(false);
        }

        // ---- Util -----------------------------------------------------------

        void RefreshProgressLabel()
            => SetText(progressLabel, $"{_portionsDone} / {_portionsTotal} porsi");

        void RefreshCurrency()
        {
            SetText(goldLabel, $"Rp {_gold:n0}");
            SetText(scoreLabel, $"Skor {_score:n0}");
        }

        void ShowOrderCard(bool visible)
        {
            if (orderCard == null) return;

            orderCard.alpha = visible ? 1f : 0f;
            orderCard.blocksRaycasts = false;
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        static void SetFill(RectTransform fill, float amount01)
        {
            if (fill == null) return;

            // Fill lewat anchor, bukan Image.fillAmount, supaya tidak butuh sprite.
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(amount01), 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        static void SetImageColor(RectTransform rect, Color color)
        {
            if (rect == null) return;

            var image = rect.GetComponent<Image>();
            if (image != null) image.color = color;
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        // ---- Tema -----------------------------------------------------------

        void ApplyStyle()
        {
            UIStyle style = UIManager.Style;
            if (style == null) return;

            style.ApplySmall(recipientLabel);
            if (recipeLabel != null)
            {
                recipeLabel.fontSize = style.fontSizeBody;
                recipeLabel.color = style.textPrimary;
            }

            style.ApplySmall(progressLabel);
            style.ApplySmall(qualityLabel);

            if (goldLabel != null)
            {
                goldLabel.fontSize = style.fontSizeBody;
                goldLabel.color = style.goldColor;
            }

            if (scoreLabel != null)
            {
                scoreLabel.fontSize = style.fontSizeBody;
                scoreLabel.color = style.textPrimary;
            }

            if (timerLabel != null)
            {
                timerLabel.fontSize = style.fontSizeHeading;
                timerLabel.color = style.textPrimary;
            }

            style.ApplyBody(nextStepLabel);

            if (floatingText != null)
            {
                floatingText.fontSize = style.fontSizeBody;
                floatingText.color = style.successColor;
            }

            SetImageColor(progressFill, style.successColor);
        }
    }
}
