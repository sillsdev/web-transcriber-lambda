using SIL.Paratext.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TranscriberAPI.Utility.Extensions
{
    public static class XElementExtensions
    {
        public static string Verses(this XElement value)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            if (value.Name.LocalName != "verse")
                return null;
            return value.Attribute("number").Value;
        }
        public static void SetReference(this XElement value, string reference)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            value.Attribute("number").Value = reference;
        }
        public static int StartVerse(this XElement value)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            int startVerse = 0, endVerse = 0;
            if (value.Verses() == null)
                return 0;
            ParseReference(value.Verses(), out startVerse, out endVerse);
            return startVerse;
        }
        public static int EndVerse(this XElement value)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            int startVerse = 0, endVerse = 0;
            if (value.Verses() == null)
                return 0;
            ParseReference(value.Verses(), out startVerse, out endVerse);
            return endVerse;
        }
        private static bool ParseReference(string reference, out int startVerse, out int endVerse)
        {
            bool OK = true;
            startVerse = 0; endVerse = 0;
            if (reference.Contains("-"))
            {
                OK = int.TryParse(reference.Substring(0, reference.IndexOf('-')), out startVerse);
                if (OK)
                    OK = int.TryParse(reference.Substring(reference.IndexOf('-') + 1), out endVerse);
            }
            else
            {
                OK = int.TryParse(reference, out startVerse);
                endVerse = startVerse;
            }
            return OK;
        }
        public static bool IsPara(this XElement value)
        {
            return value.Name == "para";
        }
        public static string SectionText(this XElement section)
        {
            if (section.FirstNode.NodeType != System.Xml.XmlNodeType.Text)
                return "";
            return ((XText)section.FirstNode).Value;
        }
        public static bool IncludesVerse(this XElement value, int number)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            int startVerse = 0, endVerse = 0;
            if (value.Verses() == null)
                return false;
            ParseReference(value.Verses(), out startVerse, out endVerse);
            return number >= startVerse && number <= endVerse;
        }
        public static string Scripture(this XElement value)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            if (value.NextNode.NodeType != System.Xml.XmlNodeType.Text)
                return "";
            return ((XText)value.NextNode).Value;
        }
        public static void SetScripture(this XElement value, string scripture)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            if (value.NextNode == null || value.NextNode.NodeType != System.Xml.XmlNodeType.Text)
                value.AddAfterSelf(new XText(""));
            ((XText)value.NextNode).Value = scripture;
        }

        public static void RemoveVerse(this XElement value)
        {
            Debug.Assert(value.Name.LocalName == "verse");
            if (value.NextNode != null && value.NextNode.NodeType == System.Xml.XmlNodeType.Text)
                value.NextNode.Remove();
            value.Remove();
        }
    }

}
