using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TS.Task
{ 
    public class QueueTask<T>
    {     
        // 对象标识
        public T ObjectKey { get; set; }
        
        // 回调方法
        public Action<T, String> Callback { get; set; }

        /// <summary>
        /// 用于保存一些回调时使用的上下文信息
        /// </summary>
        public String Context { get; set; }
    }
}
