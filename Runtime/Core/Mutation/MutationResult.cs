namespace JulyArch
{
    /// <summary>
    /// Mutation 执行结果
    /// </summary>
    public readonly struct MutationResult
    {
        public bool IsSuccess { get; }
        public int ErrorCode { get; }
        public string Message { get; }

        private MutationResult(bool isSuccess, int errorCode, string message)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            Message = message;
        }

        public static MutationResult Success(string message = null)
        {
            return new MutationResult(true, 0, message);
        }

        public static MutationResult Fail(string message, int errorCode = 0)
        {
            return new MutationResult(false, errorCode, message);
        }
    }
}
