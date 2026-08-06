using System;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;

public class PickupItemCommand : AbstractCommand
{
    public ItemConfig itemConfig;
    protected override void OnExcute()
    {
        ApplyEffect(itemConfig);
        this.GetModel<IItemModel>().Add(itemConfig);//添加拥有的Model
    }

    private void ApplyEffect(ItemConfig _config)
    {
        var combat = this.GetModel<ICombatModel>();//得到数据
        var player = this.GetModel<IEntityModel>();

        //根据道具类型应用效果
        switch (_config.itemType)
        {
            case ItemType.Heal:
                int HealAftert = (int)(MathF.Min(combat.CurrentHp.Value + _config.value, combat.MaxHp.Value));
                combat.CurrentHp.Value = HealAftert;
                break;

            case ItemType.AtkUp:
                combat.AttackPower.Value += _config.value;
                break;

            case ItemType.DefUp:
                combat.DefensePower.Value += _config.value;
                break;

            case ItemType.SpeedUp:
                player.MoveSpeed += _config.value;
                break;

            case ItemType.MaxHpUp:
                combat.MaxHp.Value += _config.value;
                break;
        }
    }
}