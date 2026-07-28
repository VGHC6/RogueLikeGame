/// <summary>
/// µÐÈË´ý»ú×´Ì¬
/// </summary>
public class EnemyIdleState : AbstractSystem, IFSMState
{
    public string AnimationName { get; } = "Idle";

    public PlayerStateType StateType { get; } = PlayerStateType.Idle;

    public void OnEnter()
    {
    }

    public void OnExit()
    {
    }

    public void OnFixUpdate(float datetime)
    {
    }

    public void OnUpdate(float datetime)
    {
    }

    protected override void OnInit()
    {
    }
}


