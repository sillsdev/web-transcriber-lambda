using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TranscriberAPI.Utility.Extensions;

namespace SIL.Transcriber.Utility
{
    public class ParatextHelpers
    {
        public static IEnumerable<XElement> GetElements(XElement root, string name)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && ((XElement)n).Name.LocalName == name);
        }
        public static IEnumerable<XElement> GetElementsWithAttribute(XElement root, string name, string attributeValue)
        {
            return root.Descendants().Where(n => n.NodeType == System.Xml.XmlNodeType.Element && ((XElement)n).Name.LocalName == name && n.FirstAttribute.Value == attributeValue);
        }

        public static XElement GetElement(XElement root, string name)
        {
           return GetElements(root, name).FirstOrDefault();
        }
        public static string ParatextProject(int projectId, ProjectService projectService)
        {
            var paratextSettings = projectService.IntegrationSettings(projectId, "paratext");
            if ((paratextSettings ?? "") == "")
            {
                throw new Exception("No Paratext Integration Settings for this project " + projectId.ToString());
            }
            dynamic settings = JsonConvert.DeserializeObject(paratextSettings);
            return settings.ParatextId;
        }

        public static string TraverseNodes(XNode xml, int level)
        {
            string myLevel = "";
            for (XNode n = xml; n != null; n = n.NextNode)
            {
                try
                {
                    myLevel += "{newline}";
                    for (int ix = level; ix > 1; ix--)
                        myLevel += "{newtab}";
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

        private static XElement ParatextPara(string style, XNode child = null)
        {
            return new XElement("para", new XAttribute("style", style), child);
        }
        private static XElement ParatextSection(string text)
        {
            return ParatextPara("s", new XText(text));
        }
        private static XElement AddParatextVerse(XNode parent, string verses, string text, bool before = false)
        {
            string[] lines = text.Split('\n');
            XText newverse = new XText(lines[0]);
            XElement first = ParatextPara("p", new XElement("verse", new XAttribute("number", verses), new XAttribute("style", "v"), newverse));
            if (before)
                parent.AddBeforeSelf(first);
            else
                parent.AddAfterSelf(first);
            XNode last = first;
            for (int ix = 1; ix < lines.Length; ix++)
            {
                last.AddAfterSelf(ParatextPara("p", new XText(lines[ix])));
                last = last.NextNode;
            }
            return first;
        }

        private static XNode MoveToPara(XElement verse)
        {
            string text = verse.Scripture();
            if (verse.Parent.IsPara()) {
                if (verse.PreviousNode != null)
                {
                    XElement newVerse = AddParatextVerse(verse.Parent, verse.FirstAttribute.Value, text);
                    verse.RemoveVerse();  //remove the verse and its text
                    return newVerse;
                }
                return verse.Parent;
            }
            XNode prev = verse.PreviousNode;
            AddParatextVerse(prev, verse.FirstAttribute.Value, text);
            verse.RemoveVerse();  //remove the verse and its text
            return prev.NextNode; //return the para
        }

        public static XElement GetParatextBook(XElement chapterContent, string code, bool addIt = false)
        {
            var book = GetElement(chapterContent, "book");
            if (book == null && addIt)
            {
                book = new XElement("book", new XAttribute("code", code));
                /* find the book */
                chapterContent.AddFirst(book);
            }
            return book;
        }

        public static XElement AddParatextChapter(XElement chapterContent, string book, int number)
        {
            var chapter = GetParatextChapter(chapterContent);
            if (chapter == null)
            {
                chapter = new XElement("chapter", new XAttribute("number", number), new XAttribute("style", "c"));
                /* find the book */
                if (number == 1 || GetParatextBook(chapterContent, book) != null) //if first chapter, or it's already there...
                {
                    XElement bookElement = GetParatextBook(chapterContent, book, true);
                    bookElement.AddAfterSelf(chapter);
                }
                else
                {
                    chapterContent.AddFirst(chapter);
                }
            }
            return chapterContent;
        }

        public static XElement GetParatextChapter(XElement chapterContent)
        {
            return GetElement(chapterContent, "chapter");
        }
        public static XElement RemoveSectionHeaders(XElement chapterContent)
        {
            IEnumerable<XElement> existingsections = GetElementsWithAttribute(chapterContent, "para", "s").ToList();
            foreach (XElement s in existingsections)
                s.Remove();
            return chapterContent;
        }

        public static XElement AddSectionHeaders(XElement chapterContent, IEnumerable<SectionSummary> sectionSummaryList, bool addNumbers = true)
        {
            RemoveSectionHeaders(chapterContent);
            IEnumerable<XElement> verses = GetElements(chapterContent, "verse");
            IEnumerable<string> sectionList = sectionSummaryList.Select(s => s.SectionHeader(addNumbers));
            int lastinChapter = 0;
            if (verses.LastOrDefault() != null)
            {
                lastinChapter = verses.Last().EndVerse();
            }

            foreach (SectionSummary sectionInfo in sectionSummaryList)
            {
                XElement verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(sectionInfo.startVerse)).FirstOrDefault();
                //find the next verse to add it before if (verse == null)
                for (int ix = sectionInfo.startVerse; verse == null && ix <= lastinChapter; ix++)
                {
                    verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                }
                if (verse == null)
                {
                    chapterContent.LastNode.AddAfterSelf(ParatextSection(sectionInfo.SectionHeader(addNumbers)));
                    AddParatextVerse(chapterContent.LastNode, sectionInfo.startVerse.ToString(), "");
                }
                else
                {
                    verse = (XElement)MoveToPara(verse);
                    verse.AddBeforeSelf(ParatextSection(sectionInfo.SectionHeader(addNumbers)));
                }
             }
            return chapterContent;
        }
        //assumes sections have been removed
        private static XNode FindNodeAfterVerse(int verse, IEnumerable<XElement> verses)
        {
            int lastinChapter = verses.LastOrDefault() != null ? int.Parse(verses.Last().FirstAttribute.Value) : 0;
            //find where to put it
            XElement nextVerse = null;
            int ix = verse + 1;
            while (nextVerse == null && ix <= lastinChapter)
            {
                nextVerse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                ix++;
            }
            if (nextVerse != null)
            {
                return MoveToPara(nextVerse);
            }
            return nextVerse;
        }
        private static bool FindNodes(XNode thisNode, XNode nextVerse, List<XNode> list)
        {
            bool stop = false;
            while (thisNode != null && !stop)
                if (thisNode == nextVerse)
                    stop = true;
                else
                {
                    if (thisNode.NodeType == System.Xml.XmlNodeType.Element)
                        stop = FindNodes(((XElement)thisNode).FirstNode, nextVerse, list);
                    list.Add(thisNode);
                    thisNode = thisNode.NextNode;
                }
            return stop;
        }
        public static XElement GenerateParatextData(XElement chapterContent, Passage currentPassage, string transcription, IEnumerable<SectionSummary> sectionSummaryList)
        {
            IEnumerable<XElement> verses = GetElements(chapterContent, "verse");

            // string sDebug = TraverseNodes(chapterContent, 1);
            //find the verses that contain verses in my range
            SortedList<int, XElement> existing = new SortedList<int, XElement>();
            for (int ix = currentPassage.StartVerse; ix <= currentPassage.EndVerse; ix++)
            {
                XElement verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                if (verse != null && !existing.ContainsKey(verse.StartVerse()))
                    existing.Add(verse.StartVerse(), verse);
            }
            int last = 0;

            XNode nextVerse = FindNodeAfterVerse(existing.Count > 0 ? existing.Values[existing.Count - 1].EndVerse(): currentPassage.EndVerse, verses);
            if (nextVerse == null)
                last = -1;                

            if (existing.Count != 0)
            {
                last = existing.Values[existing.Count - 1].EndVerse();
                XElement thisVerse = existing.Values[0];
                XNode thisNode = MoveToPara(thisVerse);

                //add a node for each verse before our passage starts
                for (int ix = thisVerse.StartVerse(); ix < currentPassage.StartVerse; ix++)
                {
                    AddParatextVerse(thisNode, ix.ToString(), "", true);
                }
                List<XNode> deleteList = new List<XNode>();
                FindNodes(thisNode, nextVerse, deleteList);
                deleteList.ForEach(d => d.Remove());
            }
            if (nextVerse == null)
            {   //add it at the end
                AddParatextVerse(chapterContent.LastNode, currentPassage.Verses, transcription);
            }
            else
            {
                AddParatextVerse(nextVerse, currentPassage.Verses, transcription, true);
            }
                //add a node for each verse after our passage ends
            for (int ix = currentPassage.EndVerse+1;  ix <= last; ix++)
            {
                if (nextVerse == null)
                {   //add it at the end
                    AddParatextVerse(chapterContent.LastNode, ix.ToString(), "");
                }
                else
                {
                    AddParatextVerse(nextVerse, ix.ToString(), "", true);
                }
            }
            //sDebug = TraverseNodes(chapterContent, 1);
            return chapterContent;
        }
     }
}
