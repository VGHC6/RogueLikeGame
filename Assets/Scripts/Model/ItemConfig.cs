using UnityEngine;

public enum ItemType
{
    Heal,
    AtkUp,
    DefUp,
    SpeedUp,
    MaxHpUp
}

[CreateAssetMenu(menuName = "RogueLike/ItemConfig")]
public class ItemConfig : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public GameObject prefab;
    public ItemType itemType;
    public int value;
    public float dropWeight;
}