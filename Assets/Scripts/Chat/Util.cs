using System;
using System.Diagnostics;
using System.Linq;

namespace Assets.Scripts
{
    public class Util
    {
        public static UserProfile ParseProfileFromJid(string jid)
        {
            string[] data = jid.Split('_');
            UserProfile profile = new UserProfile();
            profile.FirstName = FirstCharToUpper(data[0]);
            profile.LastName = FirstCharToUpper(data[1]);
            profile.Email = data[2] + "@" + data[3] + "." + data[4];
            return profile;
        }

        public static string FirstCharToUpper(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + String.Join("", input.Skip(1));
        }

    }
}
