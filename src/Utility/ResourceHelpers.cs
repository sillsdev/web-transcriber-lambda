﻿
using System.IO;
using System.Linq;
using System.Reflection;


namespace SIL.Transcriber.Utility
{
    public class ResourceHelpers
    {
        public static string LoadResource(string name)
        {
            //Load the file
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith(name));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
