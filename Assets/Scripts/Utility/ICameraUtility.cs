using System.Collections;
using UnityEngine;

public interface ICameraUtility : IUtility
{
    void Init(MonoBehaviour runner);
    void Shake(float intensity, float duration);
}

public class CameraUtility : ICameraUtility
{
    private IAchitecture _architecture;
    private Camera _camera;
    private MonoBehaviour _runner;
    public IAchitecture GetArchitecture()
    {
        return _architecture;
    }

    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
        _camera = Camera.main;
    }

    public void Init(MonoBehaviour runner)
    {
        _runner = runner;
    }

    public void Shake(float intensity, float duration)
    {
        if (_camera == null || _runner == null) return;
        _runner.StartCoroutine(Run(intensity, duration));
    }

    private IEnumerator Run(float intensity, float duration)
    {
        float elapsed = 0f;
        Vector3 origin = _camera.transform.position;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float x = Random.Range(-1f, 1f) * intensity;
            float y = Random.Range(-1f, 1f) * intensity;
            _camera.transform.position = origin + new Vector3(x, y, 0);
            yield return null;
        }
    }
}
