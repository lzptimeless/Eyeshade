using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.Log
{
    /// <summary>
    /// 日志接口
    /// </summary>
    public interface ILogWrapper
    {
        /// <summary>
        /// 普通日志信息
        /// </summary>
        /// <param name="msg">日志信息</param>
        void Info(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 普通日志信息
        /// </summary>
        /// <param name="ex">如果需要同时打印异常信息，则传入</param>
        /// <param name="msg">日志信息</param>
        void Info(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// Debug 信息,
        /// </summary>
        /// <param name="msg">Debug 信息</param>
        void Debug(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// Debug 信息
        /// </summary>
        /// <param name="ex">如果需要同时打印异常信息，则传入</param>
        /// <param name="msg">Debug 信息</param>
        void Debug(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 警告（需要注意的）信息
        /// </summary>
        /// <param name="msg">信息内容</param>
        void Warn(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 警告（需要注意的）信息
        /// </summary>
        /// <param name="ex">如果需要同时打印异常信息，则传入</param>
        /// <param name="msg">信息内容</param>
        void Warn(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 打印错误信息
        /// </summary>
        /// <param name="msg">错误信息</param>
        void Error(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 打印错误信息
        /// </summary>
        /// <param name="ex">如果需要同时打印异常信息，则传入</param>
        /// <param name="msg">错误信息</param>
        void Error(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0);
        /// <summary>
        /// 打印LogItem列表
        /// </summary>
        /// <param name="items">LogItem列表</param>
        void WriteItems(IEnumerable<LogItem> items);
        /// <summary>
        /// 确保日志保存到硬盘
        /// </summary>
        void Flush();
    }

    public class LogWrapperWriteArgs : EventArgs
    {
        public LogWrapperWriteArgs(string msg)
        {
            Message = msg;
        }

        public string Message { get; private set; }
    }

    public class LogItem
    {
        public LogItem(LogWrapperLevels level, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
            : this(level, null, msg, file, func, line)
        { }

        public LogItem(LogWrapperLevels level, Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            Level = level;
            Exception = ex;
            Message = msg;
            File = file;
            Func = func;
            Line = line;
        }

        public LogWrapperLevels Level { get; set; }
        public string Message { get; set; }
        public Exception? Exception { get; set; }
        public string File { get; set; }
        public string Func { get; set; }
        public int Line { get; set; }
    }

    public enum LogWrapperLevels
    {
        Info,
        Warning,
        Error,
        Debug
    }
}
