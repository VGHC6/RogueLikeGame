
using System.Diagnostics;

public class PlayerStateChangedEvent
{
    public string AnimationName { get; set; }//动画名称
    public PlayerStateType StateType { get; set; }//状态类型
}