using UnityEngine;

public class PausePanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void OnContinueButton()
    {
        this.GetModel<IGameStateModel>()._currentPhase.Value = UIPanelType.GamePlay;
    }

    public void OnSaveButton()
    {
        this.GetModel<IGameStateModel>().OpenSaveLoadPanel(SavePanelMode.Save, UIPanelType.Pause);
    }

    public void OnLoadButton()
    {
        this.GetModel<IGameStateModel>().OpenSaveLoadPanel(SavePanelMode.Load, UIPanelType.Pause);
    }

    public void OnReturnToMenuButton()
    {
        this.GetModel<IGameStateModel>().ReturnToMenu();
    }
}
