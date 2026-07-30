//用来实现帧停滞效果
using System.Collections;
using UnityEngine;

public interface IHitstopUtility : IUtility
{
    void Trigger(float duration);//触发帧停滞
}

public class HitstopUtility : IHitstopUtility
{
    private IAchitecture _architecture;
    private MonoBehaviour _runner;//帧停滞协程运行器
    private Coroutine _current;//当前帧停滞协程
    private bool _isFrozen;//是否正在运行帧停滞协程
    public IAchitecture GetArchitecture()
    {
        return _architecture;
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }

    public void Init(MonoBehaviour runner)
    {
        _runner = runner;
    }

    public void Trigger(float duration)
    {
        if (_runner == null) return;
        if (_current != null) _runner.StopCoroutine(_current);//防止协程重复运行
        _current = _runner.StartCoroutine(Run(duration));//开始帧停滞协程
    }

    private IEnumerator Run(float duration)
    {
        _isFrozen = true;
        Time.timeScale = 0;//冻结时间
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1;//恢复时间
        _isFrozen = false;
    }
}