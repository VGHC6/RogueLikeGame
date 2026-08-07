using System.Collections.Generic;
public interface IItemModel : IModel
{
    int Count { get; }
    BindableProperty<int> CountProperty { get; }
    IReadOnlyList<ItemConfig> Items { get; }
    void Add(ItemConfig config);
    void Clear();
}

public class ItemModel : AbstractModel, IItemModel
{
    private List<ItemConfig> _items = new();

    public int Count => _items.Count;
    public IReadOnlyList<ItemConfig> Items => _items;

    public BindableProperty<int> CountProperty { get; } = new BindableProperty<int>();
    protected override void OnInit() { }

    public void Add(ItemConfig config)
    {
        _items.Add(config);
        CountProperty.Value = _items.Count;//´¥·¢
    }

    public void Clear()
    {
        _items.Clear();
        CountProperty.Value = 0;//´¥·¢
    }
}