using System.Net;

namespace TamesServer
{
    public class MessageEvent : EventArgs
    {
        public Message justReceived;
        public HttpListenerContext context;
        public MessageEvent(Message m, HttpListenerContext context)
        {
            justReceived = m;
            this.context = context;
        }
    }
    public class Message
    {
        public const int Transform = 1;
        public const int ID = 2;
        public const int Intro = 3;
        public const int Survey = 4;
        public const int Alter = 5;
        public const int Progress = 6;

        public const int Project = 15;
        public const int Refresh = 16;
        public const int Request = 21;

        public int index = -1;
        public int messageId = 0;
        public float[] floats = null;
        public string[] strings = null;
        public int[] ints = null;
    }
}