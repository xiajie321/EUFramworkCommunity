using System;
using System.Collections.Generic;
using EUFarmworker.Core.MVC.Abstract;
using EUFarmworker.Core.MVC.CoreTool;
using EUFarmworker.Core.MVC.Interface;
using UnityEngine;

namespace EUFarmworker.Core.MVC.Example.Script
{
    /// <summary>
    /// 测试用事件结构体
    /// </summary>
    public struct TestEvent
    {
        
    }
    
    /// <summary>
    /// 测试核心功能的 MonoBehaviour
    /// </summary>
    public class TestCore:MonoBehaviour,IController
    {
        private void Awake()
        {
            // 初始化架构
            EUCore.SetArchitecture(TestAbsArchitectureBase.Instance);
        }

        private void Start()
        {
            // 开始性能测试
            RunPerformanceTest();
        }

        private void RunPerformanceTest()
        {
            const int testCount = 100000;
            const int listenerCount = 100; // 模拟高压力多播情况：200个监听者
            Debug.Log($"<color=cyan>--- 开始性能对比测试 (执行次数: {testCount}) ---</color>");

            // --- 1. 单播测试 (Unicast) ---
            {
                var euSystem = new TypeEventSystem();
                var qfSystem = new QFramework_TypeEventSystem();
                euSystem.Register<TestEvent>(m => { });
                qfSystem.Register<TestEvent>(m => { });

                // 预热
                for (int i = 0; i < 100; i++)
                {
                    euSystem.Send(new TestEvent());
                    qfSystem.Send(new TestEvent());
                }

                Debug.Log("<color=white># 单播性能测试 (1个监听者):</color>");
                
                var euSw = new System.Diagnostics.Stopwatch();
                euSw.Start();
                for (int i = 0; i < testCount; i++) euSystem.Send(new TestEvent());
                euSw.Stop();
                long euTime = euSw.ElapsedMilliseconds;

                var qfSw = new System.Diagnostics.Stopwatch();
                qfSw.Start();
                for (int i = 0; i < testCount; i++) qfSystem.Send(new TestEvent());
                qfSw.Stop();
                long qfTime = qfSw.ElapsedMilliseconds;

                Debug.Log($"EUFarmworker (Unicast): {euTime} ms");
                Debug.Log($"QFramework (Sim Unicast): {qfTime} ms");
                if (qfTime > 0)
                {
                    Debug.Log($"<color=yellow>单播性能提升: {(float)(qfTime - euTime) / qfTime * 100:F2}%</color>");
                }
            }

            Debug.Log("\n");

            // --- 2. 多播测试 (Multicast) ---
            {
                var euSystem = new TypeEventSystem();
                var qfSystem = new QFramework_TypeEventSystem();
                for (int i = 0; i < listenerCount; i++)
                {
                    euSystem.Register<TestEvent>(m => { });
                    qfSystem.Register<TestEvent>(m => { });
                }

                // 预热
                for (int i = 0; i < 100; i++)
                {
                    euSystem.Send(new TestEvent());
                    qfSystem.Send(new TestEvent());
                }

                Debug.Log($"<color=white># 多播性能测试 ({listenerCount}个监听者):</color>");

                var euSw = new System.Diagnostics.Stopwatch();
                euSw.Start();
                for (int i = 0; i < testCount; i++) euSystem.Send(new TestEvent());
                euSw.Stop();
                long euTime = euSw.ElapsedMilliseconds;

                var qfSw = new System.Diagnostics.Stopwatch();
                qfSw.Start();
                for (int i = 0; i < testCount; i++) qfSystem.Send(new TestEvent());
                qfSw.Stop();
                long qfTime = qfSw.ElapsedMilliseconds;

                Debug.Log($"EUFarmworker (Multicast): {euTime} ms");
                Debug.Log($"QFramework (Sim Multicast): {qfTime} ms");
                if (qfTime > 0)
                {
                    Debug.Log($"<color=yellow>多播性能提升: {(float)(qfTime - euTime) / qfTime * 100:F2}%</color>");
                }
            }

            Debug.Log("<color=cyan>--- 性能对比测试结束 ---</color>");
        }

        public void Run2(TestEvent testEvent)
        {
            Debug.Log("aaaaaa");
        }
        public void Run(TestEvent testEvent)
        {
            // Debug.Log("TestEvent"); // 屏蔽掉，避免干扰性能测试
        }
    }

    /// <summary>
    /// 模拟 QFramework 的 TypeEventSystem 实现 (基于 Dictionary)
    /// </summary>
    public class QFramework_TypeEventSystem
    {
        private interface IEasyEvent { }
        private class EasyEvent<T> : IEasyEvent 
        { 
            public Action<T> OnEvent = e => { }; 
        }
        
        private readonly System.Collections.Generic.Dictionary<Type, IEasyEvent> mEvents 
            = new System.Collections.Generic.Dictionary<Type, IEasyEvent>();

        public void Register<T>(Action<T> onEvent)
        {
            var type = typeof(T);
            if (!mEvents.TryGetValue(type, out var e))
            {
                e = new EasyEvent<T>();
                mEvents.Add(type, e);
            }
            ((EasyEvent<T>)e).OnEvent += onEvent;
        }

        public void UnRegister<T>(Action<T> onEvent)
        {
            var type = typeof(T);
            if (mEvents.TryGetValue(type, out var e))
            {
                ((EasyEvent<T>)e).OnEvent -= onEvent;
            }
        }

        public void Send<T>(T e)
        {
            var type = typeof(T);
            if (mEvents.TryGetValue(type, out var eventObj))
            {
                ((EasyEvent<T>)eventObj).OnEvent(e);
            }
        }
    }

    /// <summary>
    /// 测试用的架构实现
    /// </summary>
    public class TestAbsArchitectureBase : AbsArchitectureBase<TestAbsArchitectureBase>
    {
        protected override void Init()
        {
            // 注册模块
            RegisterModel(new TestModelBase());
            RegisterSystem(new TestSystemBase());
            RegisterUtility(new TestUtilityBase());
        }
    }

    /// <summary>
    /// 测试用的数据模型
    /// </summary>
    public class TestModelBase : AbsModelBase
    {
        public override void Init()
        {
            Debug.Log("TestModel");   
            // this.SendEvent(new TestEvent());//不建议在结构体内使用(因为会产生装箱)
            // this.GetUtility<TestUtility>();//不建议在结构体内使用(因为会产生装箱)
        }
    }

    /// <summary>
    /// 测试用的系统
    /// </summary>
    public class TestSystemBase : AbsSystemBase
    {
        public override void Init()
        {
            Debug.Log("TestSystem");
            // this.SendEvent(new TestEvent());//不建议在结构体内使用(因为会产生装箱)
            // this.GetModel<TestModel>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetUtility<TestUtility>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetSystem<TestSystem>();//不建议在结构体内使用(因为会产生装箱)
        }
    }
    
    /// <summary>
    /// 测试用的工具
    /// </summary>
    public class TestUtilityBase:AbsUtilityBase//工具本身仅起到辅助作用
    {
        public override void Init()
        {
            Debug.Log("TestUtility");
        }
    }

    public struct TestCommand : ICommand//无返回值的命令
    {
        public int lsValue;
        public void Execute()
        {
            Debug.Log(lsValue);
            //this.SendCommand<TestCommandReturnInt,int>(new TestCommandReturnInt());//不建议在结构体内使用(因为会产生装箱)
            // this.SendQuery<TestQuery,int>(new TestQuery());//不建议在结构体内使用(因为会产生装箱)
            // this.SendEvent<TestEvent>(new TestEvent());//不建议在结构体内使用(因为会产生装箱)
            // this.GetModel<TestModel>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetUtility<TestUtility>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetSystem<TestSystem>();//不建议在结构体内使用(因为会产生装箱)
            
            //this.SendCommand(new TestCommand());//无返回值默认通过泛型确定避免装箱问题
            // this.SendCommand<TestCommand,TestCommandReturnInt,int>(new TestCommandReturnInt());//通过泛型确定避免装箱问题
            // this.SendQuery<TestCommand,TestQuery,int>(new TestQuery());//通过泛型确定避免装箱问题
            // this.SendEvent<TestCommand,TestEvent>(new TestEvent());//通过泛型确定类型避免装箱问题
            // this.GetModel<TestCommand,TestModel>();//通过泛型确定类型避免装箱问题
            // this.GetUtility<TestCommand,TestUtility>();//通过泛型确定类型避免装箱问题
            // this.GetSystem<TestCommand,TestSystem>();//通过泛型确定类型避免装箱问题
        }
    }

    public struct TestCommandReturnInt : ICommand<int>//有返回值的命令
    {
        public int Execute()
        {
            // this.SendCommand<TestCommandReturnInt,int>(new  TestCommandReturnInt());//不建议在结构体内使用(因为会产生装箱)
            // this.SendQuery<TestQuery,int>(new TestQuery());//不建议在结构体内使用(因为会产生装箱)
            // this.SendEvent<TestEvent>(new TestEvent());//不建议在结构体内使用(因为会产生装箱)
            // this.GetModel<TestModel>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetUtility<TestUtility>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetSystem<TestSystem>();//不建议在结构体内使用(因为会产生装箱)
            //
            // this.SendCommand(new TestCommand());//无返回值默认通过泛型确定避免装箱问题
            // this.SendCommand<TestCommandReturnInt,TestCommandReturnInt,int>(new TestCommandReturnInt());//通过泛型确定避免装箱问题
            // this.SendQuery<TestCommandReturnInt,TestQuery,int>(new TestQuery());//通过泛型确定避免装箱问题
            // this.SendEvent<TestCommandReturnInt,TestEvent>(new TestEvent());//通过泛型确定类型避免装箱问题
            // this.GetModel<TestCommandReturnInt,TestModel>();//通过泛型确定类型避免装箱问题
            // this.GetUtility<TestCommandReturnInt,TestUtility>();//通过泛型确定类型避免装箱问题
            // this.GetSystem<TestCommandReturnInt,TestSystem>();//通过泛型确定类型避免装箱问题
            return 1;
        }
    }

    public struct TestQuery : IQuery<int>
    {
        public int Execute()
        {
            // this.SendQuery<TestQuery,int>(new TestQuery());//不建议在结构体内使用(因为会产生装箱)
            // this.GetModel<TestModel>();//不建议在结构体内使用(因为会产生装箱)
            // this.GetUtility<TestUtility>();//不建议在结构体内使用(因为会产生装箱)
            //
            // this.SendQuery<TestQuery,TestQuery,int>(new TestQuery());//通过泛型确定类型避免装箱问题
            // this.GetModel<TestQuery,TestModel>();//通过泛型确定类型避免装箱问题
            // this.GetUtility<TestQuery,TestUtility>();//通过泛型确定类型避免装箱问题
            return 1;
        }
    }
}
