using UnityEngine;

public class DoorView : MonoBehaviour, IController
{
    [SerializeField] private int _doorId;
    [SerializeField] public int _roomIndex;

    private Collider2D _collider;
    private SpriteRenderer _spriteRenderer;
    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _spriteRenderer = null;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            if (sr.sprite != null)
            {
                _spriteRenderer = sr;
                break;
            }
        }
    }

    public void Init(int doorId, int roomIndex)
    {
        _doorId = doorId;
        _roomIndex = roomIndex;
        SetOpen(true);

        // 门精灵高 1.25(2格)，纵向相邻会重叠；按 y 反序排序，让靠下的门显示在上层
        if (_spriteRenderer != null)
            _spriteRenderer.sortingOrder = 10000 - Mathf.RoundToInt(transform.position.y * 100f);

        this.RegisterEvent<DoorStateChangedEvent>(OnDoorStateChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnDoorStateChanged(DoorStateChangedEvent e)
    {
        if (e.DoorId != _doorId) return;
        SetOpen(e.IsOpen);
    }

    void SetOpen(bool isOpen)
    {
        if (_collider != null) _collider.enabled = !isOpen;
        if (_spriteRenderer != null) _spriteRenderer.enabled = !isOpen;
    }
}
