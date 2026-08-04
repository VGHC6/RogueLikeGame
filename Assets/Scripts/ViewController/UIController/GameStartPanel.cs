using UnityEngine;
public class GameStartPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnStartButton()
    {
        this.GetModel<IGameStateModel>().StartGame();//修改状态,游戏进行
    }
}