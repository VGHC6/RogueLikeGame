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
        var go=Instantiate(_savePanel, this.transform);
        go.GetComponent<SavePanel>().Show(SavePanelMode.Load);
    }
}