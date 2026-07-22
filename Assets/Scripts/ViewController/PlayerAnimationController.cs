using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//玩家动画控制器
public class PlayerAnimationController : MonoBehaviour, IController
{
    private Animator _animator;
    public IAchitecture GetArchitecture()
    {
        return RogueLikeGame.Interface;//获取结构
    }

    void Awake()
    {
        //获取动画组件
        _animator = GetComponent<Animator>();//获取动画组件
    }

    void Start()
    {
        //注册事件
        this.RegisterEvent<PlayerStateChangedEvent>(OnPlayerStateChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnPlayerStateChanged(PlayerStateChangedEvent e)
    {
        _animator.CrossFade(e.AnimationName, 0.1f);//切换动画
    }
}
