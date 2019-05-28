using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;

namespace TS.Task
{
    /// <summary>
    /// 超时任务运行器
    /// 专为超时时间到达后执行的任务，在超时时间到达之间，可以随时移除
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TimeoutTaskRunner<T>
    {        
        // 为添加的超时任务分配的 TaskId 任务标识序列
        private long _TaskIdSequence = 0;

        // 超时检测者，每秒扫描是否达到超时，超时则加入超时任务队列
        private System.Timers.Timer _TimeoutChecker = new System.Timers.Timer();

        // 以 TaskId(任务标识) 为 KEY 的任务清单字典
        private Dictionary<long, TimeoutTask<T>> _TaskIdDictionary = new Dictionary<long, TimeoutTask<T>>();
        // 以 ObjectId(任务相关对象标识) 为 KEY 的任务字典，因每个对象可以有多个超时任务，所以为列表
        private Dictionary<T, List<TimeoutTask<T>>> _TaskObjectKeyDictionary = new Dictionary<T, List<TimeoutTask<T>>>();
        // 用于同步操作上述两个清单字典，使得线程安全
        private object _DictionaryLocker = new object(); 

        // 已超时任务队列，由任务运行线程逐个执行
        private Queue<TimeoutTask<T>> _TaskRunQueue = new Queue<TimeoutTask<T>>();
        // 用来同步操作任务队列，使得线程安全（生产者，消费者模式）
        private object _RunLocker = new object();
       
        // 超时任务执行线程
        private Thread _TaskRunThread;
        // 用于同步操作任务队列的线程信号（生产者，消费者通知作用）
        private EventWaitHandle _WaitHandle = new AutoResetEvent(false);
        // 用于退出执行线程的一个标识
        private bool _Working = true;

        
        /// <summary>
        /// 创建实例时，开启：（1）超时检测者 （2）超时任务执行线程
        /// </summary>
        public TimeoutTaskRunner()
        {
            // （1）超时检测者
            _TimeoutChecker.Interval = 1000;
            _TimeoutChecker.Elapsed += new System.Timers.ElapsedEventHandler(CheckTimerTick);
            _TimeoutChecker.Start();

            // （2）超时任务执行线程
            _TaskRunThread = new Thread(new ThreadStart(TaskRunning));
            _TaskRunThread.Start();            
        } 

        /// <summary>
        /// 超时任务检测者
        /// 对于，时间已经超过了设定的超时时间的，加入超时任务执行队列
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckTimerTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            long secondTicks = DateTime.Now.Ticks / 10000000;
            // 遍历，把时间已到达超过超时时间的找出来
            lock (_DictionaryLocker)
            {
                foreach (var key in _TaskIdDictionary.Keys.ToList())
                {
                    var task = _TaskIdDictionary[key];
                    if (_TaskIdDictionary[key].ExecuteSecondTicks <= secondTicks)
                    {
                        // 加入超时任务执行队列，并移除清单
                        lock (_RunLocker)
                        {
                            _TaskRunQueue.Enqueue(task);
                            RemoveTimeoutTask(task.TaskId);
                        }
                        // 有生产，则通知执行线程（消费者）
                        _WaitHandle.Set();
                    }
                }
            }
        }

        /// <summary>
        /// 超时任务执行线程主体
        /// </summary>
        private void TaskRunning()
        {
            while (_Working)
            {
                TimeoutTask<T> task = null;
                lock (_RunLocker)   
                {
                    if (_TaskRunQueue.Count > 0)
                    {
                        task = _TaskRunQueue.Dequeue();  
                    }
                }
                // 存在超时任务执行其回调
                if (task != null)
                {
                    task.Callback(task.ObjectKey, task.Context);
                }
                else
                {
                    // 等待生产者通知
                    _WaitHandle.WaitOne();
                }
            }
        }

        /// <summary>
        /// 获取下一个任务标识
        /// </summary>
        /// <returns></returns>
        [MethodImplAttribute(MethodImplOptions.Synchronized)]
        private long GetNextTaskId()
        {
            _TaskIdSequence = (_TaskIdSequence + 1) % long.MaxValue;
            return _TaskIdSequence;
        } 

        #region 以下为向外开放的功能

        public long AddTimeoutTask(T objectKey, int timeoutSeconds, TimeoutCallback<T> callback)
        {
            return AddTimeoutTask(objectKey, timeoutSeconds, callback, null);
        }

        /// <summary>
        /// 指定对象标识，超时时长（秒为单位），超时执行回调，加入到超时检测字典中
        /// </summary>
        /// <param name="objectKey"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="callback"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public long AddTimeoutTask(T objectKey, int timeoutSeconds, TimeoutCallback<T> callback, String context)
        {
            TimeoutTask<T> task = new TimeoutTask<T>();
            task.ObjectKey = objectKey;
            task.TimeoutSeconds = timeoutSeconds;
            task.Callback = callback;
            long taskId = GetNextTaskId();
            task.TaskId = taskId;
            task.ExecuteSecondTicks = DateTime.Now.Ticks / 10000000 + timeoutSeconds;
            task.Context = context;

            lock (_DictionaryLocker)
            {
                // 以任务标识为主键的任务清单
                _TaskIdDictionary[taskId] = task;
                // 以对象标识为主键的任务清单
                if (_TaskObjectKeyDictionary.ContainsKey(objectKey))
                {
                    _TaskObjectKeyDictionary[objectKey].Add(task);
                }
                else
                {
                    List<TimeoutTask<T>> list = new List<TimeoutTask<T>>();
                    list.Add(task);
                    _TaskObjectKeyDictionary[objectKey] = list;
                }
            }
            return taskId;
        }

        /// <summary>
        /// 根据对象标识移除超时任务设置
        /// </summary>
        /// <param name="objectKey"></param>
        public void RemoveTimeoutTask(T objectKey)
        {
            lock (_DictionaryLocker)
            {
                if (_TaskObjectKeyDictionary.ContainsKey(objectKey))
                {
                    // 在任务标识为主键的清单中移除相应的该对象的多个超时任务
                    foreach (var task in _TaskObjectKeyDictionary[objectKey])
                    {
                        _TaskIdDictionary.Remove(task.TaskId);
                    }
                    _TaskObjectKeyDictionary[objectKey].Clear();
                }
            }
        }

        /// <summary>
        /// 根据任务标识移除超时任务设置
        /// </summary>
        /// <param name="taskId"></param>
        public void RemoveTimeoutTask(long taskId)
        {
            lock (_DictionaryLocker)
            {
                if (_TaskIdDictionary.ContainsKey(taskId))
                {
                    var task = _TaskIdDictionary[taskId];
                    _TaskIdDictionary.Remove(taskId);
                    // 在对象标识为主键的清单移除相应的超时任务
                    _TaskObjectKeyDictionary[task.ObjectKey].Remove(task);
                }
            }
        }

        /// <summary>
        /// 销毁时，退出线程执行体，释放内存
        /// </summary>
        public void Dispose()
        {
            Debug.WriteLine("timeout task runner is destoried.");
            _Working = false;
            _WaitHandle.Set();
            _TaskRunThread.Join(100);
            _WaitHandle.Close();
        }

        #endregion

    }
}
