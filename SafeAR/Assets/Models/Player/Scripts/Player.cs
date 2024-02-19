using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private string username;
    [SerializeField] private string password;
    [SerializeField] private int xp;
    [SerializeField] private int requiredXP = 100;
    [SerializeField] private int levelBase = 100;
    [SerializeField] private List<Item> items = new List<Item>();

    //public HashSet<string> loadedItems = new HashSet<string>();
    private int level = 1;
    private string playerDataPath;

    public string GetUsername
    { get { return username; } }
    public string GetPassword
    { get { return password; } }
    public int GetXP
    { get { return xp; } }
    public int GetRequiredXP
    { get { return requiredXP; } }
    public int GetLevelBase
    { get { return levelBase; } }
    public List<Item> GetItems
    { get { return items; } }
    public int GetLevel
    { get { return level; } }

    // Start is called before the first frame update
    void Start()
    {
        //playerDataPath = Application.persistentDataPath + "/player.dat";
        //Load();
    }

    public void AddXP(int xp)
    {

        this.xp += Mathf.Max(0, xp);
        //this.xp += xp;
        //if (this.xp >= requiredXP)
        //{
        //    level++;
        //    this.xp -= requiredXP;
        //    requiredXP += levelBase;
        //}
        //Save();
    }

    public void AddItems(Item item)
    {
        bool itemExists = false;

        foreach (Item i in items)
        {
            if (i.GetItemName.Equals(item.GetItemName))
            {
                i.ItemQuantity += item.ItemQuantity;
                itemExists = true;
                Debug.Log($"Increased quantity for {item.GetItemName}. New quantity: {item.ItemQuantity}");
                break;
            }
        }

        if (!itemExists)
        {
            items.Add(item);
            Debug.Log($"Added {item.GetItemName} with quantity {item.ItemQuantity}");
        }

        //loadedItems.Add(item.GetItemName);
        
        /* foreach (Item i in items)
        {
            Debug.Log($"Item: {i.GetItemName}, Quantity: {i.ItemQuantity}");
        } */
    }

    public void UpdateInventory(string itemName, int quantity)
    {
        var existingItem = items.Find(x => x.GetItemName == itemName);
        if (existingItem != null)
        {
            existingItem.ItemQuantity += quantity;
        }
        else
        {
            Item item = new Item { itemName = itemName, itemQuantity = quantity };
            items.Add(item);
        }
    }

    public void InitLevelData()
    {
        level = (xp / levelBase) + 1;
        requiredXP = levelBase * level;
    }

    /*private void Save()
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(playerDataPath);
        PlayerData playerData = new PlayerData(this);
        bf.Serialize(file, playerData);
        file.Close();
    }

    private void Load()
    {
        if (File.Exists(playerDataPath))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file = File.Open(playerDataPath, FileMode.Open);
            PlayerData playerData = (PlayerData)bf.Deserialize(file);
            file.Close();

            this.xp = playerData.GetXp;
            this.levelBase = playerData.GetLevelBase;
            this.level = playerData.GetLevel;
            this.requiredXP = playerData.GetRequiredXp;
            //get items from playerData to the items list
            foreach (ItemData itemData in playerData.GetItems)
            {
                GameObject itemObject = Resources.Load<GameObject>("Prefabs/Items/" + itemData.GetItemName);
                if (itemObject != null)
                {
                    Item item = itemObject.GetComponent<Item>();
                    if (item != null)
                    {
                        items.Add(itemObject);
                    }
                }
            }
        } else
        {
            InitLevelData();
        }
    }*/
}
