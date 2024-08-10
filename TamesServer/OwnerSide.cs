using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace TamesServer
{
    public class OwnerSide
    {
        public static OwnerSide instance;
        public string username, password, admin;
        public class Address
        {
            public string token;
            public int start, count;
            public string[] headers = new string[5];
            public Address(int s, int c) { start = s; count = c; }
        }
        public class Info
        {
            public string token, email, name;
            public Info(string token, string name, string email)
            {
                this.token = token;
                this.email = email;
                this.name = name;
            }
        }

        public List<Address> addresses = new List<Address>();
        public List<User> users = new List<User>();

        public List<Info> infos = new List<Info>();
        public OwnerSide()
        {
            instance = this;
        }
        public void Reset()
        {
            users.Clear();
            infos.Clear();
            addresses.Clear();
        }
        public void SendProjectRegistry(string email, string name)
        {
            string admin = this.admin;
            Task.Run(() =>
            {
                try
                {
                    SmtpClient mySmtpClient = new SmtpClient("smtp.google.com");

                    // set smtp-client with basicAuthentication
                    mySmtpClient.UseDefaultCredentials = false;
                    System.Net.NetworkCredential basicAuthenticationInfo = new
                       System.Net.NetworkCredential(Registry.instance.registrars[0].email, Registry.instance.registrars[0].name);
                    mySmtpClient.Credentials = basicAuthenticationInfo;

                    // add from,to mailaddresses
                    MailAddress from = new MailAddress("follyverse@gmail.com", "Tames Server");
                    MailAddress to = new MailAddress(email, "Tames User");
                    MailMessage myMail = new MailMessage(from, to);

                    // add ReplyTo

                    // set subject and encoding
                    myMail.Subject = "Succesful project registry: " + name;
                    myMail.SubjectEncoding = System.Text.Encoding.UTF8;

                    // set body-message and encoding
                    myMail.Body = $"Dear Tames User,\rThe project \"{name}\" was succesfully registered on Tames database. You can now receive the results of online sessions and surveys on your email. For this purpose, click on Request Results button on the Publish component of the Rig game object.\rYours faithfully,\r{admin}";
                    myMail.BodyEncoding = System.Text.Encoding.UTF8;
                    // text or html
                    myMail.IsBodyHtml = false;
                    Logs.Log.Add($"Registry email is being sent to {email}");
                    mySmtpClient.Send(myMail);
                }
                catch (Exception ex)
                {
                    Logs.Log.Add($"Registry email to {email} threw error: {ex.Message}");
                }
            });
        }
        public void ForStorage(TamesProject p)
        {
            int s = users.Count;
            int c = 0;
            string[] h = new string[4];
            for (int i = 0; i < h.Length; i++)
                h[i] = p.headers[i];
            foreach (User u in p.archive)
            {
                users.Add(u);
                c++;
            }
            p.archive.Clear();
            addresses.Add(new Address(s, c) { token = p.token, headers = h });
        }
        public void ForEmail(TamesProject p, string email)
        {
            Info info = new Info(p.token, p.name, email);
            infos.Add(info);
        }
        public void Apply()
        {
            Task.Run(() =>
            {
                lock (this)
                {
                    string recordPath = AppDomain.CurrentDomain.BaseDirectory + "records";
                    if (!Directory.Exists(recordPath))
                        Directory.CreateDirectory(recordPath);
                    Logs.Log.Add($"Applying OwnerSide: recording {addresses.Count} projects ({users.Count} users) and sending {infos.Count} projects");

                    Record(recordPath);
                    Send(recordPath);
                }
            });
        }
        void Record(string recordPath)
        {
            foreach (Address a in addresses)
            {
                if (a.count == 0) continue;

                string path = recordPath + "/" + a.token;
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                path += "/" + a.token;
                List<string> list = new List<string>();
                if (!File.Exists(path + "-profile.csv"))
                    list.Add("ID,Connection,Duration," + a.headers[0]);
                for (int i = 0; i < a.count; i++)
                    list.Add(users[i + a.start].id + ',' + users[i + a.start].date.ToString("yy/MM/dd HH:mm") + ',' + users[i + a.start].duration + ',' + users[i + a.start].profile);
                File.AppendAllLines(path + "-profile.csv", list.ToArray());

                list.Clear();
                if (a.headers[1].Length > 0)
                {
                    if (!File.Exists(path + "-progress.csv"))
                        list.Add("ID," + a.headers[1]);
                    for (int i = 0; i < a.count; i++)
                        list.Add(users[i + a.start].id + ',' + users[i + a.start].progress);
                    File.AppendAllLines(path + "-progress.csv", list.ToArray());
                }

                list.Clear();
                if (a.headers[2].Length > 0)
                {
                    if (!File.Exists(path + "-alter.csv"))
                        list.Add("ID," + a.headers[2]);
                    for (int i = 0; i < a.count; i++)
                        list.Add(users[i + a.start].id + ',' + users[i + a.start].alter);
                    File.AppendAllLines(path + "-alter.csv", list.ToArray());
                }
                list.Clear();
                if (a.headers[3].Length > 0)
                {
                    if (!File.Exists(path + "-survey.csv"))
                        list.Add("ID," + a.headers[3]);
                    for (int i = 0; i < a.count; i++)
                        list.Add(users[i + a.start].id + ',' + users[i + a.start].survey);
                    File.AppendAllLines(path + "-survey.csv", list.ToArray());
                }

                list.Clear();
                if (!File.Exists(path + "-location.csv"))
                    list.Add("ID,time,x,y,z,Rx,Ry,Rz,Rw");
                for (int i = 0; i < a.count; i++)
                    foreach (Transform t in users[i + a.start].transform)
                        list.Add(users[i + a.start].id + ',' + t.ToString());
                File.AppendAllLines(path + "-location.csv", list.ToArray());

                Logs.Log.Add($"Records for project {a.token} are saved");
            }
        }
        void Send(string path)
        {
            foreach (Info info in infos)
            {
                string recordPath = AppDomain.CurrentDomain.BaseDirectory + "records/" + info.token;
                if (Directory.Exists(path))
                {
                    ZipFile.CreateFromDirectory(recordPath, path + info.token + ".zip");
                    Logs.Log.Add($"Records for project {info.token} are processed for sending");
                    Email(info.email, info.name, path + info.token + ".zip");
                }else
                    Logs.Log.Add($"Records for project {info.token} were not found.");

            }
        }
        public void Email(string email, string name, string path)
        {
            try
            {
                SmtpClient mySmtpClient = new SmtpClient("smtp.google.com");

                // set smtp-client with basicAuthentication
                mySmtpClient.UseDefaultCredentials = false;
                System.Net.NetworkCredential basicAuthenticationInfo = new
                   System.Net.NetworkCredential(username, password);
                mySmtpClient.Credentials = basicAuthenticationInfo;

                // add from,to mailaddresses
                MailAddress from = new MailAddress(username, "Tames Server");
                MailAddress to = new MailAddress(email, "Tames Owner");
                MailMessage myMail = new MailMessage(from, to);

                // add ReplyTo

                // set subject and encoding
                myMail.Subject = "Results: " + name;
                myMail.SubjectEncoding = System.Text.Encoding.UTF8;

                // set body-message and encoding
                myMail.Body = $"Dear Tames User,\rPlease find the latest attached results for the project \"{name}\". This zip file may contain upto five files all with names beginning with the project's token. The files are in CSV format and can be opened by Excel or other spreadsheet applicaitons. \rPlease note that your next request will have the result for all users up to that point, inclusive of those in this email.\rYours faithfully,\r{admin}";
                myMail.BodyEncoding = System.Text.Encoding.UTF8;
                // text or html
                myMail.IsBodyHtml = false;
                myMail.Attachments.Add(new Attachment(path));
                Logs.Log.Add($"Results for project {name} is to be sent.");
                mySmtpClient.Send(myMail);
            }
            catch (Exception ex)
            {
                Logs.Log.Add($"Result sending for project {name} threw error: {ex.Message}");
            }
        }
    }
}
