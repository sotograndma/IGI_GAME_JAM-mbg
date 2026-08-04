using MBG.Catering;
using MBG.Core;
using TMPro;
using UnityEngine;

namespace MBG.Kitchen
{
    /// <summary>
    /// Penerima catering yang berdiri di seberang counter Handover. Placeholder:
    /// kotak berwarna dengan label nama institusi di atasnya.
    ///
    /// Muncul saat pesanan dimulai dan hilang begitu pesanan diserahkan atau gagal.
    /// Namanya diambil dari OrderRuntime yang dibawa event — NPC ini tidak
    /// memegang referensi ke sistem catering.
    ///
    /// Langganan event dipasang di Awake, bukan OnEnable, karena NPC menyembunyikan
    /// dirinya dengan mematikan visual (bukan GameObject-nya) supaya tetap bisa
    /// mendengar pesanan berikutnya.
    /// </summary>
    [DisallowMultipleComponent]
    public class RecipientNPC : MonoBehaviour
    {
        [Header("Referensi")]
        [SerializeField] GameObject visualRoot;
        [SerializeField] TMP_Text nameLabel;

        public bool IsVisible => visualRoot != null && visualRoot.activeSelf;

        void Awake()
        {
            Subscribe();
            SetVisible(false);
        }

        void OnDestroy() => Unsubscribe();

        void Subscribe()
        {
            GameEventBus.OnOrderStarted += HandleOrderStarted;
            GameEventBus.OnOrderCompleted += HandleOrderCompleted;
            GameEventBus.OnOrderFailed += HandleOrderFailed;
        }

        void Unsubscribe()
        {
            GameEventBus.OnOrderStarted -= HandleOrderStarted;
            GameEventBus.OnOrderCompleted -= HandleOrderCompleted;
            GameEventBus.OnOrderFailed -= HandleOrderFailed;
        }

        void HandleOrderStarted(OrderRuntime order)
        {
            string institution = order != null && order.source != null
                ? order.source.RecipientName
                : "";

            if (nameLabel != null) nameLabel.text = institution;
            SetVisible(true);
        }

        void HandleOrderCompleted(OrderResult result) => SetVisible(false);

        void HandleOrderFailed(OrderRuntime order) => SetVisible(false);

        void SetVisible(bool visible)
        {
            if (visualRoot != null && visualRoot.activeSelf != visible)
                visualRoot.SetActive(visible);
        }
    }
}
