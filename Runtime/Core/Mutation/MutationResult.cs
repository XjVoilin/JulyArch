namespace JulyArch
{
    /// <summary>
    /// Mutation 执行结果
    /// </summary>
    public readonly struct MutationResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>消息（失败原因或成功提示）</summary>
        public string Message { get; }

        private MutationResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static MutationResult Success(string message = null)
        {
            return new MutationResult(true, message);
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static MutationResult Fail(string message)
        {
            return new MutationResult(false, message);
        }
    }
}
