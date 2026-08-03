using JetBrains.Annotations;
using UnityEngine;
public class AddOtherHitParticel : AbstractCommand//对敌人造成伤害
{
    protected override void OnExcute()
    {
        this.GetUtility<IHitstopUtility>().Trigger(0.08f);
        this.GetUtility<ICameraUtility>().Shake(0.1f, 0.2f);
    }
}
