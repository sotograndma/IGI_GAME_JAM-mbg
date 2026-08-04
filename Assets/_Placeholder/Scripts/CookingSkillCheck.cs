using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CookingSkillCheck : MonoBehaviour
{
    [Header("UI Visual Components")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Image yellowZoneImage;
    [SerializeField] private Image redZoneImage;
    [SerializeField] private GameObject minigameCanvas;

    [Header("Skill Check Configuration")]
    [SerializeField] private float arrowSpeed = 200f;
    [SerializeField] private int requiredSuccesses = 3;

    [Range(0.05f, 0.3f)]
    [SerializeField] private float yellowZoneFill = 0.12f;

    [Header("Player & Dish Rewards")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Sprite crispSprite;
    [SerializeField] private GameObject cookedMealPrefab;
    [SerializeField] private Transform mealSpawnPoint;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private MonoBehaviour playerMovementScript; // Drag your Player Movement script here!
    [SerializeField] private Rigidbody2D playerRigidbody;        // Drag your Player's Rigidbody2D here!

    private int currentSuccessCount = 0;
    private bool isGameActive = false;
    private Sprite originalPlayerSprite;
    private bool isFailingSequenceRunning = false;

    private float yellowStartNormalized;
    private float yellowEndNormalized;

    void Awake()
    {
        if (minigameCanvas != null)
        {
            minigameCanvas.SetActive(false);
        }
        isGameActive = false;

        // Store player's original sprite at startup
        if (playerSpriteRenderer != null)
        {
            originalPlayerSprite = playerSpriteRenderer.sprite;
        }
    }

    public void StartMinigame()
    {
        // 🔒 BLOCKER 1: Cannot cook while carrying food
        if (playerInventory != null && playerInventory.IsHoldingItem)
        {
            Debug.Log("Hands full! Deliver your current dish before cooking again.");
            return;
        }

        // 🔒 BLOCKER 2: Prevent restarting while crisp penalty is active
        if (isFailingSequenceRunning) return;

        currentSuccessCount = 0;

        if (minigameCanvas != null)
        {
            minigameCanvas.SetActive(true);
        }

        if (arrow != null)
        {
            arrow.localRotation = Quaternion.identity;
        }

        isGameActive = true;
        RandomizeTargetZones();
    }

    void Update()
    {
        if (!isGameActive) return;

        if (arrow != null)
        {
            arrow.Rotate(0f, 0f, -arrowSpeed * Time.deltaTime);
        }

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CheckTiming();
        }
    }

    private void RandomizeTargetZones()
    {
        if (redZoneImage != null) redZoneImage.fillAmount = 1.0f;

        yellowStartNormalized = Random.Range(0.1f, 0.7f);
        yellowEndNormalized = yellowStartNormalized + yellowZoneFill;

        if (yellowZoneImage != null)
        {
            yellowZoneImage.fillAmount = yellowZoneFill;
            float rotationAngle = yellowStartNormalized * 360f;
            yellowZoneImage.rectTransform.localRotation = Quaternion.Euler(0, 0, -rotationAngle);
        }
    }

    private void CheckTiming()
    {
        float currentArrowAngle = (360f - arrow.localEulerAngles.z) % 360f;
        float currentArrowNormalized = currentArrowAngle / 360f;

        if (currentArrowNormalized >= yellowStartNormalized && currentArrowNormalized <= yellowEndNormalized)
        {
            OnSuccess();
        }
        else
        {
            OnFailure();
        }
    }

    private void OnSuccess()
    {
        currentSuccessCount++;
        Debug.Log($"Hit Yellow Zone! Progress: {currentSuccessCount}/{requiredSuccesses}");

        if (currentSuccessCount >= requiredSuccesses)
        {
            CompleteMinigame();
        }
        else
        {
            RandomizeTargetZones();
        }
    }

    private void OnFailure()
    {
        Debug.Log("Hit Red Zone! Turn player into crisp for 3 seconds...");
        EndMinigame();
        StartCoroutine(CrispFailureRoutine());
    }

    // ⏱️ 3-Second Crisp Pause Coroutine
    private IEnumerator CrispFailureRoutine()
    {
        isFailingSequenceRunning = true;

        // 1. Swap to crisp sprite
        if (playerSpriteRenderer != null && crispSprite != null)
        {
            playerSpriteRenderer.sprite = crispSprite;
        }

        // 2. Freeze the player
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false; // Stop input/movement
        }
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero; // Stop any ongoing sliding
        }

        // 3. Wait for 3 seconds
        yield return new WaitForSeconds(3.0f);

        // 4. Revert to original sprite
        if (playerSpriteRenderer != null && originalPlayerSprite != null)
        {
            playerSpriteRenderer.sprite = originalPlayerSprite;
        }

        // 5. Unfreeze the player
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = true; // Re-enable movement
        }

        isFailingSequenceRunning = false;
        Debug.Log("Player recovered from crisp and unfroze!");
    }

    private void CompleteMinigame()
    {
        Debug.Log("Cooking Complete! Spawning meal...");
        EndMinigame();

        if (cookedMealPrefab != null && mealSpawnPoint != null)
        {
            GameObject spawnedMeal = Instantiate(cookedMealPrefab, mealSpawnPoint.position, Quaternion.identity);

            // Retain prefab scale automatically
            spawnedMeal.transform.localScale = cookedMealPrefab.transform.localScale;
            spawnedMeal.SetActive(true);
        }
        else
        {
            Debug.LogError("Missing Cooked Meal Prefab OR Meal Spawn Point in Inspector!");
        }
    }

    private void EndMinigame()
    {
        isGameActive = false;

        if (minigameCanvas != null)
        {
            minigameCanvas.SetActive(false);
        }
    }
}