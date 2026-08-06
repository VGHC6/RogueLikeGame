
//给道具携带的脚本
using UnityEngine;

public class IItemPickup : MonoBehaviour, IController
{
    private ItemConfig _itemConfig;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Init(ItemConfig itemConfig)
    {
        _itemConfig = itemConfig;
        var sr = GetComponent<SpriteRenderer>();//获取组件,精灵图组件 
        if (sr != null) sr.sprite = itemConfig.icon;//设置精灵图
    }

    //拾取效果
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "Player")
        {
            this.SendCommand(new PickupItemCommand { itemConfig = _itemConfig });//发送事件
            Destroy(gameObject);//销毁自身
        }
    }
}