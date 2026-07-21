using System;
using System.Collections.Generic;


public interface IAchitecture
{
    //工具接口
    T GetUtility<T>() where T : class, IUtility;
    void RegisterUtility<T>(T instance) where T : IUtility;



    //数据层接口
    T GetModel<T>() where T : class, IModel;
    void RegisterModel<T>(T instance) where T : IModel;



    //系统层接口
    T GetSystem<T>() where T : class, ISystem;
    void RegisterSystem<T>(T instance) where T : ISystem;



    //命令接口
    void SendCommand<T>() where T : ICommand, new();//发送命令
    void SendCommand<T>(T command) where T : ICommand;//发送命令带参



    //事件接口
    void SendEvent<T>() where T : new();//发送事件
    void SendEvent<T>(T e);
    IUnRegister RegisterEvent<T>(Action<T> OnEvent);//注册事件
    void UnRegisterEvent<T>(Action<T> OnEvent);//取消注册事件
}

public abstract class Architecture<T> : IAchitecture where T : Architecture<T>, new()//子类把自己的类型作为泛型参数传给父类,泛型 T 必须是 Architecture<T> 的子类
{
    private static T _achitecture;//静态变量,用于存储当前实例

    public static Action<T> OnRegisterPatch = architecture => { };//注册模块的回调

    private bool isInited = false;//是否初始化完成
    private List<IModel> _models = new List<IModel>();//用于初始化数据层
    private List<ISystem> _systems = new List<ISystem>();//用于初始化系统层

    //接口
    public static IAchitecture Interface
    {
        get
        {
            if (_achitecture == null)
            {
                MakeArchitecture();
            }
            return _achitecture;
        }
    }



    //确保Container类只有一个实例
    static void MakeArchitecture()
    {
        if (_achitecture == null)
        {
            _achitecture = new T();//实现单例
            _achitecture.Init();

            OnRegisterPatch?.Invoke(_achitecture);//注册模块的回调
        }

        ///下面是对model和system的的初始化
        foreach (var system in _achitecture._systems)
        {
            system.Init();
        }
        _achitecture._systems.Clear();//初始化后清空

        foreach (var model in _achitecture._models)
        {
            model.Init();
        }
        _achitecture._models.Clear();//初始化后清空
        _achitecture.isInited = true;//初始化完成
    }

    //Countainer实例
    private IOCContainer _container = new IOCContainer();


    //预留注册模块接口
    protected abstract void Init();


    /// <summary>
    /// 工具接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T GetUtility<T>() where T : class, IUtility
    {
        return _container.Get<T>();
    }

    public void RegisterUtility<T>(T instance) where T : IUtility
    {
        _achitecture._container.Register<T>(instance);
    }


    /// <summary>
    /// 系统接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public T GetSystem<T>() where T : class, ISystem
    {
        return _container.Get<T>();
    }

    public void RegisterSystem<T>(T instance) where T : ISystem
    {
        instance.SetArchitecture(this);
        _container.Register<T>(instance);//注册数据层
        if (isInited)
        {
            instance.Init();//如果注册了就初始化
        }
        else
        {
            _systems.Add(instance);//如果没有注册就先存起来
        }
    }

    /// <summary>
    /// 数据接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public T GetModel<T>() where T : class, IModel
    {
        return _container.Get<T>();
    }

    public void RegisterModel<T>(T instance) where T : IModel
    {
        instance.SetArchitecture(this);
        _container.Register<T>(instance);//注册数据层

        if (isInited)
        {
            instance.Init();//如果注册了就初始化
        }
        else
        {
            _models.Add(instance);//如果没有注册就先存起来
        }
    }


    /// <summary>
    /// 命令接口
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    /// <exception cref="NotImplementedException"></exception>
    public void SendCommand<T>() where T : ICommand, new()
    {
        var command = new T();
        command.SetArchitecture(this);//这个this是为了得到这个架构
        command.Excute();
    }

    public void SendCommand<T>(T command) where T : ICommand
    {
        command.SetArchitecture(this);
        command.Excute();
    }



    /// <summary>
    /// 事件接口
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <exception></exception>
    public ITypeEventSystem _typeEventSystem = new TypeEventSystem<T>();
    public void SendEvent<T>() where T : new()
    {
        _typeEventSystem.Send<T>();
    }

    public void SendEvent<T>(T e)
    {
        _typeEventSystem.Send<T>(e);
    }

    public IUnRegister RegisterEvent<T>(Action<T> OnEvent)
    {
        return _typeEventSystem.Register<T>(OnEvent);
    }

    public void UnRegisterEvent<T>(Action<T> OnEvent)
    {
        _typeEventSystem.UnRegister<T>(OnEvent);
    }
}