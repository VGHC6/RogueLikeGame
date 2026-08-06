using System.Collections;
using UnityEngine;

public interface ICameraUtility : IUtility
{
    void Init(MonoBehaviour runner);
    void Shake(float intensity, float duration);
    void Follow(Transform pos);
    void SetBounds(float minX, float maxX, float minY, float maxY);
    void LateTick();
}

public class CameraUtility : ICameraUtility
{
    private IAchitecture _architecture;
    private Camera _camera;
    private Transform _followTarget;
    private float _minX, _maxX, _minY, _maxY;
    private bool _hasBounds;
    private MonoBehaviour _runner;

    private float _shakeIntensity;
    private float _shakeEndTime;
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
       _shakeIntensity=intensity;
       _shakeEndTime=Time.unscaledTime+duration;
    }

    /// <summary>
    /// 摄像机跟随
    /// </summary>
    /// <param name="pos"></param>
    public void Follow(Transform pos)
    {
        _followTarget = pos;
    }

    /// <summary>
    /// 设置摄像机边界
    /// </summary>
    /// <param name="minX"></param>
    /// <param name="maxX"></param>
    /// <param name="minY"></param>
    /// <param name="maxY"></param>
    public void SetBounds(float minX, float maxX, float minY, float maxY)
    {
        float camHalfH = _camera.orthographicSize;//摄像机高度的一半
        float camHalfW = _camera.aspect * camHalfH;//摄像机宽度的一半
        _minX = minX + camHalfW;//摄像机最小X
        _maxX = maxX - camHalfW;//摄像机最大X
        _minY = minY + camHalfH;//摄像机最小Y
        _maxY = maxY - camHalfH;//摄像机最大Y
        _hasBounds = true;
    }

    /// <summary>
    /// 处理摄像机跟随等
    /// </summary>
    public void LateTick()
    {
        if (_camera == null) return;
        if (_followTarget == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _followTarget = p.transform;
        }
        if (_followTarget != null)
        {
            Vector3 targetPos = _followTarget.position;//目标位置
            targetPos.z = _camera.transform.position.z;//设置Z轴

            if (_hasBounds)
            {
                targetPos.x = Mathf.Clamp(targetPos.x, _minX, _maxX);//限制X轴
                targetPos.y = Mathf.Clamp(targetPos.y, _minY, _maxY);
            }
            _camera.transform.position = targetPos;
        }

        if (Time.unscaledTime < _shakeEndTime)
        {
            float x=Random.Range(-1f,1f)*_shakeIntensity;
            float y=Random.Range(-1f,1f)*_shakeIntensity;
            _camera.transform.position=new Vector3(x,y,0)+_camera.transform.position;
        }
    }
}


