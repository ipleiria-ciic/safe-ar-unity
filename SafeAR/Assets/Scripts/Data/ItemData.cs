using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemData
{
    public string itemName;
    public int itemQuantity;

    public ItemData() {}

    public static ItemData CreateFromItem(Item item)
    {
        return new ItemData { itemName = item.GetItemName, itemQuantity = item.ItemQuantity };
    }
}
