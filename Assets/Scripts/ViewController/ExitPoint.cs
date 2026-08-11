

using UnityEngine;

public class ExitPoint : MonoBehaviour, IController
{
    public IAchitecture GetArchitecture()=>RogueLikeGame.Interface;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            this.SendCommand(new AdvanceFloorCommand());
        }
    }
}