using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
public class UIMangager : MonoBehaviour, IController
{
    [SerializeField] private GameObject _gameStartPanelPrefab;//游戏开始面板
    [SerializeField] private GameObject _gameplayPanelPrefab;//游戏运行面板
    [SerializeField] private GameObject _gameOverPanelPrefab;//游戏结束面板
    [SerializeField] private GameObject _pausePanelPrefab;//暂停面板
    [SerializeField] private GameObject _saveLoadPanelPrefab;//存档面板

    private Dictionary<UIPanelType, GameObject> _panelPrefabs = new Dictionary<UIPanelType, GameObject>();//存放 面板预制体
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.RegisterEvent<UIPanelChangeEvent>(OnPanelChange);
    }

    void Start()
    {
        this.GetSystem<IUISystem>().Changepanel(UIPanelType.Start);
    }


    void Update()
    {
        if (this.GetUtility<IInputUtility>().Pause)
        {
            var state = this.GetModel<IGameStateModel>();
            if (state._currentPhase.Value == UIPanelType.GamePlay)
                state._currentPhase.Value = UIPanelType.Pause;
            else if (state._currentPhase.Value == UIPanelType.Pause)
                state._currentPhase.Value = UIPanelType.GamePlay;
        }
    }




    void OnPanelChange(UIPanelChangeEvent e)
    {
        //只要不是第一次初始化
        if(e.OldPanel!=UIPanelType.None&&_panelPrefabs.TryGetValue(e.OldPanel,out GameObject oldPanel))
        {
            oldPanel.SetActive(false);//隐藏
        }

        //if (e.OldPanel == UIPanelType.None) return;

        if(!_panelPrefabs.TryGetValue(e.NewPanel,out GameObject newPanel))
        {
            var perfab= GetPrefab(e.NewPanel);
            if (perfab == null) return;
            newPanel= Instantiate(perfab,transform);//实例化
            _panelPrefabs[e.NewPanel] = newPanel;//存入字典
        }
        newPanel.SetActive(true);//显示

        //控制输入
        if (e.NewPanel == UIPanelType.GamePlay)
        {
            this.GetUtility<IInputUtility>().Enable();
        }
        else
        {
            this.GetUtility<IInputUtility>().Disable();
        }
    }

    /// <summary>
    /// 获取面板预制体
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    GameObject GetPrefab(UIPanelType type) => type switch
    {
        UIPanelType.Start => _gameStartPanelPrefab,
        UIPanelType.GamePlay => _gameplayPanelPrefab,
        UIPanelType.GameOver => _gameOverPanelPrefab,
        UIPanelType.Pause=> _pausePanelPrefab,
        UIPanelType.SaveLoad => _saveLoadPanelPrefab,
        _ => null//其他面板暂时没有
    };
}