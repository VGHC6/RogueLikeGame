using UnityEngine;

// 敌人动作状态枚举
public enum EnemyActionState
{
    Idle,
    Chase,
    Attack,
    Hurt,
    Dead
}

// 敌人运行时数据
public struct EnemyRuntimeData
{
    public int EnemyId;
    public int CurrentHp;
    public int MaxHp;
    public int AttackPower;
    public int DefensePower;
    public float AttackRange;
    public float ChaseRange;
    public float MoveSpeed;
    public float AttackDuration;
    public float HitCheckTime;
    public float HurtDuration;
    public float KnockbackForce;
    public float KnockbackDecay;
    public EnemyActionState State;
    public float StateTimer;
    public bool HitChecked;
    public Vector2 Position;
    public Vector2 MoveDelta;
    public Vector2 KnockbackVelocity;
    public int FacingDir;
    public bool IsDead;
}
