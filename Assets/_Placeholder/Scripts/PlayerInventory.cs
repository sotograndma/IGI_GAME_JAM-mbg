using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hold Location")]
    [SerializeField] private Transform handPoint;

    private GameObject currentCarriedItem;

    public bool IsHoldingItem => currentCarriedItem != null;

    // Pick up spawned food when walking into it
    private void OnTriggerEnter2D(Collider2D other)
    {
        // If player isn't holding anything and touches a spawned meal
        if (!IsHoldingItem && other.CompareTag("Food"))
        {
            PickUpItem(other.gameObject);
        }
    }

    private void PickUpItem(GameObject item)
    {
        currentCarriedItem = item;

        // Snap food to player's hand and parent it so it moves with the player
        item.transform.SetParent(handPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        // Disable food collider while being carried so it doesn't re-trigger
        Collider2D col = item.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Debug.Log("Picked up " + item.name);
    }

    public GameObject DeliverItem()
    {
        if (!IsHoldingItem) return null;

        GameObject delivered = currentCarriedItem;
        currentCarriedItem = null;
        return delivered;
    }
}