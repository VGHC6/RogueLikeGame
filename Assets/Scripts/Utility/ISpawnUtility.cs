// 生成工具
using System.Collections.Generic;
using UnityEngine;

// 敌人出生数据
public struct EnemySpawnData
{
    public GameObject GO;// 游戏物体
    public EnemyRuntimeData Data;
}

public interface ISpawnUtility : IUtility
{
    GameObject SpawnPlayer(Vector2 pos);// 生成玩家,传入位置 
    EnemySpawnData SpwanEnemy(Vector2 atPosition);// 生成敌人,传入位置
    GameObject SpawnPickup(ItemConfig config, Vector2 atPosition);// 生成道具
    void CleanupAll();// 清理所有
}





public class SpawnUtility : ISpawnUtility
{
    private IAchitecture _architecture;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;// 获取架构

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public GameObject SpawnPlayer(Vector2 pos)
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Player");
        var go = GameObject.Instantiate(perfab, pos, Quaternion.identity);// 实例化玩家
        return go;// 返回玩家
    }

    /// <summary>
    /// 生成敌人数据
    /// </summary>
    /// <param name="outEnmeyList">输出的敌人列表</param>
    public EnemySpawnData SpwanEnemy(Vector2 atPosition)
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Enemy");
        var go= GameObject.Instantiate(perfab, atPosition, Quaternion.identity);// 实例化敌人
        var date= BuildEnemyData(atPosition);// 构建敌人数据
        return new EnemySpawnData { GO = go, Data = date };// 返回敌人数据
    }

    public GameObject SpawnPickup(ItemConfig config, Vector2 atPosition)
    {
        var go = GameObject.Instantiate(config.prefab, atPosition, Quaternion.identity);
        return go;
    }


    /// <summary>
    /// 清理所有生成的物体
    /// </summary>
    public void CleanupAll()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy")) GameObject.Destroy(obj);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) { GameObject.Destroy(player); }
        var exits = GameObject.FindGameObjectsWithTag("ExitPoint");
        foreach (var exit in exits) GameObject.Destroy(exit);
    }

    /// <summary>
    /// 构建敌人运行时数据
    /// </summary>
    /// <param name="pos">出生位置</param>
    /// <returns>敌人运行时数据</returns>
    private EnemyRuntimeData BuildEnemyData(Vector2 pos) => new EnemyRuntimeData
    {
        MaxHp = 6,
        CurrentHp = 6,
        AttackPower = 1,
        DefensePower = 1,
        AttackRange = 1f,
        ChaseRange = 5f,
        MoveSpeed = 3f,
        AttackDuration = 0.5f,
        HitCheckTime = 0.25f,
        HurtDuration = 0.4f,
        KnockbackForce = 8f,
        KnockbackDecay = 0.85f,
        State = EnemyActionState.Idle,
        Position = pos
    };


    /// <summary>
    /// 临时生成位置
    /// </summary>
    /// <returns>生成位置数组</returns>
    //Vector2[] GetSpawnPositions() => new[]
    //{
    //    new Vector2(3f,  1f),
    //    new Vector2(5f, -1f),
    //    new Vector2(7f,  0f),
    //};
}