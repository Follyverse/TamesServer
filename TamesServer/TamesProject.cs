using System.IO;
using System.IO.Compression;
namespace TamesServer
{
    public class TamesProject
    {
        public int registrar;
        public int index;
        public string name;
        public string token;
        public List<User> users = new List<User>();
        public string[] headers = new string[4];
        public int maxDuration;

        public List<User> archive = new List<User>();

        public void Archive(User u)
        {
            archive.Add(u);
            users.Remove(u);
        }
    }


}