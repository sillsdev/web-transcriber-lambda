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
            Debug.Assert(IsVerse(value));
            if (!IsVerse(value))
                return null;
            return value.Attribute("number").Value;
        }

        public static void SetReference(this XElement value, string reference)
        {
            Debug.Assert(IsVerse(value));
            value.Attribute("number").Value = reference;
        }
        public static int StartVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            int startVerse = 0, endVerse = 0;
            if (value.Verses() == null)
                return 0;
            ParseReference(value.Verses(), out startVerse, out endVerse);
            return startVerse;
        }
        public static int EndVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
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
            if (reference.Contains("-"))  //TODO do other languages use something besides - ???
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
        public static bool IsText(this XNode value)
        {
            if (value is null) return false;
            return value.NodeType == System.Xml.XmlNodeType.Text;
        }
        public static bool IsPara(this XNode value)
        {
            if (value == null || value.NodeType != System.Xml.XmlNodeType.Element)
                return false;
            return ((XElement)value).Name == "para";
        }
        public static bool IsSection(this XNode value)
        {
            return IsPara(value) && ((XElement)value).Attribute("style").Value == "s";
        }
        public static bool IsVerse(this XNode value)
        {
            if (value == null || value.NodeType != System.Xml.XmlNodeType.Element)
                return false;
            return ((XElement)value).Name.LocalName == "verse";
        }
        public static string SectionText(this XElement section)
        {
            if (!IsText(section.FirstNode))
                return "";
            return ((XText)section.FirstNode).Value;
        }
        public static bool IncludesVerse(this XElement value, int number)
        {
            Debug.Assert(IsVerse(value));
            int startVerse = 0, endVerse = 0;
            if (value.Verses() == null)
                return false;
            ParseReference(value.Verses(), out startVerse, out endVerse);
            return number >= startVerse && number <= endVerse;
        }
        public static string VerseText(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            if (value.NextNode == null)
                return "";
            //ignore cross ref, notes, etc
            string text = "";
            XNode next = value.NextNode;
            while (next != null)
            {
                if (next.IsText())
                {
                    text += ((XText)next).Value + '\n';
                    next = next.Parent.NextNode;
                }
                else if (next.IsPara())
                {
                    if (((XElement)next).FirstNode != null && ((XElement)next).FirstNode.IsText())
                        next = ((XElement)next).FirstNode;
                    else next = null;
                }
                else if (next.IsVerse())
                    next = null;
                else //skip notes etc
                    next = next.NextNode;
            }
            return text;
        }
        public static void SetScripture(this XElement value, string scripture)
        {
            Debug.Assert(IsVerse(value));
            if (!IsText(value.NextNode))
                value.AddAfterSelf(new XText(""));
            ((XText)value.NextNode).Value = scripture;
        }

        public static void RemoveSection(this XElement value)
        {
            Debug.Assert(IsSection(value));
            value.Remove();
        }
        public static void RemoveVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            XElement removeParent = value.Parent.IsPara() && value.Parent.GetElements("verse").Count() == 1 ? value.Parent : null;
            XNode next = value.NextNode;
            while (next != null)
            {
                if (next.IsText())
                {
                    XNode rem = next;
                    next = removeParent != null ? removeParent.NextNode : null;
                    rem.Remove();
                }
                else if (next.IsPara() && !next.IsSection() && !IsVerse(((XElement)next).FirstNode))
                {
                    XNode rem = next;
                    next = next.NextNode;
                    rem.Remove();
                }
                else
                    next = null;
            }
            value.Remove();
            if (removeParent != null)
                removeParent.Remove();
        }

        public static IEnumerable<XElement> GetElements(this XElement root, string name)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && ((XElement)n).Name.LocalName == name);
        }
        public static IEnumerable<XElement> GetElementsWithAttribute(this XElement root, string name, string attributeValue)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && ((XElement)n).Name.LocalName == name && n.FirstAttribute.Value == attributeValue);
        }
        public static XElement GetElement(this XElement root, string name)
        {
            return root.GetElements(name).FirstOrDefault();
        }
    }

}