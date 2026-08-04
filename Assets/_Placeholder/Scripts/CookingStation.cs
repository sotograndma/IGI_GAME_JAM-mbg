using UnityEngine;

public class CookingStation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CookingSkillCheck skillCheckManager;
    [SerializeField] private GameObject interactionPromptUI;

    [Header("Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool isPlayerInRange = false;

    void Start()
    {
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);
    }

    void Update()
    {
        if (isPlayerInRange && Input.GetKeyDown(interactKey))
        {
            Debug.Log("1. Key pressed while standing at station!");
            StartCooking();
        }
    }

    private void StartCooking()
    {
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(false);

        if (skillCheckManager != null)
        {
            Debug.Log("2. Calling StartMinigame on CookingSkillCheck...");
            skillCheckManager.StartMinigame();
        }
        else
        {
            Debug.LogError("ERROR: skillCheckManager is NOT assigned in the Inspector!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Something entered trigger: " + other.gameObject.name);

        if (other.CompareTag("Player"))
        {
            Debug.Log("Player tag matched!");
            isPlayerInRange = true;

            if (interactionPromptUI != null)
            if (interactionPromptUI != null)
                interactionPromptUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player left trigger.");
            isPlayerInRange = false;

            if (interactionPromptUI != null)
                interactionPromptUI.SetActive(false);
        }
    }
}