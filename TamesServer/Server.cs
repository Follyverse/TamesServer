using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TamesServer
{

    public class Transform
    {
        public float[] position;
        public float[] rotation;
        public float time;
        public Transform(float[] fs)
        {
            time = fs[0];
            position = new float[] { fs[1], fs[2], fs[3] };
            rotation = new float[] { fs[4], fs[5], fs[6], fs[7] };
        }
        public new string ToString()
        {
            string s = time.ToString("0.0");
            for (int i = 0; i < position.Length; i++)
                s += ',' + position[i].ToString("0.00");
            for (int i = 0; i < rotation.Length; i++)
                s += ',' + rotation[i].ToString("0.0000");
            return s;
        }
    }
    public class User
    {
        public int id;
        public TamesProject project;
        public List<Transform> transform = new List<Transform>();
        public DateTime date;
        public int duration;
        public string profile = "";
        public string survey = "";
        public string alter = "";
        public string progress = "";
        public bool Expired()
        {
            TimeSpan ts = DateTime.Now - date;
            return ts.Minutes > project.maxDuration;
        }
    }
    public class Survey
    {
        public int info;
        public int frame;
        public int[] choices;
        public Survey(int[] data)
        {
            info = data[0];
            frame = data[1];
            choices = new int[data.Length - 2];
            for (int i = 0; i < choices.Length; i++) choices[i] = data[i + 2];
        }
    }

    public class Server
    {
        public static Server instance;
        public HttpListener listener;
        public int lastUnread = 0;
        public EventHandler received;
        public Server(string ip, string port)
        {
            received += Received;
            instance = this;
            Task.Run(() =>
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://" + ip + ":" + port + "/");
                listener.Start();
                Logs.Log.Add($"Listening began on ip:{ip} and port:{port}.");
                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    string reqIP = context.Request.RemoteEndPoint.ToString();
                    Logs.Log.Add($"A new request detected from {reqIP}");
                    HttpListenerRequest request = context.Request;
                    string text;
                    var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                    text = reader.ReadToEnd();
                    try
                    {
                        Message m = JsonSerializer.Deserialize<Message>(text);
                        if (m == null)
                            Logs.Log.Add("Error in recieved message.");
                        else
                        {
                            Logs.Log.Add("A typed message received.");
                            received.Invoke(this, new MessageEvent(m, context));
                        }
                    }
                    catch (Exception ex) { }
                }
            });
        }
        public void Stop()
        {
            listener.Stop();
        }
        bool ValidID(Message m, out int id)
        {
            id = -1;
            if (m != null)
                if (m.messageId == Message.Intro)
                    if (m.strings != null)
                        if (m.strings.Length >= 1)
                            return (id = Registry.instance.AddUser(m)) >= 0;
            return false;
        }
        private void Received(object? sender, EventArgs e)
        {
            MessageEvent me = (MessageEvent)e;
            Message m = me.justReceived;
            HttpListenerContext context = me.context;

            if (m.messageId == Message.Intro)
            {
                byte[] bytes;
                string s;
                if (ValidID(m, out int id))
                {
                    bytes = Encoding.ASCII.GetBytes(s = "{userId =" + id + "}");
                    Logs.Log.Add("Profile invalid");
                }
                else
                {
                    bytes = Encoding.ASCII.GetBytes(s = "{error =" + (-1) + "}");
                    Logs.Log.Add("Profile valid, user was added with ID: " + id);
                }
                context.Response.OutputStream.Write(bytes);
            }
            else
                switch (m.messageId)
                {
                    case Message.Survey:
                    case Message.Transform:
                    case Message.Alter:
                    case Message.Progress:
                        Logs.Log.Add($"User message received (id:{m.messageId}) from user #{m.index}");
                        Registry.instance.GetUserMessage(m);
                        break;
                    case Message.Project:
                    case Message.Request:
                        Logs.Log.Add($"Owner message received (id:{m.messageId})");
                        Registry.instance.GetOwnerMessage(m);
                        break;
                    case Message.Refresh:
                        Logs.Log.Add($"Admin message received (id:{m.messageId})");
                        Registry.instance.GetAdminMessage(m);
                        break;
                }
        }


    }
}
