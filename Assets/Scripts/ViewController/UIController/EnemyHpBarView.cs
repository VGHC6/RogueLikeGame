using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
//敌人的血条
public class EnemyHpBarView : MonoBehaviour, IController
{
    [SerializeField] Slider _slider;//血条
    private int _enmeyID;
    private RectTransform _rectTransform;
    private Vector2 _canvasSize;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Start()
    {
        _enmeyID = this.GetComponentInParent<EnemyView>().EnemyId;//获取敌人的id
        var model = this.GetModel<IEnemyModel>();//获取敌人数据
        Debug.Log("EnemyHpBarView Start" + model.TryGet(_enmeyID, out var data1));

        if (model.TryGet(_enmeyID, out var data))//从Id获得对应敌人的数据
        {
            Debug.Log("EnemyHpBarView Start" + data.MaxHp);
            _slider.maxValue = data.MaxHp;
            _slider.value = data.CurrentHp;
        }

        //修改方向
        var canvas = GetComponentInChildren<Canvas>();//获取画布
        _rectTransform=canvas.GetComponent<RectTransform>();//获取画布的RectTransform
        _canvasSize = _rectTransform.localScale;

        this.RegisterEvent<EnemyHpChangedEvent>(OnEnemyHpChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
        this.RegisterEvent<EnemyDeadEvent>(OnDead).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void LateUpdate()
    {
        _rectTransform.rotation=Quaternion.identity;//重置旋转
        Vector2 ps = transform.localScale;
        _rectTransform.localScale = new Vector2(_canvasSize.x * ps.x, _canvasSize.y * ps.y);//修改方向
    }

    void OnEnemyHpChanged(EnemyHpChangedEvent e)
    {
        if (e.EnemyId != _enmeyID) return;
        _slider.value = e.CurrentHp;
    }

    void OnDead(EnemyDeadEvent e)
    {
        if (e.EnemyId != _enmeyID) return;
        this.gameObject.SetActive(false);//隐藏血条
    }
}