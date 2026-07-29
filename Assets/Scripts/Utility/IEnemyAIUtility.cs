using UnityEngine;

public interface IEnemyAIUtility:IUtility
{
    Vector2 ChaseDirection { get; }//指向玩家的归一化方向
    bool HasTarget { get; }//是否有玩家
}



public class EnemyAIUtility : IEnemyAIUtility
{
    private Transform _self;
    private Transform _target;
    
    private IAchitecture _architecture;
    public Vector2 TargetPosition { get
        {
            return _target.position;
        }
    }
    public Vector2 ChaseDirection { get
        {
            if(_target == null) return Vector2.zero;
            return (_target.position - _self.position).normalized;
        }
    }
    public bool HasTarget=> _target != null;
    public IAchitecture GetArchitecture() => _architecture;

    public void Awake(Transform self) {
        _self = self;
        var player = GameObject.FindWithTag("Player");
        if(player != null) _target=player.transform;
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }
}