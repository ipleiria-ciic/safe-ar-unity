using System.Collections;
using System.Collections.Generic;
using System.IO;
using Assets.Mapbox.Unity.MeshGeneration.Modifiers.MeshModifiers;
using Mapbox.Json;
using UnityEngine;

public static class DataManager
{
    private static string path = Application.persistentDataPath + "/data.json";

    public static void SaveData(Player player)
    {
        PlayerData playerData = new PlayerData(player);
        Debug.Log("Saving data: " + playerData.items.Count);

        string json = JsonConvert.SerializeObject(playerData, Formatting.Indented);

        File.WriteAllText(path, json);
        Debug.Log("Saved data to " + path);
    }

    public static PlayerData LoadData()
    {
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            Debug.Log("Loaded json: " + json);
            PlayerData playerData = JsonConvert.DeserializeObject<PlayerData>(json);
            return playerData;
        }
        else
        {
            return null;
        }
    }
}
