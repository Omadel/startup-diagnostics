using System;
using System.Collections.Generic;

namespace Javienne.StartupDiagnostics
{
    /// <summary>
    ///     The parts of the JSON output of <c>xcrun devicectl … --json-output</c> that the measure reads. The fields are
    ///     named after the keys of that JSON, as <see cref="UnityEngine.JsonUtility" /> needs; other keys are ignored.
    /// </summary>
    internal static class DevicectlJson
    {
        [Serializable]
        internal sealed class Output
        {
            public Result result;
            public Error error;
        }

        [Serializable]
        internal sealed class Result
        {
            public List<Device> devices = new();
            public bool passcodeRequired;
            public Process process;
        }

        [Serializable]
        internal sealed class Device
        {
            public string identifier;
            public ConnectionProperties connectionProperties;
            public DeviceProperties deviceProperties;
            public HardwareProperties hardwareProperties;
        }

        [Serializable]
        internal sealed class ConnectionProperties
        {
            public string tunnelState;
        }

        [Serializable]
        internal sealed class DeviceProperties
        {
            public string name;
            public string osVersionNumber;
        }

        [Serializable]
        internal sealed class HardwareProperties
        {
            public string udid;
            public string marketingName;
            public string productType;
            public string platform;
            public string reality;
        }

        [Serializable]
        internal sealed class Process
        {
            public int processIdentifier = -1;
        }

        [Serializable]
        internal sealed class Error
        {
            public int code;
            public string domain;
            public UserInfo userInfo;
        }

        [Serializable]
        internal sealed class UserInfo
        {
            public StringValue NSLocalizedDescription;
        }

        [Serializable]
        internal sealed class StringValue
        {
            public string @string;
        }
    }
}