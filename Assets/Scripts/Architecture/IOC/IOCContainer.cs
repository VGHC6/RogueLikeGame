using System;
using System.Collections.Generic;

/// <summary>
/// ioc容器用来管理依赖注入,依赖注入:一个类不自己创建它依赖的对象，而是由外部"注入"给它。
/// </summary>
public class IOCContainer
{
    //实例, object 是 C#中所有类型的基类，意味着字典可以存储任意类型的实例
    public Dictionary<Type, object> _iocContainer = new Dictionary<Type, object>();
    
    /// <summary>
    /// 注册一个实例
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instance"></param>
    public void Register<T>(T instance)
    {
        var key = typeof(T);
        if (!_iocContainer.ContainsKey(key))
        {
            _iocContainer.Add(key, instance);
        }
        else
        {
            _iocContainer[key] = instance;
        }
    }

    //获取
    public T Get<T>() where T : class
    {
        var key = typeof(T);
        if (_iocContainer.ContainsKey(key))
        {
            return _iocContainer[key] as T;
        }
        return null;
    }
}
