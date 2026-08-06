using UnityEngine;
using UnityEngine.UI;

public class PlayerItemView : MonoBehaviour, IController
{
    [SerializeField] private GameObject _itemPrefab;//图标
    private Image[] _image;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        var model = this.GetModel<IItemModel>();

        model.CountProperty.RegisterOnValueChanged(count =>
        {
            Refresh(model, count);
        }).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void Refresh(IItemModel model, int count)
    {
        if (_image != null)
        {
            foreach (var item in _image)
            {
                if (item != null) Destroy(item.gameObject);
            }
        }
        _image = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(_itemPrefab, transform);
            var img = go.GetComponent<Image>();
            if (img != null) img.sprite = model.Items[i].icon;
            _image[i] = img;
        }
    }
}