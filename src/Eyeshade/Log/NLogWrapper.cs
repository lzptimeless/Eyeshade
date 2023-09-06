using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.Log
{
    public class NLogWrapper : ILogWrapper
    {
        private readonly NLog.Logger _logger;

        /// <summary>
        /// 创建日志记录器
        /// </summary>
        /// <param name="logPath">日志文件路径</param>
        public NLogWrapper(string logPath)
        {
            string logFileName = Path.GetFileNameWithoutExtension(logPath);
            var config = new NLog.Config.LoggingConfiguration();
            string format = @"${date:format=yy-MM-dd HH\:mm\:ss.fff}|${level:uppercase=true:truncate=3}|${event-properties:item=file}.${event-properties:item=func}# ${message}${onexception:inner=${newline}${exception:format=tostring}}";
            // 定义写入文件
            var logFile = new NLog.Targets.FileTarget("LogFile")
            {
                FileName = logPath,
                Layout = format,
                KeepFileOpen = true,
                ConcurrentWrites = false,
                ArchiveAboveSize = 1024 * 1024 * 20, // Log file max size is 20M
                ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Rolling,
                MaxArchiveFiles = 2
            };
            // 设置异步写入
            var asyncLogFile = new NLog.Targets.Wrappers.AsyncTargetWrapper(logFile)
            {
                OverflowAction = NLog.Targets.Wrappers.AsyncTargetWrapperOverflowAction.Block
            };
            // 设置外部自定义日志打印目标
            var target = new NLog.Targets.MethodCallTarget("LogAction", OnLogAction);
            // Rules for mapping loggers to targets
            config.AddRuleForAllLevels(asyncLogFile);
            config.AddRuleForAllLevels(target);

            // Apply config
            NLog.LogManager.Configuration = config;

            // 获取 logger
            _logger = NLog.LogManager.GetLogger("Default");
        }

        public event EventHandler<LogWrapperWriteArgs>? OnWrite;

        #region public methods
        public void Debug(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Debug(CreateEventInfo(NLog.LogLevel.Debug, null, msg, file, func, line));
        }

        public void Debug(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Debug(CreateEventInfo(NLog.LogLevel.Debug, ex, msg, file, func, line));
        }

        public void Error(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Error(CreateEventInfo(NLog.LogLevel.Error, null, msg, file, func, line));
        }

        public void Error(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Error(CreateEventInfo(NLog.LogLevel.Error, ex, msg, file, func, line));
        }

        public void Info(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Info(CreateEventInfo(NLog.LogLevel.Info, null, msg, file, func, line));
        }

        public void Info(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Info(CreateEventInfo(NLog.LogLevel.Info, ex, msg, file, func, line));
        }

        public void Warn(string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Warn(CreateEventInfo(NLog.LogLevel.Warn, null, msg, file, func, line));
        }

        public void Warn(Exception? ex, string msg, [CallerFilePath] string file = "", [CallerMemberName] string func = "", [CallerLineNumber] int line = 0)
        {
            _logger.Warn(CreateEventInfo(NLog.LogLevel.Warn, ex, msg, file, func, line));
        }

        public void WriteItems(IEnumerable<LogItem> items)
        {
            foreach (var item in items)
            {
                if (item.Level == LogWrapperLevels.Info) Info(item.Exception, item.Message, item.File, item.Func, item.Line);
                else if (item.Level == LogWrapperLevels.Warning) Warn(item.Exception, item.Message, item.File, item.Func, item.Line);
                else if (item.Level == LogWrapperLevels.Error) Error(item.Exception, item.Message, item.File, item.Func, item.Line);
                else if (item.Level == LogWrapperLevels.Debug) Debug(item.Exception, item.Message, item.File, item.Func, item.Line);
                else Info(item.Exception, item.Message, item.File, item.Func, item.Line);
            }
        }

        public void FlushAndShutdown()
        {
            NLog.LogManager.Shutdown();
        }

        public void Flush()
        {
            NLog.LogManager.Flush();
        }
        #endregion

        #region private methods
        private void OnLogAction(NLog.LogEventInfo logEventInfo, object[] args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToLongTimeString());
            sb.Append('|');
            sb.Append(string.Format("{0,-48}", $"{logEventInfo.Properties["file"]}.{logEventInfo.Properties["func"]}"));
            sb.Append("# ");
            sb.Append(logEventInfo.Message);
            if (logEventInfo.Exception != null)
            {
                sb.AppendLine();
                sb.Append(logEventInfo.Exception.ToString());
            }

            string msg = sb.ToString();
            OnWrite?.Invoke(this, new LogWrapperWriteArgs(msg));
        }

        private NLog.LogEventInfo CreateEventInfo(NLog.LogLevel level, Exception? ex, string msg, string file, string func, int line)
        {
            var logInfo = new NLog.LogEventInfo(level, null, null, msg, null, ex);
            logInfo.Properties["line"] = line;
            logInfo.Properties["func"] = func;
            logInfo.Properties["file"] = Path.GetFileNameWithoutExtension(file);
            return logInfo;
        }
        #endregion
    }
}
