using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotItem : MonoBehaviour, IController
{
    [SerializeField] TMP_Text _labelText;
    [SerializeField] TMP_Text _infoText;
    [SerializeField] Button _button;

    public int _slotIndex { get;private set; }
    public bool _isEmpty { get;private set; }

    public IAchitecture GetArchitecture() => RogueLikeGame.Interface;

    public void Init(SaveSlotInfo saveSlotInfo,System.Action<int> Onclick,bool isSaveMode)
    {
        _slotIndex= saveSlotInfo._slotIndex;
        _isEmpty = saveSlotInfo._isEmpty;

        _labelText.text = $"Save {saveSlotInfo._slotIndex + 1}";
        if (saveSlotInfo._isEmpty)
        {
            _infoText.text = "Empty";
            _button.interactable = isSaveMode;
        }
        else
        {
            _infoText.text = $"{saveSlotInfo._saveTime}  HP:{saveSlotInfo._hp}/{saveSlotInfo._maxHp}  {saveSlotInfo._floorName}";
            _button.interactable = true;
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(()=>Onclick(_slotIndex));
    }

}
