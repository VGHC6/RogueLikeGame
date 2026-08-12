public class PlayerEnteredRoomCommand : AbstractCommand
{
    public int RoomIndex;
    protected override void OnExcute()
    {
        this.SendEvent(new PlayerEnteredRoomEvent { RoomIndex = RoomIndex });
    }
}