using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class RecipesAndItemsWindow : UIScreen
{
    [Inject] IReadOnlyItemsInfo itemsInfo;
    [Inject] IReadOnlyRecipeInfo recipeInfoInfo;
    [SerializeField] Button[] ItemClassBTs;
    [SerializeField] Button[] ItemButtons;
    [SerializeField] TextMeshProUGUI ItemText;
    public event Action<int> onItemChoosed;
    public void SetUpWindowAsRecipesByRecipeGroup(HashSet<RequiredRecipesGroup>  groups)
    {
        foreach(var bt in ItemClassBTs)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        HashSet<ItemClass> classes;
        if (groups.Count>0)
        {
           classes = recipeInfoInfo.RecipeInfos.Values
            .Where(info => info.RecipesGroupIds.Any(id => groups.Contains((RequiredRecipesGroup)id)))
            .Select(info => info.ItemClass) 
            .ToHashSet();
                
        }
        else
            classes=Enum.GetValues(typeof(ItemClass)).Cast<ItemClass>()
                                 .ToHashSet();;
        
        int i=0;
        ItemText.text="Рецепты";
        foreach(ItemClass c in classes)
        {
            if (i >= ItemClassBTs.Length) break;

            ItemClassBTs[i].image.sprite = itemsInfo.GetItemClassBTSprite((int)c);
            ItemClassBTs[i].gameObject.SetActive(true);
            
            ItemClass capturedClass = c; 
            ItemClassBTs[i].onClick.AddListener(() => SetUpRecipesByItemClassesButtons (c,groups));
            
            i++;
        }
        SetUpRecipesByItemClassesButtons(classes.First(),groups);
        Open();
    }
    public void SetUpWindowAsItemsByItemClasses(HashSet<ItemType>  ItemTypes)
    {
        foreach(var bt in ItemClassBTs)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        
        var items=itemsInfo.ItemsInfos.Where(f=>ItemTypes.Contains(f.Value.ItemType)).Select(f=>f.Value);
        HashSet<ItemClass> classes=items.Select(f=>f.ItemClass).ToHashSet();
        int i=0;
        foreach(ItemClass c in classes)
        {
            ItemClassBTs[i].image.sprite=itemsInfo.GetItemClassBTSprite((int)c);
            ItemClassBTs[i].gameObject.SetActive(true);
            ItemClassBTs[i].onClick.AddListener(()=>SetUpItemsByItemClassesButtons(items.Where(f=>f.ItemClass==c)));
            i++;
        }
        
        SetUpItemsByItemClassesButtons(items.Where(f=>f.ItemClass==classes.First()));
        Open();
    }
    public override void Close()
    {
        foreach(var bt in ItemClassBTs)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        foreach(var bt in ItemButtons)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        onItemChoosed=null;
        base.Close();
    }
    void SetUpItemsByItemClassesButtons(IEnumerable<ItemConfig> itemConfigs)
    {
        
        foreach(var bt in ItemButtons)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        
        ItemText.text="Предметы";
        int i=0;
        
        foreach(var r in itemConfigs)
        {
            ItemButtons[i].image.sprite=recipeInfoInfo.GetRecipeSprite(r.id);
            ItemButtons[i].gameObject.SetActive(true);
            ItemButtons[i].onClick.AddListener(() =>
            {
                onItemChoosed?.Invoke(r.id);
                Close();
            });
            i++;
        }
    }
    void SetUpRecipesByItemClassesButtons(ItemClass itemClass,HashSet<RequiredRecipesGroup>  groups)
    {
        foreach(var bt in ItemButtons)
        {
            bt.onClick.RemoveAllListeners();
            bt.gameObject.SetActive(false);
        }
        var recipes=recipeInfoInfo.RecipeInfos.Where(f=>f.Value.ItemClass==itemClass).Select(f=>f.Value);
        if (groups.Count>0)
        {
            recipes=recipes.Where(f=>f.RecipesGroupIds.Any(id => groups.Contains(id)));
        }
        int i=0;
        foreach(var r in recipes)
        {
            ItemButtons[i].image.sprite=recipeInfoInfo.GetRecipeSprite(r.id);
            ItemButtons[i].gameObject.SetActive(true);
            ItemButtons[i].onClick.AddListener(() =>
            {
                onItemChoosed?.Invoke(r.id);
                Close();
            });
            i++;
        }
    }
}