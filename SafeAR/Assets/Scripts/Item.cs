using System.Collections;
using System.Collections.Generic;
using Mapbox.Utils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;

public class Item : MonoBehaviour
{
    [SerializeField] public string itemName;
    [SerializeField] public int itemQuantity;
    [SerializeField] private AudioClip itemSound;
    private CatchManager catchManager;
    private GameObject arCamera;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        Assert.IsNotNull(audioSource);
        Assert.IsNotNull(itemSound);
        
    }

    public string GetItemName
    { get { return itemName; } }
    public string SetItemName
    { set { itemName = value; } }
    public AudioClip GetItemSound
    { get { return itemSound; } }
    public int ItemQuantity
    { get { return itemQuantity; } set { itemQuantity = value; } }
    public Vector2d GeographicLocation { get; set; }

    private void Start()
    {
        DontDestroyOnLoad(this);
    }

    public void OnMouseDown()
    {
        audioSource.PlayOneShot(itemSound);
        GameObject arCamera = GameObject.Find("Main Camera AR");
        catchManager = Object.FindObjectOfType<CatchManager>();

        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (catchManager != null)
        {
            if (arCamera != null && arCamera.activeSelf)
            {
                float maxRayDistance = 15f;
                float distance = Vector3.Distance(arCamera.transform.position, transform.position);
                
                Debug.Log("Distance: " + distance);
                if (distance <= maxRayDistance)
                {
                    catchManager.CatchItemScreen(this);
                    Debug.Log("Item clicked");
                }
                else
                {
                    catchManager.CannotCatchScreenItemToFarAway(this);
                    Debug.Log("Item clicked too far away");
                }
            }
            else
            {
                catchManager.CannotCatchScreen(this);
                Debug.Log("Item clicked in non-AR mode");
            }
        }
    }
}
