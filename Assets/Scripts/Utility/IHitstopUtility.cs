using System.Collections;
using UnityEngine;

public interface IHitstopUtility : IUtility
{
    void Init(MonoBehaviour runner);
    void Trigger(float duration);
}

public class HitstopUtility : IHitstopUtility
{
    private IAchitecture _architecture;
    private MonoBehaviour _runner;
    private Coroutine _current;
    private bool _isFrozen;
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
        if (_current != null) _runner.StopCoroutine(_current);
        _current = _runner.StartCoroutine(Run(duration));
    }

    private IEnumerator Run(float duration)
    {
        _isFrozen = true;
        Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1;
        _isFrozen = false;
    }
}
