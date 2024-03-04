using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;


public class CatchManager : MonoBehaviour
{
    public Text usernameText;
    public Text xpText;
    public Text levelText;
    [SerializeField] private AudioClip itemSound;
    [SerializeField] private GameObject inventoryButton;
    [SerializeField] private GameObject catchItemScreen;
    [SerializeField] private GameObject cannotCatchScreen;
    [SerializeField] private GameObject inventoryScreen;
    [SerializeField] private GameObject inventoryBackBtn;
    [SerializeField] private GameObject catchButton;
    [SerializeField] private GameObject dismissButton;
    //-----------------Inventory----------------------//
    //------------------------------------------------//
    [SerializeField] private Text numWoodText;
    [SerializeField] private Text numClothText;
    [SerializeField] private Text numMetalText;
    [SerializeField] private Text numFoodText;
    //-----------------Catch Screen-------------------//
    [SerializeField] private Text catchItemText;
    [SerializeField] private Text cannotCatchItemText;
    private int randomAmount;
    //------------------------------------------------//
    private Item selectedItem; 

    private AudioSource audioSource;
    private int numWoodCaught;
    private int numClothCaught;
    private int numMetalCaught;
    private int numFoodCaught;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    void Update()
    {
        updateLevel();
        updateXP();
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void CannotCatchScreen(Item Item)
    {
        selectedItem = Item;
        audioSource.PlayOneShot(itemSound);
        cannotCatchScreen.SetActive(true);
        cannotCatchItemText.text = "You cannot catch " + selectedItem.GetItemName + " unless in AR mode!";
        
    }

    public void CannotCatchScreenItemToFarAway(Item item)
    {
        selectedItem = item;
        audioSource.PlayOneShot(itemSound);
        cannotCatchScreen.SetActive(true);
        cannotCatchItemText.text = "You are too far away to catch " + selectedItem.GetItemName + "! Get closer to the item!";
    }
    
    public void DismissCannotCatchScreen()
    {
        if (cannotCatchScreen.activeSelf)
        {
            audioSource.PlayOneShot(itemSound);
            cannotCatchScreen.SetActive(false);
        }
    }

    public void CatchItemScreen(Item Item)
    {
        selectedItem = Item;
        audioSource.PlayOneShot(itemSound);
        randomAmount = GetRamdomAmount();
        catchItemScreen.SetActive(true);
        catchItemText.text = "Do you want to catch " + randomAmount + " of " + selectedItem.GetItemName + "?";
    }

    public void CatchItem()
    {
        if (catchItemScreen.activeSelf)
        {

            //get the item name from the item that was clicked
            string itemName = selectedItem.GetItemName;
            Debug.Log("Item caught: " + itemName);

            audioSource.PlayOneShot(itemSound);

            //add the item to the inventory
            Item caughtItem = new Item { itemName = itemName, itemQuantity = randomAmount };
            GameManager.Instance.CurrentPlayer.AddItems(caughtItem);

            //update the inventory quantoity
            int quantity = GameManager.Instance.CurrentPlayer.GetItems.Find(x => x.GetItemName == itemName).ItemQuantity;
            

            switch (itemName)
            {
                case "Wood":
                    numWoodText.text = itemName + " (" + quantity.ToString() + ")";
                    break;
                case "Cloth":
                    numClothText.text = itemName + " (" + quantity.ToString() + ")";
                    break;
                case "Metal":
                    numMetalText.text = itemName + " (" + quantity.ToString() + ")";
                    break;
                case "Food":
                    numFoodText.text = itemName + " (" + quantity.ToString() + ")";
                    break;
            }

            int xpEarned = CalculateXPEarned(randomAmount);
            GameManager.Instance.CurrentPlayer.AddXP(xpEarned);   

            //save the data
            DataManager.SaveData(GameManager.Instance.CurrentPlayer);

            Spawner.Instance.DestroyItem(selectedItem);
            catchItemScreen.SetActive(false);
            
        }
    }

    public void DismissItem()
    {
        if (catchItemScreen.activeSelf) 
        {
            audioSource.PlayOneShot(itemSound);
            catchItemScreen.SetActive(false);
        }
    }

    public void toggleInventory()
    {
        audioSource.PlayOneShot(itemSound);
        inventoryScreen.SetActive(!inventoryScreen.activeSelf);
    }

    public void toggleInventoryBackBtn()
    {
        audioSource.PlayOneShot(itemSound);
        if (inventoryScreen.activeSelf)
        {
            inventoryScreen.SetActive(false);
        }
    }

    public void updateXP()
    {
        xpText.text = GameManager.Instance.CurrentPlayer.GetXP.ToString();
    }

    public void updateLevel()
    {
        levelText.text = GameManager.Instance.CurrentPlayer.GetLevel.ToString();
    }

    public void updateUsername()
    {
        usernameText.text = GameManager.Instance.CurrentPlayer.GetUsername;
    }
    
    private int GetRamdomAmount()
    {
        int amount = 1;
        amount = GetRandomAmountProbabilities(new float[] { 0.7f, 0.4f, 0.2f, 0.1f, 0.05f }, new int[] { 1, 2, 3, 4, 5 });

        return amount;
    }

    private int GetRandomAmountProbabilities(float[] probabilities, int[] amounts)
    {
        if (probabilities.Length != amounts.Length)
        {
            return 1;
        }

        float randomValue = UnityEngine.Random.value;
        float comulativeProbability = 0f;

        for (int i = 0; i < probabilities.Length; i++)
        {
            comulativeProbability += probabilities[i];
            if (randomValue < comulativeProbability)
            {
                return amounts[i];
            }
        }

        return 1;
    }

    private int CalculateXPEarned(int amount)
    {
        int baseXP = 10;
        float xpMultiplier = 1.5f;
        int xpEarned = (int)(baseXP * (Mathf.Pow(xpMultiplier, amount) - 1) / (xpMultiplier - 1));

        return xpEarned;
    }
}
