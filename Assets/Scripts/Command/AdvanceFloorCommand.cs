//发送命令，切换楼层
using System.Linq;

public class AdvanceFloorCommand : AbstractCommand
{
    protected override void OnExcute()
    {
        var state = this.GetModel<IGameStateModel>();

        if (state._currentFloor >= state._maxFloor)
        {
            state.GameOver(true);

            return;
        }

        //清理旧数据，生成新的
        this.GetUtility<ISpawnUtility>().CleanupAll();
        var enemyModel = this.GetModel<IEnemyModel>();
        enemyModel.GetAll().ToList().ForEach(id => enemyModel.Unregister(id.Key));
        this.GetModel<IMapModel>().Clearup();

        state._currentFloor++;

        //发送事件
        this.SendEvent(new FloorAdvancedEvent { newFloorIndex = state._currentFloor });
    }
}