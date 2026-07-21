using System;
using System.Collections.Generic;
using UnityEngine;
//事件中心
public interface ITypeEventSystem
{
    /// 发送事件
    void Send<T>() where T : new();
    void Send<T>(T e);

    /// 注册事件
    IUnRegister Register<T>(Action<T> OnEvent);

    // 注销事件
    void UnRegister<T>(Action<T> OnEvent);
}



/// <summary>
/// 注销事件接口
/// </summary>
public interface IUnRegister
{
    void UnRegister();
}




/// <summary>
/// 事件系统注销
/// </summary>
/// <typeparam name="T"></typeparam>
public class TypeEventSystemUnRegister<T> : IUnRegister
{
    public ITypeEventSystem TypeEventSystem;
    public Action<T> _onEvent;
    //取消注册接口实现
    public void UnRegister()
    {
        TypeEventSystem.UnRegister(_onEvent);
        TypeEventSystem = null;
        _onEvent = null;
    }
}



/// <summary>
/// 扩展注销，可以挂载在物体上，在物体销毁时自动注销
/// </summary>
public class UnRegisterOnDestroyTrigger : MonoBehaviour
{
    private HashSet<IUnRegister> _unRegisters = new HashSet<IUnRegister>();

    public void AddUnRegister(IUnRegister unRegister)
    {
        _unRegisters.Add(unRegister);
    }

    public void OnDestroy()
    {
        foreach (var unRegister in _unRegisters)
        {
            unRegister.UnRegister();
        }
        _unRegisters.Clear();
    }
}



/// <summary>
/// 注销扩展,this IUnRegister unRegister是可以做到链式调用
/// </summary>
public static class UnRegisterExtension
{
    public static void UnRegisterWhenGameObjectDestroyed(this IUnRegister unRegister,GameObject gameObject)
    {
       var trigger = gameObject.GetComponent<UnRegisterOnDestroyTrigger>();//获取注销触发器
        if (!trigger)
        {
            trigger = gameObject.AddComponent<UnRegisterOnDestroyTrigger>();//不存在就添加
        }
        trigger.AddUnRegister(unRegister);//添加注销
    }
}



/// <summary>
/// 事件系统
/// </summary>
/// <typeparam name="T"></typeparam>
public class TypeEventSystem<T> : ITypeEventSystem
{
    interface IRegistrations { }//内置注册接口

    public class Registrations<T> : IRegistrations
    {
        public Action<T> OnEvent = obj => { };
    }

    private Dictionary<Type, IRegistrations> _registrations = new Dictionary<Type, IRegistrations>();//注册字典

    public void Send<T>() where T : new()
    {
        var e = new T();
        Send<T>(e);
    }

    public void Send<T>(T e)
    {
        var type = typeof(T);
        IRegistrations registrations;
        if (_registrations.TryGetValue(type, out registrations))
        {
            (registrations as Registrations<T>)?.OnEvent.Invoke(e);//调用注册的事件
        }
    }

    public IUnRegister Register<T>(Action<T> OnEvent)
    {
        var type = typeof(T);
        IRegistrations registrations;

        if (_registrations.TryGetValue(type, out registrations))
        {

        }
        else
        {
            //不存在的话
            registrations = new Registrations<T>();
            _registrations.Add(type, registrations);
        }

        (registrations as Registrations<T>).OnEvent += OnEvent;//注册事件 

        return new TypeEventSystemUnRegister<T>()
        {
            _onEvent = OnEvent,
            TypeEventSystem = this
        };
    }

    public void UnRegister<T>(Action<T> OnEvent)
    {
        var type = typeof(T);
        IRegistrations registrations;

        if (_registrations.TryGetValue(type, out registrations))
        {
            (registrations as Registrations<T>).OnEvent -= OnEvent;//注销事件
        }
    }
}


