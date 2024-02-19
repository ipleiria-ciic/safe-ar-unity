using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class ItemFactory : Singleton<ItemFactory>
{
    [SerializeField] private Item[] availableItems;
    [SerializeField] private Player player;
    [SerializeField] private float waitTime = 30f;
    [SerializeField] private float minRange = 3f;
    [SerializeField] private float maxRange = 15f;

    private List<Item> spawnedItems = new List<Item>();
    private Item selectedItem;

    private Dictionary<Vector3, Item> itemPositions;

    [SerializeField] private Item wood;
    [SerializeField] private Item cloth;
    [SerializeField] private Item metal;
    [SerializeField] private Item food;

    private Vector3 patioA = new Vector3(15, 10, 104);
    private Vector3 edificioD = new Vector3(24, 10, -13);
    private Vector3 estacioinamentoA = new Vector3(61, 5, 66);
    private Vector3 edificioB = new Vector3(-51.5f, 10, 15.3f);
    private Vector3 edificioC = new Vector3(-58f, 10, -90f);
    private Vector3 dakar = new Vector3(-65.2f, 10, 65.5f);
    private Vector3 cantinaBaixo = new Vector3(-115, 10, 5.5f);
    private Vector3 estacionamentoD = new Vector3(-11.5f, 10, -60.3f);

    public Item SelectedItem
    { get { return selectedItem; } }

    public List<Item> SpawnedItems { get { return spawnedItems; } }

    private void Awake()
    {
        Assert.IsNotNull(availableItems);
        Assert.IsNotNull(player);
    }

    // Start is called before the first frame update
    private void Start()
    {
        /*for (int i = 0; i < availableItems.Length; i++)
        {
            SpawnItem();
        }*/

        StartCoroutine(SpawnItemRoutine());

    }

    public void ItemWasSelected(Item item)
    {
        selectedItem = item;
    }

    private IEnumerator SpawnItemRoutine()
    {
        /*while (true)
        {
           
            SpawnItem();
            yield return new WaitForSeconds(waitTime);
        }*/
    itemPositions = new Dictionary<Vector3, Item>()
        {
            { patioA, cloth },
            { edificioD, cloth },
            { estacioinamentoA, metal },
            { edificioB, cloth },
            { edificioC, cloth },
            { dakar, wood },
            { cantinaBaixo, food },
            { estacionamentoD, metal }
        };

        foreach (var kvp in itemPositions)
        {
            Vector3 position = kvp.Key;
            Item itemPrefab = kvp.Value;
            float distance = Vector3.Distance(player.transform.position, position);

            if (distance < 1000) // Adjust the threshold as needed
            {
                for (int i = 0; i < 5; i++)
                {

                    SpawnItem(itemPrefab, position);
                    yield return new WaitForSeconds(waitTime);
                }
            }
        }
    }

    private bool IsInRange()
    {
        float distance = Vector3.Distance(player.transform.position, edificioD);

        Debug.Log("Distance: " + distance);
        return distance <= 50;
    }

    public void SpawnItem(Item itemPrefab, Vector3 position)
    {
        //int random = Random.Range(0, availableItems.Length);
        //float x = player.transform.position.x + GenerateRange();
        //float y = player.transform.position.y + 5;
        //float z = player.transform.position.z + GenerateRange();
        //Vector3 position = new Vector3(x, y, z);
        //make the item spawn whth a 90 degree rotation
        //Quaternion rotation = Quaternion.Euler(0, 0, 0);

        Vector3 spawnPosition = new Vector3(position.x + GenerateRange(), position.y , position.z + GenerateRange());
        spawnedItems.Add(Instantiate(itemPrefab, spawnPosition, Quaternion.identity));
    }

    private float GenerateRange()
    {
        float randomNumber = Random.Range(minRange, maxRange);
        bool isPositive = Random.Range(0, 10) < 5;
        return randomNumber * (isPositive ? 1 : -1);
    }
}

