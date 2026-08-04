//������
using System.Collections.Generic;
using UnityEngine;

//���ɵ��˵�����
public struct EnemySpawnData
{
    public GameObject GO;//���
    public EnemyRuntimeData Data;
}

public interface ISpawnUtility : IUtility
{
    GameObject SpawnPlayer();//�������
    void SpwanEnemy(List<EnemySpawnData> outEnmeyList);//���ɵ���
    void CleanupAll();//��������
}





public class SpawnUtility : ISpawnUtility
{
    private IAchitecture _architecture;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public GameObject SpawnPlayer()
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Player");
        var go = GameObject.Instantiate(perfab, Vector2.zero, Quaternion.identity);//�������
        return go;//�������
    }

    /// <summary>
    /// ���ɵ��˵�����
    /// </summary>
    /// <param name="outEnmeyList"></param>
    public void SpwanEnemy(List<EnemySpawnData> outEnmeyList)
    {
        var perfab = Resources.Load<GameObject>("Perfabs/Enemy");
        foreach (var pos in GetSpawnPositions())
        {
            var go = GameObject.Instantiate(perfab, pos, Quaternion.identity);//���ɵ���
            var data = BuildEnemyData(pos);
            outEnmeyList.Add(new EnemySpawnData { GO = go, Data = data });
        }
    }

    /// <summary>
    /// ��������
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public void CleanupAll()
    {
        foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy")) GameObject.Destroy(obj);
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) { GameObject.Destroy(player); }
    }

    /// <summary>
    /// ���ɵ��˵�����
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
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
    /// ��ʱ���������λ��
    /// </summary>
    /// <returns></returns>
    Vector2[] GetSpawnPositions() => new[]
    {
        new Vector2(3f,  1f),
        new Vector2(5f, -1f),
        new Vector2(7f,  0f),
    };
}