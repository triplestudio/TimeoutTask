using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TS.Diagnose
{
    /// <summary>
    /// 耗时计数器
    /// </summary>
    public class ElapsedTimer
    {
        // 创建时默认为创建时间，这样不调用 Start 也可以
        private DateTime _StartTime = DateTime.Now;

        // 计时开始
        public void Start()
        {
            _StartTime = DateTime.Now;
        }

        // 计时结束（输出毫秒）
        public long Over()
        {
            var timespan = DateTime.Now - _StartTime;
            return Convert.ToInt32(timespan.TotalMilliseconds);
        }

        // 取得间隔
        public TimeSpan Span()
        {
            return DateTime.Now - _StartTime;
        }

        /// <summary>
        /// 静态方法，运行指定函数，返回耗时结果
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public static TimeSpan Elapsed(Action action)
        {
            ElapsedTimer tcc = new ElapsedTimer();
            action();
            return tcc.Span();
        }
    }
}
