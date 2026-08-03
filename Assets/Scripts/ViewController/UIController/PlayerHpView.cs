using UnityEngine;
using UnityEngine.UI;
//玩家血条

public class PlayerHpView : MonoBehaviour, IController
{
    [SerializeField] private GameObject _heartPrefab;//血条
    private Image[] _image;

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        var combat = this.GetModel<ICombatModel>();//获取战斗模块

        BuildHearts(combat.MaxHp.Value);//生成血量

        combat.CurrentHp.RegisterOnValueChanged(_ => RefreshHearts())
            .UnRegisterWhenGameObjectDestroyed(gameObject);

        combat.MaxHp.RegisterOnValueChanged(max =>
        {
            foreach (var h in _image) Destroy(h.gameObject);
            BuildHearts(max);
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    /// <summary>
    /// 血量刷新
    /// </summary>
    private void RefreshHearts()
    {
        int hp = this.GetModel<ICombatModel>().CurrentHp.Value;
        for (int i = 0; i < _image.Length; i++)
        {
            _image[i].enabled = i < hp;
        }
    }

    /// <summary>
    /// 生成血量
    /// </summary>
    void BuildHearts(int count)
    {
        _image = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(_heartPrefab, transform);
            _image[i] = go.GetComponent<Image>();
        }
    }

    public void Init() { }
}