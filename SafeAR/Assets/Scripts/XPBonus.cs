using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class XPBonus : MonoBehaviour
{
    [SerializeField] private int xpBonus = 10;

    private void OnMouseDown()
    {
        //add xp to player
        GameManager.Instance.CurrentPlayer.AddXP(xpBonus);
        //destroy the game object
        //Destroy(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
