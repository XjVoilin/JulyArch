using System;
using UnityEngine;

namespace JulyArch
{
    internal sealed class DefaultArchLogger : IArchLogger
    {
        public void Log(string msg) => Debug.Log(msg);
        public void LogWarning(string msg) => Debug.LogWarning(msg);
        public void LogError(string msg) => Debug.LogError(msg);
        public void LogException(Exception ex) => Debug.LogException(ex);
    }
}
