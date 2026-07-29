using UnityEngine;
public interface IAnimationUtility : IUtility
{
    void Init(Animator animator);
}

public class AnimationUtility : IAnimationUtility
{
    private IAchitecture _architecture;
    private Animator _animator;
    public IAchitecture GetArchitecture() => _architecture;
    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }
    public void Init(Animator animator)
    {
        _animator = animator;
        _architecture.RegisterEvent<PlayerStateChangedEvent>(OnStateChanged);
    }

    void OnStateChanged(PlayerStateChangedEvent e)
    {
        //Debug.Log($"[{_animator?.name}] Animation: {e.AnimationName}, AnimatorNull:{_animator == null}");
        _animator?.CrossFade(e.AnimationName, 0.2f);
    }

}
