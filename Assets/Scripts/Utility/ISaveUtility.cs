using Unity.VisualScripting;
using UnityEngine;

//存储数据显示
public struct SaveSlotInfo
{
    public int _slotIndex;
    public string _slotName;
    public bool _isEmpty;
    public string _saveTime;
    public int _hp;
    public int _maxHp;
    public string _floorName;
}

//存储工具
public interface ISaveUtility : IUtility
{
    SaveSlotInfo GetSlotinfo(int soltIndex);//获取指定位置存储信息;
    void SaveToSolt(int soltIndex, SaveData data);//存储到指定位置
    SaveData LoadFromSlot(int soltIndex);//从指定位置加载
    void DeleteSlot(int soltIndex);//删除指定位置存储
}

public class SaveUtility : ISaveUtility
{
    private int _maxSlotCount = 3;//最大存档个数
    private string GetPath(int i)
    {
        return System.IO.Path.Combine(Application.persistentDataPath, $"Save{i}.json");//存储路径，Unity的持久化路径+SaveX.json
    }

    public IAchitecture _architecture;
    public IAchitecture GetArchitecture() => _architecture;
    public void SetArchitecture(IAchitecture architecture)
    {
        _architecture = architecture;
    }


    public SaveSlotInfo GetSlotinfo(int soltIndex)
    {
        var path = GetPath(soltIndex);//得到存储路径
        if (!System.IO.File.Exists(path))
        {
            return new SaveSlotInfo { _slotIndex = soltIndex, _isEmpty = true };
        }

        var json = System.IO.File.ReadAllText(path);//读取json文件
        var data = JsonUtility.FromJson<SaveData>(json);//反序列化
        return new SaveSlotInfo
        {
            _slotIndex = soltIndex,
            _isEmpty = false,
            _saveTime = data._detalTime,
            _hp = data._displayHp,
            _maxHp = data._displayMaxHp,
            _floorName = data._floorName
        };
    }

    /// <summary>
    /// 存储到指定位置
    /// </summary>
    /// <param name="soltIndex"></param>
    /// <param name="data"></param>
    public void SaveToSolt(int soltIndex, SaveData data)
    {
        var json= JsonUtility.ToJson(data,prettyPrint:true);//序列化,prettyPrinttrue时，json格式化，方便阅读
        System.IO.File.WriteAllText(GetPath(soltIndex), json);//写入json文件
    }

    /// <summary>
    /// 从指定位置加载
    /// </summary>
    /// <param name="soltIndex"></param>
    /// <returns></returns>
    public SaveData LoadFromSlot(int soltIndex)
    {
        var path = GetPath(soltIndex);
        if (!System.IO.File.Exists(path)) return null;
        return JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(path));//反序列化
    }

    /// <summary>
    /// 删除指定位置存储
    /// </summary>
    /// <param name="soltIndex"></param>
    public void DeleteSlot(int soltIndex)
    {
        var path = GetPath(soltIndex);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
}