using UnityEngine;

public class ItemShuffler : MonoBehaviour
{
    // List of items to shuffle
    [SerializeField]
    private GameObject[] items;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (items.Length == 0)
        {
            Debug.Log("No items assigned, using children of the GameObject.");
            items = GetComponentsInChildren<GameObject>();
        }
        ShuffleItems();
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // Method to shuffle the items positions in the array
    private void ShuffleItems()
    {
        for (int i = 0; i < items.Length; i++)
        {
            int randomIndex = Random.Range(0, items.Length);
            GameObject temp = items[i];
            items[i].transform.position = items[randomIndex].transform.position;
            items[randomIndex].transform.position = temp.transform.position;
        }
    }
}
