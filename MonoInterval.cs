using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


namespace MyTest.MonoTimer
{
    /// <summary>
    /// 添加延时误差用的类
    /// </summary>
    class DeviationBox
    {
        public float original = 0;
        public int time = 1;
        private const float deviation = 0.00001f;
        public float Deviation => deviation;

        public float GetDeviation(float original)
        {
            if (this.original == original)
            {
                time++;
                return original + deviation * time;
            }
            else
            {
                this.original = original;
                time = 1;
                return original + deviation;
            }
        }
    }

    /// <summary>
    /// 间隔任务的Token
    /// </summary>
    public class IntervalTaskToken
    {
        public bool canceled { get; private set; } = false;
        public void Cancel() => canceled = true;
        public bool running = false;
    }

    /// <summary>
    /// 间隔任务
    /// </summary>
    public class IntervalTask : IComparable<IntervalTask>
    {
        public Action IntervalAction = () => { };

        public Action cancelCallback;

        public float timeSpan;
        public float finalTime;

        public IntervalTaskToken token = new IntervalTaskToken();

        /// <summary>
        /// 构建一个间隔任务
        /// </summary>
        /// <param name="timeSpan">时间差</param>
        /// <param name="IntervalAction">回调</param>
        /// <param name="cancelCallback">取消后回调</param>
        /// <param name="token">自定义Token</param>
        public IntervalTask(float timeSpan, Action IntervalAction, Action cancelCallback = null, IntervalTaskToken token = null)
        {
            this.timeSpan = timeSpan;
            this.IntervalAction += IntervalAction;

            this.cancelCallback += () => this.IntervalAction -= IntervalAction;
            this.cancelCallback += cancelCallback;

            if (token != null)
                this.token = token;
        }

        public int CompareTo(IntervalTask other)
        {
            return this.finalTime.CompareTo(other.finalTime);
        }

        //内部初始化
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


    public class IntervalSystem
    {
        /// <summary>
        /// 全局间隔系统
        /// </summary>
        /// <returns></returns>
        public static IntervalSystem GlobalInterval = new IntervalSystem();
        private SortedSet<IntervalTask> timeScaleSet = new SortedSet<IntervalTask>();
        private SortedSet<IntervalTask> ignoreTimeScaleSet = new SortedSet<IntervalTask>();
        private IntervalCore intervalCore;
        private DeviationBox deviationBox = new DeviationBox();

        /// <summary>
        /// 是否有受TimeScale影响的间隔任务在执行
        /// </summary>
        /// <value></value>
        public bool timeScaleEmpty { get; private set; } = true;

        /// <summary>
        /// 是否有不受TimeScale影响的间隔任务在执行
        /// </summary>
        /// <value></value>
        public bool ignoreTimeScaleEmpty { get; private set; } = true;

        /// <summary>
        /// 当前任务总数
        /// </summary>
        public int TaskCount => timeScaleSet.Count + ignoreTimeScaleSet.Count;


        public IntervalSystem()
        {
            GameObject intervalCoreObj = new GameObject(nameof(IntervalCore));
            intervalCore = intervalCoreObj.AddComponent<IntervalCore>();
            intervalCore.intervalSystem = this;
        }

        /// <summary>
        /// 增加一个间隔任务
        /// </summary>
        /// <param name="task">间隔任务</param>
        /// <param name="ignoreTimeScale">是否忽略TimeScale</param>
        public void AddIntervalTask(IntervalTask task, bool ignoreTimeScale = false)
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
        /// 增加一个间隔任务, 并返回其Token
        /// </summary>
        /// <param name="task">间隔任务</param>
        /// <param name="ignoreTimeScale">是否忽略TimeScale</param>
        /// <returns>间隔任务的Token</returns>
        public IntervalTaskToken AddIntervalTaskWithToken(IntervalTask task, bool ignoreTimeScale = false)
        {
            AddIntervalTask(task, ignoreTimeScale);
            return task.token;
        }

        private bool TryGetTimeScaleTask(out IntervalTask task)
        {
            if (timeScaleEmpty)
            {
                task = null;
                return false;
            }

            task = timeScaleSet.First();
            return true;
        }

        private bool TryGetIgnoreTimeScaleTask(out IntervalTask task)
        {
            if (ignoreTimeScaleEmpty)
            {
                task = null;
                return false;
            }

            task = ignoreTimeScaleSet.First();
            return true;
        }

        public class IntervalCore : MonoBehaviour
        {
            [HideInInspector]
            public IntervalSystem intervalSystem;

            private bool hasTimeScaleTask = false;
            private bool hasIgnoreTimeScaleTask = false;

            private IntervalTask timeScaleTask;
            private IntervalTask ignoreTimeScaleTask;

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
                if (intervalSystem.timeScaleEmpty) return;

                if (!hasTimeScaleTask)
                {
                    hasTimeScaleTask = intervalSystem.TryGetTimeScaleTask(out timeScaleTask);
                    if (hasTimeScaleTask)
                        timeScaleTask.token.running = true;
                }

                if (Time.time > timeScaleTask.finalTime)
                {
                    if (timeScaleTask.token.canceled)
                    {
                        timeScaleTask.cancelCallback.Invoke();

                        intervalSystem.timeScaleSet.Remove(timeScaleTask);
                        if (intervalSystem.timeScaleSet.Count == 0)
                        {
                            intervalSystem.timeScaleEmpty = true;
                        }
                        hasTimeScaleTask = false;
                        return;

                    }

                    timeScaleTask.IntervalAction.Invoke();
                    timeScaleTask.InitTask(false);
                    intervalSystem.timeScaleSet.Remove(timeScaleTask);
                    intervalSystem.timeScaleSet.Add(timeScaleTask);
                    timeScaleTask.token.running = false;
                    hasTimeScaleTask = false;
                }
            }

            private void ResloveIgnoreTimeScaleTask()
            {
                if (intervalSystem.ignoreTimeScaleEmpty) return;

                if (!hasIgnoreTimeScaleTask)
                {
                    hasIgnoreTimeScaleTask = intervalSystem.TryGetIgnoreTimeScaleTask(out ignoreTimeScaleTask);
                    if (hasIgnoreTimeScaleTask)
                        ignoreTimeScaleTask.token.running = true;
                }

                if (Time.time > ignoreTimeScaleTask.finalTime)
                {
                    if (ignoreTimeScaleTask.token.canceled)
                    {
                        ignoreTimeScaleTask.cancelCallback.Invoke();

                        intervalSystem.ignoreTimeScaleSet.Remove(ignoreTimeScaleTask);
                        if (intervalSystem.ignoreTimeScaleSet.Count == 0)
                        {
                            intervalSystem.ignoreTimeScaleEmpty = true;
                        }
                        hasIgnoreTimeScaleTask = false;
                        return;
                    }

                    ignoreTimeScaleTask.IntervalAction.Invoke();
                    ignoreTimeScaleTask.InitTask(false);
                    intervalSystem.ignoreTimeScaleSet.Remove(ignoreTimeScaleTask);
                    intervalSystem.ignoreTimeScaleSet.Add(ignoreTimeScaleTask);
                    ignoreTimeScaleTask.token.running = false;
                    hasIgnoreTimeScaleTask = false;
                }
            }

        }

    }









}