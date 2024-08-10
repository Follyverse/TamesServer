using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TamesServer
{
    public class Registry
    {
        public class UserAddress
        {
            public ushort registrar;
            public ushort project;
            public ushort user;
            public UserAddress(ushort r, ushort p, ushort u)
            {
                registrar = r;
                project = p;
                user = u;
            }
        }
        public const int MaxToken = 20;
        public static Registry instance;

        public string name = "";
        public string email = "";
        public string password = "";
        public string port;
        public string ip;
        public UserAddress[] userAddress = new UserAddress[1 << 20];
        public List<Registrar> registrars = new List<Registrar>();
        public int lastUser = -1;
        public Registry()
        {
            instance = this;
            Refresh(true);
        }
        int[] FindToken(string token, byte condition)
        {
            for (int i = 0; i < registrars.Count; i++)
                for (int j = 0; j < MaxToken; j++)
                    if (registrars[i].token[j] == token)
                        if ((condition & registrars[i].taken[j]) > 0)
                            return new int[] { i, j };
            return null;
        }
        public void Refresh(bool start)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(path + "registry.txt"))
            {
                string[] lines = File.ReadAllLines(path + "registry.txt");
                List<Registrar> regs = new List<Registrar>();
                name = lines[0];
                email = lines[1];
                password = lines[2];
                ip = lines[3];
                port = lines[4];
                for (int i = 6; i < lines.Length; i += MaxToken + 4)
                    if (lines.Length >= i + MaxToken + 4)
                    {
                        Registrar reg = new Registrar(lines[i], lines[i + 1]);
                        for (int j = 0; j < MaxToken; j++)
                        {
                            reg.token[i] = lines[i + j + 4].Substring(1);
                        }
                        regs.Add(reg);
                    }
                lock (this)
                {
                    registrars.Clear();
                    registrars.AddRange(regs);
                }
            }
            if (File.Exists(path + "projects.txt"))
            {
                string[] lines = File.ReadAllLines(path + "projects.txt");
                lock (this)
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string t = lines[i];
                        if (t.Length < 10)
                        {
                            if (int.TryParse(t, out int value))
                                lastUser = value;
                        }
                        else
                        {
                            int[] I = FindToken(t, Registrar.Either);
                            if (I != null)
                                registrars[I[0]].taken[I[1]] = Registrar.Taken;
                        }
                    }
            }

        }

        public void AddProject(string name, string token, int d)
        {
            lock (this)
            {
                int[] I = FindToken(token, Registrar.Vacant);

                if (I == null) return;

                TamesProject project = new TamesProject() { name = name, registrar = I[0], index = I[1], token = token, maxDuration = d };
                registrars[I[0]].projects[I[1]] = project;
                registrars[I[0]].taken[I[1]] = Registrar.Taken;
                string[] lines = new string[] { I[0] + "", I[1] + "", name };
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string projPath = path + "projects";
                if (!Directory.Exists(projPath))
                    Directory.CreateDirectory(projPath);
                File.WriteAllLines(projPath + '/' + token + ".tpj", lines);
                File.AppendAllLines(path + "projects.txt", new string[] { token });
                Logs.Log.Add($"Project registry succesful: email is being sent to {registrars[I[0]].email}");
                OwnerSide.instance.SendProjectRegistry(registrars[I[0]].email, name);
            }
        }
        public int AddUser(Message msg)
        {
            lock (this)
            {
                if (msg == null) return -1;
                if (msg.strings == null) return -1;
                if (msg.strings.Length != 6) return -1;

                int[] I = FindToken(msg.strings[0], Registrar.Taken);
                if (I == null) return -1;

                lastUser++;


                User user = new User()
                {
                    project = registrars[I[0]].projects[I[1]],
                    id = lastUser,
                    date = DateTime.Now
                };
                for (int i = 0; i < 4; i++)
                    if (user.project.headers[i].Length == 0) user.project.headers[i] = msg.strings[i + 1];
                user.profile = msg.strings[5];

                string path = AppDomain.CurrentDomain.BaseDirectory;
                File.AppendAllLines(path + "projects.txt", new string[] { lastUser + "" });
                registrars[I[0]].projects[I[1]].users.Add(user);
                return lastUser;
            }
        }
        int backup = 0;
        List<TamesProject> toEmail = new List<TamesProject>();
        public void BackUp()
        {
            backup++;
            lock (this)
            {
                if (backup % 10 == 0)
                {
                    OwnerSide.instance.Reset();
                    foreach (Registrar r in registrars)
                        foreach (TamesProject p in r.projects)
                            OwnerSide.instance.ForStorage(p);

                    foreach (TamesProject tp in toEmail)
                        OwnerSide.instance.ForEmail(tp, registrars[tp.registrar].email);
                    toEmail.Clear();

                    OwnerSide.instance.Apply();
                }
                else
                {
                    foreach (Registrar r in registrars)
                        foreach (TamesProject p in r.projects)
                            for (int i = p.users.Count - 1; i >= 0; i--)
                                if (p.users[i].Expired())
                                {
                                    userAddress[p.users[i].id] = null;
                                    p.Archive(p.users[i]);
                                }
                }
            }
        }
        void ProcessRequest(string token)
        {
            int[] I = FindToken(token, Registrar.Taken);
            if (I == null)
            {
                Logs.Log.Add($"Request denied because {token} does not exist.");
                return;
            }
            Logs.Log.Add($"Result request was queued for project {token}.");
            toEmail.Add(registrars[I[0]].projects[I[1]]);
        }
        public void GetAdminMessage(Message msg)
        {
            if (msg == null) return;
            lock (this)
            {
                switch (msg.messageId)
                {
                    case Message.Refresh:
                        Logs.Log.Add($"Refresh requested by the admin.");
                        Refresh(false);
                        break;
                }
            }
        }
        public void GetOwnerMessage(Message msg)
        {
            switch (msg.messageId)
            {
                case Message.Project:
                    if (msg.strings != null)
                        if (msg.strings.Length > 1)
                            if (msg.ints != null)
                                if (msg.ints.Length > 1)
                                {
                                    string name = msg.strings[0];
                                    string token = msg.strings[1];
                                    Logs.Log.Add($"Owner project registry: {name}, {token}");
                                    AddProject(name, token, msg.ints[0]);
                                }
                    break;
                case Message.Request:
                    if (msg.strings != null)
                        if (msg.strings.Length > 0)
                            if (msg.ints != null)
                                if (msg.ints.Length > 0)
                                    lock (this)
                                    {
                                        Logs.Log.Add($"Owner requested results of project {msg.strings[0]}");
                                        ProcessRequest(msg.strings[0]);
                                    }
                    break;

            }
        }
        public void GetUserMessage(Message msg)
        {
            if (msg == null) return;
            lock (this)
            {
                switch (msg.messageId)
                {
                    case Message.Survey:
                        if (msg.strings != null)
                            if (msg.strings.Length > 0)
                                if (msg.index >= 0 && msg.index < 1 << 20)
                                    lock (this)
                                        if (userAddress[msg.index] != null)
                                        {
                                            UserAddress u = userAddress[msg.index];
                                            Logs.Log.Add($"Survey input for user{msg.index} of project {u.project}: {msg.strings[0]}");
                                            registrars[u.registrar].projects[u.project].users[u.user].survey = msg.strings[0];
                                        }
                        break;
                    case Message.Progress:
                        if (msg.strings != null)
                            if (msg.strings.Length > 0)
                                if (msg.index >= 0 && msg.index < 1 << 20)
                                    lock (this)
                                        if (userAddress[msg.index] != null)
                                        {
                                            UserAddress u = userAddress[msg.index];
                                            Logs.Log.Add($"Progress input for user{msg.index} of project {u.project}: {msg.strings[0]}");
                                            registrars[u.registrar].projects[u.project].users[u.user].progress = msg.strings[0];
                                        }
                        break;
                    case Message.Alter:
                        if (msg.strings != null)
                            if (msg.strings.Length > 0)
                                if (msg.index >= 0 && msg.index < 1 << 20)
                                    lock (this)
                                        if (userAddress[msg.index] != null)
                                        {
                                            UserAddress u = userAddress[msg.index];
                                            Logs.Log.Add($"Alter input for user{msg.index} of project {u.project}: {msg.strings[0]}");
                                            registrars[u.registrar].projects[u.project].users[u.user].alter = msg.strings[0];
                                        }
                        break;
                    case Message.Transform:
                        if (msg.strings != null)
                            if (msg.strings.Length > 0)
                                if (msg.index >= 0 && msg.index < 1 << 20)
                                    lock (this)
                                        if (userAddress[msg.index] != null)
                                        {
                                            UserAddress u = userAddress[msg.index];
                                            int i = 0;
                                            Logs.Log.Add($"Location input for user{msg.index} of project {u.project}: {msg.floats.Length} floats");
                                            while (i + 7 < msg.floats.Length)
                                            {
                                                float[] fs = new float[8];
                                                for (int f = 0; f < 8; f++)
                                                    fs[f] = msg.floats[i++];
                                                registrars[u.registrar].projects[u.project].users[u.user].transform.Add(new Transform(fs));
                                            }
                                        }
                        break;
                }
            }
        }

    }
    public class Registrar
    {
        public const byte Taken = 2;
        public const byte Vacant = 1;
        public const byte Either = 3;
        public int index = 0;
        public string name;
        public string email;
        public string[] token;
        public byte[] taken;
        public TamesProject[] projects;
        public Registrar(string name, string email)
        {
            this.name = name;
            this.email = email;
            token = new string[20];
            taken = new byte[20];
            for (int i = 0; i < 20; i++) taken[i] = Vacant;
            projects = new TamesProject[20];
        }
    }
}
