using UnityEngine;
using UnityEngine.InputSystem;

public class CustomerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Animation Settings")]
    [SerializeField] private string satisfiedTrigger = "IsSatisfied";

    private GameObject promptObject;
    private bool isPlayerInRange = false;
    private PlayerInventory playerInv;
    private bool isServed = false;

    void Start()
    {
        if (animator == null) animator = GetComponent<Animator>();
        // DO NOT search or turn off promptObject in Start()!
        // This prevents new spawns from interrupting active prompts.
    }

    void Update()
    {
        if (isServed || !isPlayerInRange) return;

        // ONLY allow serving when player is inside the trigger zone and presses E
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryReceiveFood();
        }
    }

    private void TryReceiveFood()
    {
        if (playerInv != null && playerInv.IsHoldingItem)
        {
            GameObject meal = playerInv.DeliverItem();
            Destroy(meal);

            isServed = true;

            if (promptObject != null) promptObject.SetActive(false);

            if (animator != null)
            {
                animator.SetTrigger(satisfiedTrigger);
            }

            Debug.Log("Customer served successfully!");
            Destroy(gameObject, 3.0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isServed) return;

        if (other.CompareTag("Player"))
        {
            playerInv = other.GetComponent<PlayerInventory>();
            isPlayerInRange = true;

            // Find global prompt ONLY when entering range
            if (promptObject == null)
            {
                GameObject canvasObj = GameObject.Find("UICanvas");
                if (canvasObj != null)
                {
                    Transform t = canvasObj.transform.Find("Serve Food Text");
                    if (t != null) promptObject = t.gameObject;
                }
            }

            // Display prompt if holding food
            if (promptObject != null && playerInv != null && playerInv.IsHoldingItem)
            {
                promptObject.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;
            playerInv = null;

            if (promptObject != null)
            {
                promptObject.SetActive(false);
            }
        }
    }
}