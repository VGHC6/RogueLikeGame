//道具生成系统
using UnityEngine;
public interface IDropSystem : ISystem
{

}
public class DropSystem : AbstractSystem, IDropSystem
{
    private ItemConfig[] _itemConfigs;

    protected override void OnInit()
    {
        _itemConfigs = Resources.LoadAll<ItemConfig>("Config/Items");
        this.RegisterEvent<EnemyDeadEvent>(OnEnemyDead);
    }

    /// <summary>
    /// 敌人死亡事件
    /// </summary>
    /// <param name="e"></param>
    void OnEnemyDead(EnemyDeadEvent e)
    {
        if (_itemConfigs == null) return;

        var all = this.GetModel<IEnemyModel>().GetAll();
        if (!all.TryGetValue(e.EnemyId, out var enemyData)) return;

        var config = PickRandom();
        if (config == null) return;

        var go = this.GetUtility<ISpawnUtility>().SpawnPickup(config, enemyData.Position);
        go.GetComponent<IItemPickup>().Init(config);//初始化
    }

    /// <summary>
    /// 随机道具
    /// </summary>
    /// <returns></returns>
    ItemConfig PickRandom()
    {
        float totalWeight = 0f;
        foreach (var item in _itemConfigs)
            totalWeight += item.dropWeight;

        float r = Random.Range(0f, totalWeight);
        float accum = 0f;
        foreach (var item in _itemConfigs)
        {
            accum += item.dropWeight;
            if (r <= accum) return item;
        }
        return null;
    }
}
