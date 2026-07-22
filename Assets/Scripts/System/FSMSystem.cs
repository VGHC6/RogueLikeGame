using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IFSMSystem : ISystem
{
    IFSMState _currentState { get; }//当前状态
    void Update(float deltaTime);//更新状态

    void ChangeState<T>() where T : IFSMState, new();//切换状态
}


public class FSMSystem : AbstractSystem, IFSMSystem
{
    public IFSMState _currentState { get; private set; }//当前状态
    public PlayerStateType _stateType { get; private set; }//状态类型
    private Dictionary<string, IFSMState> _States = new Dictionary<string, IFSMState>();//状态字典
    private PlayerModel _playerModel;
    protected override void OnInit()
    {
        InsertState();//插入状态
        _currentState = new FsmIdleState();//初始化当前状态
        _playerModel = this.GetModel<PlayerModel>();//获取玩家模型
    }

    private void InsertState()
    {
        _States.Add("Idle", new FsmIdleState());
        _States.Add("Move", new FsmMoveState());
    }

    public void Update(float deltaTime)
    {
        _currentState?.OnUpdate(deltaTime);
    }

    public void ChangeState<T>() where T : IFSMState, new()
    {
        var newState = new T();//创建新状态
        var newStateType = newState.StateType;//获取新状态类型

        _currentState.OnExit();//退出当前状态
        _currentState = newState;//切换状态
        _stateType = newStateType;//切换状态类型
        _currentState.OnEnter();//进入新状态

        _playerModel._currentState.Value = newStateType;//更新玩家模型状态

        //发送事件
        this.SendEvent(new PlayerStateChangedEvent
        {
            StateType = newStateType,
            AnimationName = newState.AnimationName
        });
    }
}