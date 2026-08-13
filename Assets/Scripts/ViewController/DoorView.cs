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
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        var model = this.GetModel<IDoorModel>();
        model.RegisterDoor(new DoorData
        {
            DoorId = _doorId,
            RoomIndex = _roomIndex,
            IsOpen = true
        });

        _doorId = model.Doors[model.Doors.Count - 1].DoorId;//¼ÇÂ¼ID

        this.RegisterEvent<DoorStateChangedEvent>(OnDoorStateChanged).UnRegisterWhenGameObjectDestroyed(gameObject);
    }

    void OnDoorStateChanged(DoorStateChangedEvent e)
    {
        if (e.DoorId != _doorId) return;

        _collider.enabled = !e.IsOpen;
        _spriteRenderer.enabled = !e.IsOpen;
    }

    void OnDestroy()
    {
        var model = this.GetModel<IDoorModel>();
        model?.ReMoveDoor(_doorId);
    }
}