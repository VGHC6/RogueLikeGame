using TMPro;
using UnityEngine;
public class GameOverPanel : MonoBehaviour, IController
{
    [SerializeField] public TMP_Text _resultText;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    private void OnEnable()
    {
        _resultText.text = this.GetModel<IGameStateModel>().IsWin ? "You Win!" : "You Lose!";
    }

    /// <summary>
    /// 按下结束按钮,返回主菜单
    /// </summary>
    public void OnRestartButton()
    {
        this.GetModel<IGameStateModel>().ReturnToMenu();
    }
}
