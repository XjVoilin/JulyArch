using System;

namespace JulyArch
{
    public interface IArchLogger
    {
        void Log(string msg);
        void LogWarning(string msg);
        void LogError(string msg);
        void LogException(Exception ex);
    }
}
