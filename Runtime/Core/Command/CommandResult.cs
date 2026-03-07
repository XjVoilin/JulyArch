namespace JulyArch
{
    /// <summary>
    /// 命令执行结果
    /// </summary>
    public readonly struct CommandResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>消息（失败原因或成功提示）</summary>
        public string Message { get; }

        private CommandResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static CommandResult Success(string message = null)
        {
            return new CommandResult(true, message);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static CommandResult Fail(string message)
        {
            return new CommandResult(false, message);
        }
    }
}
