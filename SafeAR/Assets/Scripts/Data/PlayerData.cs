using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class PlayerData
{
    public int xp;
    //public int level;
    public List<ItemData> items;

    public PlayerData(Player player)
    {
        xp = player.GetXP;
        //level = player.GetLevel;
        items = new List<ItemData>();
        foreach (Item item in player.GetItems)
        {
            items.Add(ItemData.CreateFromItem(item));
        }
    }

    public PlayerData() { 
        items = new List<ItemData>();
    }
}
