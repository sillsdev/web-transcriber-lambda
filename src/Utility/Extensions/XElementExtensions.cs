using System.Diagnostics;
using System.Xml.Linq;

namespace SIL.Transcriber.Utility.Extensions
{
    public static class XElementExtensions
    {
        public static string? Verses(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            return !IsVerse(value) ? null : (value.Attribute("number")?.Value);
        }
        public static string SortableVerses(this XElement value)
        {
            _ = ParseReference(value.Verses(), out int startVerse, out int endVerse);
            return endVerse == startVerse ? startVerse.ToString("D4") : startVerse.ToString("D4") + "-" + endVerse.ToString("D4");
        }
        public static void SetReference(this XElement value, string reference)
        {
            Debug.Assert(IsVerse(value));
            if (value.Attribute("number") != null)
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                value.Attribute("number").Value = reference;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }
        public static int StartVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            if (value.Verses() == null)
                return 0;
            _ = ParseReference(value.Verses(), out int startVerse, out _);
            return startVerse;
        }
        public static int EndVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            if (value.Verses() == null)
                return 0;
            _ = ParseReference(value.Verses(), out _, out int endVerse);
            return endVerse;
        }
        private static bool ParseReference(string? reference, out int startVerse, out int endVerse)
        {
            bool OK;
            startVerse = 0;
            endVerse = 0;
            if (string.IsNullOrEmpty(reference))
                return false;

            if (reference.Contains('-'))  //TODO do other languages use something besides - ???
            {
                OK = int.TryParse(reference.AsSpan(0, reference.IndexOf('-')), out startVerse);
                if (OK)
                    OK = int.TryParse(reference.AsSpan(reference.IndexOf('-') + 1), out endVerse);
            }
            else
            {
                OK = int.TryParse(reference, out startVerse);
                endVerse = startVerse;
            }

            return OK;
        }
        public static bool IsText(this XNode? value)
        {
            return value is not null && value.NodeType == System.Xml.XmlNodeType.Text;
        }
        public static bool IsPara(this XNode? value)
        {
            return value != null && value.NodeType == System.Xml.XmlNodeType.Element
                && ((XElement)value).Name == "para";
        }
        public static bool IsSection(this XNode? value)
        {
            return IsPara(value) && (value as XElement)?.Attribute("style")?.Value == "s";
        }
        public static bool IsVerse(this XNode? value)
        {
            return value != null && value.NodeType == System.Xml.XmlNodeType.Element
                && ((XElement)value).Name.LocalName == "verse";
        }
        public static bool IsNote(this XNode? value)
        {
            return value != null && value.NodeType == System.Xml.XmlNodeType.Element
                && (((XElement)value).Name.LocalName == "note" || ((XElement)value).Name.LocalName == "rem");
        }
        public static bool HasChildren(this XNode? value)
        {
            return value != null && value.NodeType == System.Xml.XmlNodeType.Element
                && ((XElement)value).FirstNode != null;
        }

        public static string SectionText(this XElement section)
        {
            return !IsText(section.FirstNode) ? "" : ((XText?)section.FirstNode)?.Value ?? "";
        }
        public static bool IncludesVerse(this XElement value, int number)
        {
            Debug.Assert(IsVerse(value));
            if (value.Verses() == null)
                return false;
            _ = ParseReference(value.Verses(), out int startVerse, out int endVerse);
            return number >= startVerse && number <= endVerse;
        }
        public static string VerseText(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            if (value.NextNode == null)
                return "";
            //ignore cross ref, notes, etc
            string text = value.FirstNode?.NextNode?.IsText() ?? false ? ((XText)value.FirstNode.NextNode).Value  : "";
            XNode? next = value.NextNode;
            while (next != null)
            {
                if (next.IsVerse() || next.IsSection())
                    next = null;
                else if (next.IsText())
                {
                    text += ((XText)next).Value;
                    next = next.NextNode ?? next.Parent?.NextNode;
                }
                else if (next.IsPara() && !next.IsSection())
                {
                    text += '\n';
                    next = ((XElement)next).FirstNode ?? ((XElement)next).NextNode;
                }
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
            if (value.NextNode != null)
                ((XText)value.NextNode).Value = scripture;
        }

        public static void RemoveSection(this XElement value)
        {
            Debug.Assert(IsSection(value));
            value.Remove();
        }
        public static XNode? MoveTo(this XNode value, XNode? dest)
        {
            XNode? copyNode = value;
            XNode? newNode = null;
            while (copyNode != null)
            {
                dest?.AddAfterSelf(copyNode);
                dest = dest?.NextNode;
                newNode ??= dest;
                XNode? rem = copyNode;
                copyNode = copyNode.NextNode;
                rem.Remove();
            }
            return newNode;
        }
        public static void RemoveText(this XElement? value)
        {
            XElement? verse = value;
            if (IsPara(value))
                verse = (XElement?)value?.FirstNode;
            Debug.Assert(IsVerse(verse));
            if (verse == null)
                return;

            static XNode? NextNodeDepthFirst(XNode node)
            {
                if (node is XElement e && e.FirstNode != null)
                    return e.FirstNode;

                XNode? current = node;
                while (current != null)
                {
                    if (current.NextNode != null)
                        return current.NextNode;
                    current = current.Parent;
                }
                return null;
            }

            XNode? next = verse.NextNode ?? verse.Parent?.NextNode;
            while (next != null)
            {
                if (next.IsSection() || next.IsVerse() || (next.IsPara() && (((XElement)next).FirstNode?.IsVerse() ?? false)))
                    break;

                XNode current = next;
                next = NextNodeDepthFirst(current);

                if (current.IsNote())
                    continue;

                if (current.IsPara())
                {
                    //continuation of my verse inside a paragraph
                    XElement para = (XElement)current;
                    XNode? mytext = para.FirstNode;
                    //move all the text and other nodes after my verse to after the verse
                    mytext?.NextNode?.MoveTo(value);
                    if (mytext != null)
                        current = mytext;
                    next = NextNodeDepthFirst(current);
                }

                if (!HasChildren(current))
                {
                    XElement? parent = current.Parent;
                    current.Remove();
                    while (parent != null && !parent.IsVerse() && !parent.IsSection() && !parent.HasElements && string.IsNullOrWhiteSpace(parent.Value))
                    {
                        XElement? removeParent = parent;
                        parent = removeParent.Parent;
                        removeParent.Remove();
                    }
                }
            }
        }
        public static bool RemoveVerse(this XElement value)
        {
            Debug.Assert(IsVerse(value));
            RemoveText(value);
            XElement? removeParent = (value.Parent?.IsPara()??false) && (value.Parent?.GetElements("verse").Count()??0) == 1 ? value?.Parent : null;
            if (!value.HasChildren())
            {
                value?.Remove();
                removeParent?.Remove();
                return true;
            }
            return false;
        }

        public static IEnumerable<XElement> GetElements(this XElement root, string name)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && n.Name.LocalName == name);
        }
        public static IEnumerable<XElement> GetElementsWithAttribute(this XElement root, string name, string attributeValue)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && n.Name.LocalName == name && n.FirstAttribute?.Value == attributeValue);
        }
        public static XElement? GetElement(this XElement root, string name)
        {
            return root.GetElements(name).FirstOrDefault();
        }
        public static string TraverseNodes(this XNode? xml, int level)
        {
            string myLevel = "";
            for (XNode? n = xml; n != null; n = n.NextNode)
            {
                try
                {
                    myLevel += "\n";
                    for (int ix = level; ix > 1; ix--)
                        myLevel += "\t";
                    myLevel += "{Level " + level.ToString() + " Type:" + n.NodeType + " ";
                    switch (n.NodeType)
                    {
                        case System.Xml.XmlNodeType.Element:
                            myLevel += " Name: " + ((XElement)n).Name + " FirstAttribute: " + ((XElement)n).FirstAttribute;
                            myLevel += TraverseNodes(((XElement)n).FirstNode, level + 1);
                            break;
                        case System.Xml.XmlNodeType.Text:
                            myLevel += " Value: " + ((XText)n).Value;
                            break;
                        default:
                            break;
                    }
                    myLevel += "}";
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return myLevel;
        }
    }

}