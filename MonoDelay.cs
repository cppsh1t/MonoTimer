using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

namespace MyTest.MonoDelay
{
    public class DealyTaskToken
    {
        public bool canceled { get; private set; } = false;
        public bool finished { get; private set; } = false;

        public void Cancel() => canceled = true;
        public void Finish() => finished = true;
    }


    public class DealyTask : IComparable<DealyTask>
    {
        public Action delayCallback = () => { };
        public Action cancelCallback;

        public float timeSpan;
        public float finalTime;

        public DealyTaskToken token = new DealyTaskToken();

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

        public static (DealyTask, DealyTaskToken) CreateWithToken(float timeSpan, Action delayCallback, Action cancelCallback = null)
        {
            DealyTask task = new DealyTask(timeSpan, delayCallback, cancelCallback);
            return (task, task.token);
        }

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


    public class DelaySystem
    {
        public static DelaySystem GlobalDelay = new DelaySystem();

        private SortedSet<DealyTask> timeScaleSet = new SortedSet<DealyTask>();
        private SortedSet<DealyTask> ignoreTimeScaleSet = new SortedSet<DealyTask>();

        public bool timeScaleEmpty { get; private set; } = true;
        public bool ignoreTimeScaleEmpty { get; private set; } = true;

        public int TaskCount => timeScaleSet.Count + ignoreTimeScaleSet.Count;

        public DelaySystem()
        {
            GameObject delayCoreObj = new GameObject(nameof(DelayCore));
            var delayCore = delayCoreObj.AddComponent<DelayCore>();
            delayCore.delaySystem = this;
        }

        public void AddDelayTask(DealyTask task, bool ignoreTimeScale = false)
        {
            task.InitTask(ignoreTimeScale);

            if (!ignoreTimeScale)
            {
                bool contain = timeScaleSet.TryGetValue(task, out var insideTask);

                if (contain)
                {
                    insideTask.delayCallback += task.delayCallback;
                }
                else
                {
                    timeScaleSet.Add(task);
                    timeScaleEmpty = false;
                }
            }
            else
            {
                bool contain = ignoreTimeScaleSet.TryGetValue(task, out var insideTask);

                if (contain)
                {
                    insideTask.delayCallback += task.delayCallback;
                }
                else
                {
                    ignoreTimeScaleSet.Add(task);
                    ignoreTimeScaleEmpty = false;
                }
            }

        }

        public bool TryGetTimeScaleTask(out DealyTask task)
        {
            if (timeScaleEmpty)
            {
                task = null;
                return false;
            }

            task = timeScaleSet.First();
            return true;
        }

        public bool TryGetIgnoreTimeScaleTask(out DealyTask task)
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