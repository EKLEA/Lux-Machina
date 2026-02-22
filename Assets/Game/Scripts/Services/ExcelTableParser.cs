#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System;

public class CsvTableParserEditor : EditorWindow
{
    TextAsset buildingsBaseCsv;
    TextAsset buildingsStorageCsv;
    TextAsset buildingsProcessionCsv;
    TextAsset buildingsItemRequestsCsv;
    TextAsset buildingsEnergyCsv;
    TextAsset recipesCsv;
    TextAsset itemsCsv;

    [MenuItem("Tools/Parse CSV Tables")]
    public static void ShowWindow()
    {
        GetWindow<CsvTableParserEditor>("CSV Parser");
    }

    void OnGUI()
    {
        GUILayout.Label("CSV Tables Parser", EditorStyles.boldLabel);

        
        buildingsBaseCsv =
            EditorGUILayout.ObjectField("Buildings Base CSV", buildingsBaseCsv, typeof(TextAsset), false)
            as TextAsset;
       
        buildingsStorageCsv =
            EditorGUILayout.ObjectField("Buildings Storage CSV", buildingsStorageCsv, typeof(TextAsset), false)
            as TextAsset;
        buildingsProcessionCsv =
            EditorGUILayout.ObjectField("Buildings Procession CSV", buildingsProcessionCsv, typeof(TextAsset), false)
            as TextAsset;
        buildingsItemRequestsCsv =
            EditorGUILayout.ObjectField("Buildings Item Requests CSV", buildingsItemRequestsCsv, typeof(TextAsset), false)
            as TextAsset;

         buildingsEnergyCsv =
            EditorGUILayout.ObjectField("Buildings Enegty CSV", buildingsEnergyCsv, typeof(TextAsset), false)
            as TextAsset;

        recipesCsv =
            EditorGUILayout.ObjectField("Recipes CSV", recipesCsv, typeof(TextAsset), false)
            as TextAsset;
        itemsCsv =
            EditorGUILayout.ObjectField("Items CSV", itemsCsv, typeof(TextAsset), false)
            as TextAsset;
        if (GUILayout.Button("Parse All Tables"))
        {
            ParseAllTables();
        }
    }

    void ParseAllTables()
    {
        try
        {
           
            if (buildingsBaseCsv != null)
            {
                Debug.Log($"Parsing Buildings CSV: {buildingsBaseCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsBaseCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildingsBaseConfig(csvText);
            }
            if (buildingsStorageCsv != null)
            {
                Debug.Log($"Parsing Buildings Storage CSV: {buildingsStorageCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsStorageCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildingsStorageConfig(csvText);
            }
            if (buildingsProcessionCsv != null)
            {
                Debug.Log($"Parsing Buildings Procession CSV: {buildingsProcessionCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsProcessionCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildingsProcessionConfig(csvText);
            } 
            if (buildingsItemRequestsCsv != null)
            {
                Debug.Log($"Parsing Item Requests CSV: {buildingsItemRequestsCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsItemRequestsCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildingsItemRequestsConfig(csvText);
            }
            if (buildingsEnergyCsv != null)
            {
                Debug.Log($"Parsing Building Energy Configs CSV: {buildingsEnergyCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsEnergyCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildingsEnergyConfig(csvText);
            }
            if (recipesCsv != null)
            {
                Debug.Log($"Parsing Recipes CSV: {recipesCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(recipesCsv),
                    System.Text.Encoding.UTF8
                );
                ParseRecipes(csvText);
            }

            if (itemsCsv != null)
            {
                Debug.Log($"Parsing Items CSV: {itemsCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(itemsCsv),
                    System.Text.Encoding.UTF8
                );
                ParseItems(csvText);
            }
            EditorUtility.DisplayDialog("Success", "All tables parsed successfully!", "OK");
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to parse: {ex.Message}", "OK");
            Debug.LogError($"Parse error: {ex.Message}\n{ex.StackTrace}");
        }
    }

#region buildings
    void ParseBuildingsBaseConfig(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var buildings = new List<BuildingBaseConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 14)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings CSV: not enough columns ({row.Length})"
                );
                continue;
            }

            try
            {
                buildings.Add(
                    new BuildingBaseConfig
                    {
                        id = row[0],
                        title = row[1],
                        description = row[2],
                        iconPath = row[3],
                        prefabPath = row[4],
                        buildingType = (BuildingsTypes)int.Parse(row[5]),
                        actionType = (ActionType)int.Parse(row[6]),
                        size = new Vector3Int(
                            int.Parse(row[7]),
                            int.Parse(row[8]),
                            int.Parse(row[9])
                        ),
                        typeOfLogic = (TypeOfLogic)int.Parse(row[10]),
                        maxHealth = float.Parse(row[11]),
                        timeToStartRestore = float.Parse(row[12]),
                        restoreHealthPerSecond = float.Parse(row[13]),
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing building at row {i}: {ex.Message}");
                Debug.LogError($"Row data: {string.Join(" | ", row)}");
                throw;
            }
        }

        SaveToJson(new BuildingBaseConfigList { buildingsBaseConfigs = buildings }, "buildings");
        Debug.Log($"Parsed {buildings.Count} buildings");
    }
    void ParseBuildingsStorageConfig(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var buildingsStorages = new List<BuildingStorageConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 3)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings CSV: not enough columns ({row.Length})"
                );
                continue;
            }
            try
            {
                buildingsStorages.Add(
                    new BuildingStorageConfig
                    {
                        BuildingID = row[0],
                        MaxSlots = int.Parse(row[1]),
                        ItemsTypes=ParseHashSet<ItemClass>(row[2]) 

                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing building at row {i}: {ex.Message}");
                Debug.LogError($"Row data: {string.Join(" | ", row)}");
                throw;
            }
        }

        SaveToJson(new BuildingStorageConfigList { storageConfigs = buildingsStorages }, "buildingsStorages");
        Debug.Log($"Parsed {buildingsStorages.Count} buildingsStorages");
    }
    void ParseBuildingsProcessionConfig(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var buildingProcessions = new List<BuildingProcessionConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 3)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings CSV: not enough columns ({row.Length})"
                );
                continue;
            }
            try
            {
                buildingProcessions.Add(
                    new BuildingProcessionConfig
                    {
                        BuildingID = row[0],
                        typeOfProcession = (TypeOfProcession)int.Parse(row[1]),
                        requiredRecipesGroup=ParseHashSet<RequiredRecipesGroup>(row[2]) 

                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing building at row {i}: {ex.Message}");
                Debug.LogError($"Row data: {string.Join(" | ", row)}");
                throw;
            }
        }

        SaveToJson(new BuildingProcessionConfigList { processionConfigs = buildingProcessions }, "buildingProcessions");
        Debug.Log($"Parsed {buildingProcessions.Count} buildingProcessions");
    }

    void ParseBuildingsItemRequestsConfig(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;
        var buildingItemRequests = new List<BuildingItemRequestsConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 2)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings CSV: not enough columns ({row.Length})"
                );
                continue;
            }
            try
            {
                buildingItemRequests.Add(
                    new BuildingItemRequestsConfig
                    {
                        BuildingID = row[0],
                        itemsRequest =ParseIngredientString(row[1]) ,

                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing building at row {i}: {ex.Message}");
                Debug.LogError($"Row data: {string.Join(" | ", row)}");
                throw;
            }
        }

        SaveToJson(new BuildingItemRequestsConfigList {  buildingItemRequestsConfigs= buildingItemRequests }, "buildingItemRequests");
        Debug.Log($"Parsed {buildingItemRequests.Count} buildingItemRequests");
    }
     void ParseBuildingsEnergyConfig(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var buildingEnegry = new List<BuildingEnegryConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 3)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings Energy CSV: not enough columns ({row.Length})"
                );
                continue;
            }

            try
            {
                buildingEnegry.Add(
                    new BuildingEnegryConfig
                    {
                        BuildingID = row[0],
                        radius = float.Parse(row[1]),
                        maxConnections = int.Parse(row[2]),
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing building at row {i}: {ex.Message}");
                Debug.LogError($"Row data: {string.Join(" | ", row)}");
                throw;
            }
        }

        SaveToJson(new BuildingEnegryConfigList { buildingEnegryConfigs = buildingEnegry }, "buildingsEnergy");
        Debug.Log($"Parsed {buildingEnegry.Count} buildings energy");
    }
#endregion
#region itemes
    void ParseItems(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var items = new List<ItemConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 6)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Items CSV: not enough columns ({row.Length})"
                );
                continue;
            }

            try
            {
                items.Add(
                    new ItemConfig
                    {
                        id = int.Parse(row[0]),
                        title = row[1],
                        description = row[2],
                        iconPath = row[3],
                        ItemClass = (ItemClass)int.Parse(row[4]),
                        ItemType = (ItemType)int.Parse(row[5]),
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing item at row {i}: {ex.Message}");
                throw;
            }
        }

        SaveToJson(new ItemConfigList { items = items }, "items");
        Debug.Log($"Parsed {items.Count} items");
    }

    void ParseRecipes(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var recipes = new List<RecipeConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 8)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Recipes CSV: not enough columns ({row.Length})"
                );
                continue;
            }

            try
            {
                recipes.Add(
                    new RecipeConfig
                    {
                        id = int.Parse(row[0]),
                        title = row[1].Trim(),
                        RecipesGroupIds = ParseHashSet<RequiredRecipesGroup>(row[2]),
                        ItemClass = (ItemClass)int.Parse(row[3]),
                        inputItems = ParseIngredientString(row[4]),
                        outputItems = ParseIngredientString(row[5]),
                        craftTime = float.Parse(row[6]),
                        recipeSpritePath = row[7],
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing recipe at row {i}: {ex.Message}");
                throw;
            }
        }

        SaveToJson(new RecipeConfigList { recipes = recipes }, "recipes");
        Debug.Log($"Parsed {recipes.Count} recipes");
    }
#endregion
    List<T> ParseHashSet<T>(string idsString) where T : struct, Enum
    {
        List<T> ids = new List<T>();
        
        if (string.IsNullOrEmpty(idsString) || idsString.Trim() == "0")
            return ids;

        string[] pairs = idsString.Split(',');
        foreach (string pair in pairs)
        {
            if (int.TryParse(pair.Trim(), out int id))
            {
                ids.Add((T)System.Enum.ToObject(typeof(T), id));
            }
        }
        return ids;
    }

    List<RecipeIngredient> ParseIngredientString(string ingredientString)
    {
        var ingredients = new List<RecipeIngredient>();

        if (string.IsNullOrEmpty(ingredientString.Trim()))
            return ingredients;

        string[] pairs = ingredientString.Split(',');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int amount))
            {
                string itemIdStr = parts[0].Trim();
                if (int.TryParse(itemIdStr, out int itemId))
                {
                    ingredients.Add(
                        new RecipeIngredient
                        {
                            itemId = itemId,
                            amount = amount,
                        }
                    );
                }
                else
                {
                    ingredients.Add(
                        new RecipeIngredient
                        {
                            itemId = itemIdStr.GetStableHashCode(),
                            amount = amount,
                        }
                    );
                }
            }
        }

        return ingredients;
    }

    List<string[]> ParseCsv(string csvText)
    {
        var rows = new List<string[]>();

        if (csvText.Length > 0 && csvText[0] == '\uFEFF')
        {
            csvText = csvText.Substring(1);
        }

        var lines = csvText.Split('\n');

        char delimiter = ';';
        if (lines.Length > 0 && lines[0].Contains(',') && !lines[0].Contains(';'))
        {
            delimiter = ',';
        }

        Debug.Log($"Using delimiter: '{delimiter}'");

        foreach (var line in lines)
        {
            var l =line.Trim();
            if (string.IsNullOrWhiteSpace(l))
                continue;

            var fields = new List<string>();
            var currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < l.Length; i++)
            {
                char c = l[i];

                if (c == '"')
                {
                    if (inQuotes && i < l.Length - 1 && l[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString().Trim());

            // Убираем пустые строки
            if (fields.Count > 1 || !string.IsNullOrEmpty(fields[0]))
            {
                rows.Add(fields.ToArray());
            }
        }

        Debug.Log($"Parsed {rows.Count} rows from CSV");

        // Отладочный вывод первой строки данных
        if (rows.Count > 1)
        {
            Debug.Log($"First data row: {string.Join(" | ", rows[1])}");
        }

        return rows;
    }

    void SaveToJson(IWrapper data, string fileName)
    {
        try
        {
            string json=JsonUtility.ToJson(data, true);

            string directory = Application.dataPath + "/Resources/Configs/JsonData";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            string filePath = $"{directory}/{fileName}.json";
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            Debug.Log($"Saved: {fileName}.json with {json.Length} characters");
            Debug.Log($"JSON content for {fileName}: {json}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving {fileName}.json: {ex.Message}");
        }
    }
}
#endif