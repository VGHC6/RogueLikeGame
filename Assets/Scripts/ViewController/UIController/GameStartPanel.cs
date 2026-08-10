using UnityEngine;
public class GameStartPanel : MonoBehaviour, IController
{
    [SerializeField] private GameObject _savePanel;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnStartButton()
    {
        this.GetModel<IGameStateModel>().StartGame();//修改状态,游戏进行
    }

    public void OnLoadButton()
    {
        this.GetModel<IGameStateModel>().OpenSaveLoadPanel(SavePanelMode.Load, UIPanelType.Start);
    }
}