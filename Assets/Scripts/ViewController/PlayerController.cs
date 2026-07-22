using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//调度 InputUtility 采集输入，调度 FSMSystem 驱动状态机。
public class PlayerController : MonoBehaviour, IController
{
    private IInputUtility _inputUtility;//采集输入,位于工具层
    private IFSMSystem _fsmSystem;//驱动状态机，位于逻辑层
    public IAchitecture GetArchitecture()
    {
        return RogueLikeGame.Interface;//获取架构
    }

    public void Update()
    {
        _inputUtility.Update();//采集输入,业务逻辑
        _fsmSystem.Update(Time.deltaTime);//驱动状态机,业务逻辑
    }

}
