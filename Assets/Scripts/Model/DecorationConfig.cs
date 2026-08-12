
//添加装饰配置
using UnityEngine;
using UnityEngine.SocialPlatforms;

[CreateAssetMenu(menuName = "RogueLike/DecorationConfig")]
public class DecorationConfig:ScriptableObject
{
    public string name; 
    public GameObject prefab;
    [Range(0, 1f)] public float weight;//权重越大，越常见 
}