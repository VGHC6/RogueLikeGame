using UnityEngine;
//游戏运行时的UI控制
public class GameplayPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Awake()
    {
    }
}