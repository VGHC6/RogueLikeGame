using UnityEngine;
//游戏运行时的UI控制
public class GameplayPanel : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Awake()
    {
        this.RegisterEvent<EnemyDeadEvent>(OnEnemyDead);//注册敌人死亡事件
    }

    public void Start()
    {
        this.GetModel<ICombatModel>().IsDead.RegisterOnValueChanged(OnPlayerDead);//注册玩家死亡事件
    }

    void OnEnemyDead(EnemyDeadEvent e)
    {
        if(this.GetModel<IEnemyModel>().GetAll().Count == 0)
        {
            this.GetModel<IGameStateModel>().GameOver(true);
        }
    }

    void OnPlayerDead(bool IsDead)
    {
        if(IsDead) this.GetModel<IGameStateModel>().GameOver(false);
    }
}