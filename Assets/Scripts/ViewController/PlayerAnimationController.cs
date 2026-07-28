using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationController : MonoBehaviour, IController
{
    private Animator _animator;
    private PlayerController _playerController;

    public IAchitecture GetArchitecture() => _playerController.GetArchitecture();

    void Awake()
    {
        _playerController = GetComponent<PlayerController>();//µÃµ½½ÇÉ«
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        this.RegisterEvent<PlayerStateChangedEvent>(OnPlayerStateChanged)
            .UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnPlayerStateChanged(PlayerStateChangedEvent e)
    {
        _animator.CrossFade(e.AnimationName, 0.1f);
    }
}
