using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
//敌人的血条
public class EnemyHpBarView : MonoBehaviour, IController
{
    [SerializeField] Slider _slider;//血条
    private int _enmeyID;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;
    void Awake()
    {
        _enmeyID=this.GetComponentInParent<EnemyView>().EnemyId;//获取敌人的id
    }

    void OnEnable()
    {
        var model = this.GetModel<IEnemyModel>();//获取敌人数据
        if(model.TryGet(_enmeyID,out var data))//从Id获得对应敌人的数据
        {
            _slider.maxValue = data.MaxHp;
            _slider.value = data.CurrentHp;
        }
    }
}