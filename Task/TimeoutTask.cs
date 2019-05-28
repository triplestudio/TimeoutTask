using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TS.Task
{
    /// <summary>
    /// 超时回调的委托
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="objectKey"></param>
    /// <param name="context"></param>
    public delegate void TimeoutCallback<T>(T objectKey, String context);

    /// <summary>
    /// 超时任务信息
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TimeoutTask<T>
    {
        // 任务标识（由超时任务运行器自动分配）
        public long TaskId { get; set; }
        // 对象标识
        public T ObjectKey { get; set; }
        // 超时秒数
        public int TimeoutSeconds { get; set; }
        /// <summary>
        /// 以秒为单位的 Tick 值，由超时任务运行器根据当前时间加上超时秒数计算设置
        /// DateTime.Ticks 是以 10ns（10纳秒） 为单位
        /// 将其除以 10 单位为 ws（微秒），再除以 1000 为 ms（毫秒），再除以 1000 为 s（秒）
        /// 累计为 DateTime.Ticks / 10000000
        /// </summary>
        public long ExecuteSecondTicks { get; set; }
        // 超时回调方法
        public TimeoutCallback<T> Callback { get; set; }
        /// <summary>
        /// 用于保存一些回调时使用的上下文信息
        /// </summary>
        public String Context { get; set; }
    }
}
