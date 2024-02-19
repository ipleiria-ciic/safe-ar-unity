using System.Collections;
using System.Collections.Generic;
using Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private Player currentPlayer;
    [SerializeField] private List<GameObject> itemPrefabs;

    private Dictionary<string, GameObject> itemPrefabDictionary;

    [SerializeField] private Text numWoodText;
    [SerializeField] private Text numClothText;
    [SerializeField] private Text numMetalText;
    [SerializeField] private Text numFoodText;

    public Player CurrentPlayer { get { return currentPlayer; } }

    private void Awake()
    {
        Assert.IsNotNull(currentPlayer, "Current player is null");

        itemPrefabDictionary = new Dictionary<string, GameObject>();
        foreach (var prefab in itemPrefabs)
        {
            Item item = prefab.GetComponent<Item>();
            if (item != null && !itemPrefabDictionary.ContainsKey(item.GetItemName))
            {
                itemPrefabDictionary.Add(item.GetItemName, prefab);
            }
        }
    }

    private void Start()
    {
        LoadData();
    }

    public void LoadData()
    {
        PlayerData playerData = DataManager.LoadData();
        if (playerData != null)
        {
            currentPlayer.AddXP(playerData.xp);
            foreach (ItemData itemData in playerData.items)
            {
               if (itemPrefabDictionary.TryGetValue(itemData.itemName, out GameObject itemPrefab))
                {
                    /* GameObject itemObject = Instantiate(itemPrefab);
                    Item itemComponent = itemObject.GetComponent<Item>();
                    itemComponent.SetItemName = itemData.itemName;
                    itemComponent.ItemQuantity = itemData.itemQuantity;
                    currentPlayer.AddItems(itemComponent); */
                    currentPlayer.UpdateInventory(itemData.itemName, itemData.itemQuantity);
                }
                else
                {
                    Debug.LogWarning("Prefab for item " + itemData.itemName + " not found.");
                } 
                
                UpdateItemTextUI(itemData.itemName, itemData.itemQuantity);
            }
        }
    }

    public void UpdateItemTextUI(string itemName, int quantity)
    {
        switch (itemName)
        {
            case "Wood":
                numWoodText.text = $"Wood ({quantity})";
                break;
            case "Cloth":
                numClothText.text = $"Cloth ({quantity})";
                break;
            case "Metal":
                numMetalText.text = $"Metal ({quantity})";
                break;
            case "Food":
                numFoodText.text = $"Food ({quantity})";
                break;
            // Add additional cases as needed
        }
    }
}
