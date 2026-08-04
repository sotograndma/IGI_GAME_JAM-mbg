using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    [Header("Customer Prefabs")]
    [SerializeField] private GameObject[] customerPrefabs; // Your 4 child prefabs

    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;      // Array of all 12 spawn points

    [Header("Spawn Settings")]
    [SerializeField] private float minSpawnInterval = 3.0f; // Minimum time between new customer arrivals
    [SerializeField] private float maxSpawnInterval = 7.0f; // Maximum time between new customer arrivals
    [SerializeField] private int maxSimultaneousCustomers = 12;

    // Map each spawn point index to the active customer GameObject standing there
    private Dictionary<int, GameObject> activeCustomers = new Dictionary<int, GameObject>();

    void Start()
    {
        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);

            // Clean up missing/destroyed customer entries from tracking
            List<int> keysToRemove = new List<int>();
            foreach (var kvp in activeCustomers)
            {
                if (kvp.Value == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (int key in keysToRemove)
            {
                activeCustomers.Remove(key);
            }

            // If we have open tables, spawn a customer at an available spot
            if (activeCustomers.Count < maxSimultaneousCustomers)
            {
                TrySpawnAtRandomEmptyPoint();
            }
        }
    }

    private void TrySpawnAtRandomEmptyPoint()
    {
        if (customerPrefabs.Length == 0 || spawnPoints.Length == 0) return;

        // Find all indices of spawn points that currently do NOT have a customer
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null && !activeCustomers.ContainsKey(i))
            {
                availableIndices.Add(i);
            }
        }

        if (availableIndices.Count == 0) return; // All 12 tables are full!

        // Pick a random available spawn point
        int chosenPointIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        Transform spawnPoint = spawnPoints[chosenPointIndex];

        // Pick a random customer prefab out of the 4 children
        int randomCustomerIndex = Random.Range(0, customerPrefabs.Length);

        // Instantiate customer at the selected position
        GameObject newCustomer = Instantiate(customerPrefabs[randomCustomerIndex], spawnPoint.position, Quaternion.identity);

        // Track that this spawn point is now occupied
        activeCustomers[chosenPointIndex] = newCustomer;
    }
}