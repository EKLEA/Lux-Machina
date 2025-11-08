#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class CsvTableParserEditor : EditorWindow
{
    private TextAsset itemsCsv;
    private TextAsset buildingsCsv;
    private TextAsset recipesCsv;

    [MenuItem("Tools/Parse CSV Tables")]
    public static void ShowWindow()
    {
        GetWindow<CsvTableParserEditor>("CSV Parser");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV Tables Parser", EditorStyles.boldLabel);
        GUILayout.Label("Работает с CSV которые выглядят как Excel", EditorStyles.miniLabel);

        itemsCsv =
            EditorGUILayout.ObjectField("Items CSV", itemsCsv, typeof(TextAsset), false)
            as TextAsset;
        buildingsCsv =
            EditorGUILayout.ObjectField("Buildings CSV", buildingsCsv, typeof(TextAsset), false)
            as TextAsset;
        recipesCsv =
            EditorGUILayout.ObjectField("Recipes CSV", recipesCsv, typeof(TextAsset), false)
            as TextAsset;

        if (GUILayout.Button("Parse All Tables"))
        {
            ParseAllTables();
        }
    }

    private void ParseAllTables()
    {
        try
        {
            if (itemsCsv != null)
            {
                Debug.Log($"Parsing Items CSV: {itemsCsv.name}");
                // Явно читаем файл с кодировкой UTF-8
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(itemsCsv),
                    System.Text.Encoding.UTF8
                );
                ParseItems(csvText);
            }
            if (buildingsCsv != null)
            {
                Debug.Log($"Parsing Buildings CSV: {buildingsCsv.name}");
                string csvText = File.ReadAllText(
                    AssetDatabase.GetAssetPath(buildingsCsv),
                    System.Text.Encoding.UTF8
                );
                ParseBuildings(csvText);
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

            EditorUtility.DisplayDialog("Success", "All tables parsed successfully!", "OK");
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to parse: {ex.Message}", "OK");
            Debug.LogError($"Parse error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void ParseItems(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var items = new List<ItemConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 5)
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
                        id = row[0].Trim(),
                        title = row[1],
                        description = row[2],
                        iconPath = row[3],
                        maxInStack = int.Parse(row[4]),
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

    private void ParseBuildings(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var buildings = new List<BuildingConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 15)
            {
                Debug.LogWarning(
                    $"Skipping row {i} in Buildings CSV: not enough columns ({row.Length})"
                );
                continue;
            }

            try
            {
                buildings.Add(
                    new BuildingConfig
                    {
                        id = row[0].Trim(),
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
                        requiredRecipesGroup = (RequiredRecipesGroup)int.Parse(row[11]),
                        maxHealth = float.Parse(row[12]),
                        timeToStartRestore = float.Parse(row[13]),
                        restoreHealthPerSecond = float.Parse(row[14]),
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

        SaveToJson(new BuildingConfigList { buildings = buildings }, "buildings");
        Debug.Log($"Parsed {buildings.Count} buildings");
    }

    private void ParseRecipes(string csvText)
    {
        var rows = ParseCsv(csvText);
        if (rows.Count < 2)
            return;

        var recipes = new List<RecipeConfig>();

        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length < 7)
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
                        id = row[0].Trim(),
                        groupId = int.Parse(row[1]),
                        inputItems = ParseIngredientString(row[2]),
                        outputItems = ParseIngredientString(row[3]),
                        craftTime = float.Parse(row[4]),
                        recipeSpritePath = row[5],
                        buildingId = row[6],
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

    private List<RecipeIngredient> ParseIngredientString(string ingredientString)
    {
        var ingredients = new List<RecipeIngredient>();

        if (string.IsNullOrEmpty(ingredientString))
            return ingredients;

        string[] pairs = ingredientString.Split(',');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int amount))
            {
                ingredients.Add(
                    new RecipeIngredient
                    {
                        itemId = parts[0].Trim().GetStableHashCode(),
                        amount = amount,
                    }
                );
            }
        }

        return ingredients;
    }

    private List<string[]> ParseCsv(string csvText)
    {
        var rows = new List<string[]>();

        // Удаляем BOM символ если есть (для UTF-8)
        if (csvText.Length > 0 && csvText[0] == '\uFEFF')
        {
            csvText = csvText.Substring(1);
        }

        var lines = csvText.Split('\n');

        // Определяем разделитель
        char delimiter = ';';
        if (lines.Length > 0 && lines[0].Contains(',') && !lines[0].Contains(';'))
        {
            delimiter = ',';
        }

        Debug.Log($"Using delimiter: '{delimiter}'");

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = new List<string>();
            var currentField = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i < line.Length - 1 && line[i + 1] == '"')
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

    private void SaveToJson(object data, string fileName)
    {
        try
        {
            string json = "";

            // Выбираем правильный wrapper в зависимости от типа данных
            if (data is BuildingConfig[])
            {
                var wrapper = new BuildingConfigList
                {
                    buildings = new List<BuildingConfig>((BuildingConfig[])data),
                };
                json = JsonUtility.ToJson(wrapper, true);
            }
            else if (data is ItemConfig[])
            {
                var wrapper = new ItemConfigList
                {
                    items = new List<ItemConfig>((ItemConfig[])data),
                };
                json = JsonUtility.ToJson(wrapper, true);
            }
            else if (data is RecipeConfig[])
            {
                var wrapper = new RecipeConfigList
                {
                    recipes = new List<RecipeConfig>((RecipeConfig[])data),
                };
                json = JsonUtility.ToJson(wrapper, true);
            }
            else
            {
                json = JsonUtility.ToJson(data, true);
            }

            string directory = Application.dataPath + "/Resources/Configs/JsonData";
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Сохраняем с кодировкой UTF-8
            string filePath = $"{directory}/{fileName}.json";
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);

            Debug.Log($"Saved: {fileName}.json with {json.Length} characters");

            // Для отладки выводим содержимое JSON
            Debug.Log($"JSON content for {fileName}: {json}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error saving {fileName}.json: {ex.Message}");
        }
    }
}
#endif
