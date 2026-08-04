using TMPro;
using UnityEngine;

namespace MBG.UI
{
    /// <summary>
    /// Penunjuk objektif: teks tujuan plus panah kecil yang menempel di tepi layar
    /// mengarah ke target selama target berada di luar pandangan.
    ///
    /// Kalau targetnya sudah terlihat, panah pindah ke atas target alih-alih tetap
    /// menempel di tepi — supaya pemain tahu ia sudah sampai.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveMarker : MonoBehaviour
    {
        [Header("Referensi (diisi Tools > MBG > Build Santet Setup)")]
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform arrow;
        [SerializeField] TMP_Text label;

        [Header("Tata letak")]
        [Tooltip("Jarak panah dari tepi layar, dalam piksel canvas.")]
        [SerializeField] float edgeMargin = 90f;

        [Tooltip("Tinggi panah di atas target saat target sudah terlihat.")]
        [SerializeField] float aboveTargetOffset = 70f;

        Transform _target;

        void Awake() => Apply(false);

        public void Show(string text, Transform target)
        {
            _target = target;

            if (label != null) label.text = text;
            Apply(true);
        }

        public void Hide()
        {
            _target = null;
            Apply(false);
        }

        void Apply(bool visible)
        {
            if (group == null) return;

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        void LateUpdate()
        {
            if (_target == null || group == null || group.alpha <= 0.01f) return;
            if (arrow == null || canvasRect == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 viewport = cam.WorldToViewportPoint(_target.position);
            bool onScreen = viewport.z > 0f
                            && viewport.x > 0.02f && viewport.x < 0.98f
                            && viewport.y > 0.02f && viewport.y < 0.98f;

            // Target di belakang kamera membuat viewport terbalik; cerminkan supaya
            // panah tetap menunjuk ke sisi yang benar.
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            Vector2 size = canvasRect.rect.size;
            var local = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);

            if (onScreen)
            {
                arrow.anchoredPosition = local + new Vector2(0f, aboveTargetOffset);
                arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
                return;
            }

            Vector2 half = size * 0.5f - Vector2.one * edgeMargin;
            Vector2 clamped = local;

            // Proyeksikan ke tepi kotak sambil mempertahankan arahnya.
            float scale = Mathf.Max(Mathf.Abs(local.x) / Mathf.Max(1f, half.x),
                                    Mathf.Abs(local.y) / Mathf.Max(1f, half.y));
            if (scale > 1f) clamped = local / scale;

            arrow.anchoredPosition = clamped;

            float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            arrow.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }
}
