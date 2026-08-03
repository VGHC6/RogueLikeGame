using UnityEngine;

public class EnemyManagerDriver : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        this.GetUtility<IHitstopUtility>().Init(this);//调用HitstopUtility的Init方法
        this.GetUtility<ICameraUtility>().Init(this);//调用CameraUtility的Init方法
    }

    void FixedUpdate()
    {
        this.GetSystem<IEnemyManagerSystem>().Update(Time.fixedDeltaTime);//调用EnemyManagerSystem的Update方法,unscaledDeltaTime避免卡肉暂停住时间
    }
}
