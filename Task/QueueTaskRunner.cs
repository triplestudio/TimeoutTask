using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace TS.Task
{
    public class QueueTaskRunner<T>
    {  
        // 任务队列，由任务运行线程逐个执行
        private Queue<QueueTask<T>> _TaskRunQueue = new Queue<QueueTask<T>>();
        // 用来同步操作任务队列，使得线程安全（生产者，消费者模式）
        private object _RunLocker = new object();
       
        // 超时任务执行线程
        private Thread _TaskRunThread;
        // 用于同步操作任务队列的线程信号（生产者，消费者通知作用）
        private EventWaitHandle _WaitHandle = new AutoResetEvent(false);
        // 用于退出执行线程的一个标识
        private bool _Working = true;
        
        /// <summary>
        /// 创建实例时，开启：任务执行线程
        /// </summary>
        public QueueTaskRunner()
        { 
            _TaskRunThread = new Thread(new ThreadStart(TaskRunning));
            _TaskRunThread.Start();            
        }        

        /// <summary>
        /// 任务执行线程主体
        /// </summary>
        private void TaskRunning()
        {
            while (_Working)
            {
                QueueTask<T> task = null;
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

        #region 以下为向外开放的功能

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="objectKey"></param>
        /// <param name="callback"></param>
        public void AddTask(T objectKey, Action<T, String> callback)
        {
            AddTask(objectKey, callback, null);
        }

        /// <summary>
        /// 添加任务
        /// </summary>
        /// <param name="objectKey"></param>
        /// <param name="callback"></param>
        /// <param name="context"></param>
        public void AddTask(T objectKey, Action<T, String> callback, String context)
        {
            QueueTask<T> task = new QueueTask<T>();
            task.ObjectKey = objectKey; 
            task.Callback = callback; 
            task.Context = context;

            // 加入任务执行队列
            lock (_RunLocker)
            {
                _TaskRunQueue.Enqueue(task); 
            }
            // 有生产，则通知执行线程（消费者）
            _WaitHandle.Set();
        } 

        /// <summary>
        /// 销毁时，退出线程执行体，释放内存
        /// </summary>
        public void Dispose()
        { 
            _Working = false;
            _WaitHandle.Set();
            _TaskRunThread.Join();
            _WaitHandle.Close();
        }

        #endregion

    }
}
