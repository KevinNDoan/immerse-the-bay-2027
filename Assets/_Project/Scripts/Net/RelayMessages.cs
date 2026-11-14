using System;

namespace Zap.Net
{
    [Serializable]
    public class MessageHeader
    {
        public string t;
    }

    // Client to server messages
    [Serializable]
    public class EnterMessage
    {
        public string t = "enter";
        public string nodeId = "";
        public string topic = "";
        public string prompt = "";
        public string image = "";
    }

    [Serializable]
    public class SteerMessage
    {
        public string t = "steer";
        public string kind = "";
        public string text = "";
    }

    [Serializable]
    public class ExitMessage
    {
        public string t = "exit";
    }

    [Serializable]
    public class PingMessage
    {
        public string t = "ping";
    }

    // Server to client messages
    [Serializable]
    public class StatusMessage
    {
        public string t = "status";
        public string state = "";
        public string detail = "";
    }

    [Serializable]
    public class PromptMessage
    {
        public string t = "prompt";
        public string text = "";
        public uint appliesAtSeq;
    }

    [Serializable]
    public class PongMessage
    {
        public string t = "pong";
    }

    [Serializable]
    public class ErrorMessage
    {
        public string t = "error";
        public string code = "";
        public string detail = "";
    }

    // HTTP /health response
    [Serializable]
    public class HealthResponse
    {
        public bool ok;
        public string mode = "";
        public string state = "";
        public string model = "";
    }
}
