using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace MyTest.MonoTimer
{
    /// <summary>
    /// 链接DelayTask状态的Token
    /// </summary>
    public class DealyTaskToken
    {
        public bool canceled { get; private set; } = false;

        /// <summary>
        /// 取消DelayTask的执行
        /// </summary>
        public void Cancel() => canceled = true;
    }

    /// <summary>
    /// 延时任务
    /// </summary>
    public class DealyTask : IComparable<DealyTask>
    {
        public Action delayCallback = () => { };
        public Action cancelCallback;

        [Range(0, Single.MaxValue)]
        public float timeSpan;
        public float finalTime;

        public DealyTaskToken token = new DealyTaskToken();

        /// <summary>
        /// 构造一个延时任务
        /// </summary>
        /// <param name="timeSpan">等待事件</param>
        /// <param name="delayCallback">回调</param>
        /// <param name="cancelCallback">取消后的回调</param>
        /// <param name="token">自定义Token</param>
        public DealyTask(float timeSpan, Action delayCallback, Action cancelCallback = null, DealyTaskToken token = null)
        {
            this.timeSpan = timeSpan;
            this.delayCallback += delayCallback;

            this.cancelCallback += () => this.delayCallback -= delayCallback;
            this.cancelCallback += cancelCallback;

            if (token != null)
                this.token = token;
        }

        public int CompareTo(DealyTask other)
        {
            return this.finalTime.CompareTo(other.finalTime);
        }

        //内部初始化任务时间
        public void InitTask(bool ignoreTimeScale)
        {
            if (!ignoreTimeScale)
            {
                this.finalTime = Time.time + timeSpan;
            }
            else
            {
                this.finalTime = Time.realtimeSinceStartup + timeSpan;
            }

        }
    }

    /// <summary>
    /// 延时系统
    /// </summary>
    public class DelaySystem
    {   
        /// <summary>
        /// 全局延时系统
        /// </summary>
        /// <returns></returns>
        public static DelaySystem GlobalDelay = new DelaySystem();

        private SortedSet<DealyTask> timeScaleSet = new SortedSet<DealyTask>();
        private SortedSet<DealyTask> ignoreTimeScaleSet = new SortedSet<DealyTask>();

        private DeviationBox deviationBox = new DeviationBox();

        /// <summary>
        /// 是否有受TimeScale影响的任务在等待或执行
        /// </summary>
        /// <value></value>
        public bool timeScaleEmpty { get; private set; } = true;

        /// <summary>
        /// 是否有不受TimeScale影响的任务在等待或执行
        /// </summary>
        /// <value></value>
        public bool ignoreTimeScaleEmpty { get; private set; } = true;

        /// <summary>
        /// 当前任务总数
        /// </summary>
        public int TaskCount => timeScaleSet.Count + ignoreTimeScaleSet.Count;

        public DelaySystem()
        {
            GameObject delayCoreObj = new GameObject(nameof(DelayCore));
            var delayCore = delayCoreObj.AddComponent<DelayCore>();
            delayCore.delaySystem = this;
        }

        /// <summary>
        /// 添加一个延时任务
        /// </summary>
        /// <param name="task">延时任务</param>
        /// <param name="ignoreTimeScale">是否忽略TimeScale</param>
        public void AddDelayTask(DealyTask task, bool ignoreTimeScale = false)
        {
            task.InitTask(ignoreTimeScale);

            if (!ignoreTimeScale)
            {
                if (timeScaleSet.Contains(task))
                {
                    task.finalTime = deviationBox.GetDeviation(task.finalTime);
                }

                timeScaleSet.Add(task);
                timeScaleEmpty = false;
            }
            else
            {
                if (ignoreTimeScaleSet.Contains(task))
                {
                    task.finalTime = deviationBox.GetDeviation(task.finalTime);
                }
                ignoreTimeScaleSet.Add(task);
                ignoreTimeScaleEmpty = false;

            }

        }

        /// <summary>
        /// 添加一个延时任务, 并返回其Token
        /// </summary>
        /// <param name="task">延时任务</param>
        /// <param name="ignoreTimeScale">是否忽略TimeScale</param>
        /// <returns>延时任务的Token</returns>
        public DealyTaskToken AddDelayTaskWithToken(DealyTask task, bool ignoreTimeScale = false)
        {
            AddDelayTask(task, ignoreTimeScale);
            return task.token;
        }

        private bool TryGetTimeScaleTask(out DealyTask task)
        {
            if (timeScaleEmpty)
            {
                task = null;
                return false;
            }

            task = timeScaleSet.First();
            return true;
        }

        private bool TryGetIgnoreTimeScaleTask(out DealyTask task)
        {
            if (ignoreTimeScaleEmpty)
            {
                task = null;
                return false;
            }

            task = ignoreTimeScaleSet.First();
            return true;
        }

        public class DelayCore : MonoBehaviour
        {
            [HideInInspector]
            public DelaySystem delaySystem;

            private bool hasTimeScaleTask = false;
            private bool hasIgnoreTimeScaleTask = false;

            private DealyTask timeScaleTask;
            private DealyTask ignoreTimeScaleTask;

            void Awake()
            {
                DontDestroyOnLoad(this.gameObject);
            }

            void Update()
            {
                ResloveTimeScaleTask();
                ResloveIgnoreTimeScaleTask();
            }

            private void ResloveTimeScaleTask()
            {
                if (delaySystem.timeScaleEmpty) return;

                if (!hasTimeScaleTask)
                {
                    delaySystem.TryGetTimeScaleTask(out timeScaleTask);
                }

                if (Time.time > timeScaleTask.finalTime)
                {
                    if (timeScaleTask.token.canceled)
                    {
                        timeScaleTask.cancelCallback.Invoke();
                    }

                    timeScaleTask.delayCallback.Invoke();
                    delaySystem.timeScaleSet.Remove(timeScaleTask);

                    if (delaySystem.timeScaleSet.Count == 0)
                    {
                        delaySystem.timeScaleEmpty = true;
                    }
                }
            }

            private void ResloveIgnoreTimeScaleTask()
            {
                if (delaySystem.ignoreTimeScaleEmpty) return;

                if (!hasIgnoreTimeScaleTask)
                {
                    delaySystem.TryGetIgnoreTimeScaleTask(out ignoreTimeScaleTask);
                }

                if (Time.realtimeSinceStartup > ignoreTimeScaleTask.finalTime)
                {
                    if (ignoreTimeScaleTask.token.canceled)
                    {
                        ignoreTimeScaleTask.cancelCallback.Invoke();
                    }

                    ignoreTimeScaleTask.delayCallback.Invoke();
                    delaySystem.ignoreTimeScaleSet.Remove(ignoreTimeScaleTask);

                    if (delaySystem.ignoreTimeScaleSet.Count == 0)
                    {
                        delaySystem.ignoreTimeScaleEmpty = true;
                    }
                }
            }
        }

    }

}