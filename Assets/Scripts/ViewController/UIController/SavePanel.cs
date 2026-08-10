using UnityEngine;

public enum SavePanelMode
{
    Save, Load
}
public class SavePanel : MonoBehaviour, IController
{
    [SerializeField] SaveSlotItem[] _saveSlotItemPrefab;//三个存档
    [SerializeField] GameObject _panelRoot;//存档面板
    //[SerializeField] private GameObject _titleForSave;//标题
    //[SerializeField] private GameObject _titleForLoad;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;


    private void OnEnable()
    {
        var state = this.GetModel<IGameStateModel>();
        var mode = state.SaveMode;
        //切换面板
        //if (_titleForSave != null) _titleForSave.SetActive(mode == SavePanelMode.Save);
        //if (_titleForLoad != null) _titleForLoad.SetActive(mode == SavePanelMode.Load);

        RefreshSlots();
    }

    /// <summary>
    /// 刷新存档
    /// </summary>
    void RefreshSlots()
    {
        var _saveUtil = this.GetUtility<ISaveUtility>();
        for(int i = 0; i < 3; i++)
        {
            var info= _saveUtil.GetSlotinfo(i);//获取存档信息
           var isSave=this.GetModel<IGameStateModel>().SaveMode == SavePanelMode.Save;//是否是存档模式
            _saveSlotItemPrefab[i].Init(info, OnSlotClicked, isSave);
        }
    }

    /// <summary>
    /// 存档槽位点击事件
    /// </summary>
    /// <param name="index"></param>
    void OnSlotClicked(int index)
    {
        var state=this.GetModel<IGameStateModel>();//获取游戏状态

        if(state.SaveMode == SavePanelMode.Save)
        {
            this.SendCommand(new SaveGameCommand() { soltIndex = index });
            state.CloseSaveLoadPanel();
        }
        else
        {
            this.SendCommand(new LoadGameCommand() { slotIndex = index });
        }
    }

    /// <summary>
    /// 关闭存档面板
    /// </summary>
    public void OnCloseButton()
    {
        this.GetModel<IGameStateModel>().CloseSaveLoadPanel();
    }
}