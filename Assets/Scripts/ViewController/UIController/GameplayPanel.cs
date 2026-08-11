using UnityEngine;
//游戏运行时的UI控制
public class GameplayPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Awake()
    {
        this.RegisterEvent<AllEnemiesDeadEvent>(OnAllEnemiesDead);//注册敌人死亡事件

    }

    public void Start()
    {
        this.GetModel<ICombatModel>().IsDead.RegisterOnValueChanged(OnPlayerDead);//注册玩家死亡事件
    }

    void OnAllEnemiesDead(AllEnemiesDeadEvent e)
    {
        var state = this.GetModel<IGameStateModel>();
        var rooms = this.GetModel<IMapModel>().Rooms;
        if (state._currentPhase.Value != UIPanelType.GamePlay) return;
        if (state._currentFloor >= state._maxFloor)
        {
            state.GameOver(true);
            return;
        }
        if (rooms.Count == 0) return;
        var exitPerfabs = Resources.Load<GameObject>("Perfabs/ExitPoint");
        Instantiate(exitPerfabs, rooms[rooms.Count - 1].Center, Quaternion.identity);
    }

    void OnPlayerDead(bool IsDead)
    {
        if(IsDead) this.GetModel<IGameStateModel>().GameOver(false);
    }
}