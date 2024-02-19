using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public Text usernameText;
    public Text xpText;
    public Text levelText;
    public GameObject inventory;
    [SerializeField] private GameObject catchItemScreen;

    [SerializeField] private AudioClip btnSound;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        Assert.IsNotNull(inventory);
        Assert.IsNotNull(levelText);
        Assert.IsNotNull(xpText);
        Assert.IsNotNull(usernameText);
        Assert.IsNotNull(btnSound);
        Assert.IsNotNull(audioSource);
        Assert.IsNotNull(catchItemScreen);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        updateLevel();
        updateXP();
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

    public void btnClicked()
    {
        audioSource.PlayOneShot(btnSound);
        Debug.Log("Button clicked" + btnSound);
        toggleInventorry();
    }

    private void toggleInventorry()
    {
        inventory.SetActive(!inventory.activeSelf);
    }

    public void catchItem()
    {
        audioSource.PlayOneShot(btnSound);
        catchItemScreen.SetActive(!catchItemScreen.activeSelf);
    }

    //add items to the inventory if the catch button is clicked
    public void addItem(Item item)
    {
        GameManager.Instance.CurrentPlayer.AddItems(item);
        catchItemScreen.SetActive(false);
    }

    public void closeCatchItemScreen()
    {
        catchItemScreen.SetActive(false);
    }

}
