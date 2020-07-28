using Newtonsoft.Json;
using SIL.Linq;
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
                    var asxml = verse.Parent.ToString();

                    XElement newVerse = AddParatextVerse(verse.Parent, verse.FirstAttribute.Value, text);
                    XNode nextVerse = verse.NextNode;
                    XNode endverse = newVerse;
                    
                    asxml = verse.Parent.ToString();

                    while (nextVerse != null)
                    {
                        endverse.AddAfterSelf(nextVerse); //unlike javascript, this doesn't MOVE it, it copies it
                        XNode rem = nextVerse;
                        nextVerse = nextVerse.NextNode;
                        if (rem.NodeType == System.Xml.XmlNodeType.Element)
                        {
                            XNode remchild = ((XElement)rem).FirstNode;
                            while (remchild != null)
                            {
                                XNode x = remchild;
                                remchild = remchild.NextNode;
                                x.Remove();
                            }
                        }
                        rem.Remove();
                        endverse = endverse.NextNode;
                    }
                    asxml = verse.Parent.ToString();
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
            var book = chapterContent.GetElement("book");
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
            return chapterContent.GetElement("chapter");
        }
        private static XNode FindNodeAfterVerse(int startverse, int endverse, IEnumerable<XElement> verses)
        {
            //find where to put it
            XElement nextVerse = null;
            verses.ForEach(v =>
            {
                if (nextVerse == null)
                {
                    if (v.StartVerse() == startverse && v.EndVerse() > endverse)
                        nextVerse = v;
                    else if (v.StartVerse() > startverse)
                        nextVerse = v;
                }
            });
            if (nextVerse != null)
            {
                nextVerse= (XElement)MoveToPara(nextVerse);
                //skip section if there
                if (nextVerse.PreviousNode != null && nextVerse.PreviousNode.PreviousNode != null && nextVerse.PreviousNode.PreviousNode.IsSection())
                    return nextVerse.PreviousNode.PreviousNode;
                return nextVerse;
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

        public static XElement GenerateParatextData(XElement chapterContent, Passage currentPassage, string transcription, IEnumerable<SectionSummary> sectionSummaryList,bool addNumbers)
        {
            IEnumerable<XElement> verses = chapterContent.GetElements("verse");

            string sDebug = TraverseNodes(chapterContent, 1);

            //find the verses that contain verses in my range
            SortedList<string, XElement> existing = new SortedList<string, XElement>();
            for (int ix = currentPassage.StartVerse; ix <= currentPassage.EndVerse; ix++)
            {
                XElement verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                if (verse != null && !existing.ContainsKey(verse.Verses()) && (verse.Verses() == currentPassage.Verses || verse.Scripture() == ""))            
                    existing.Add(verse.Verses(), verse);
            }
            existing.Values.ForEach(v => v.RemoveVerse());

            verses = chapterContent.GetElements("verse");
            XNode nextVerse = FindNodeAfterVerse(currentPassage.StartVerse, currentPassage.EndVerse, verses);
            XNode thisVerse;
            if (nextVerse == null)
            {   //add it at the end
                thisVerse = AddParatextVerse(chapterContent.LastNode, currentPassage.Verses, transcription);
            }
            else
            {   //add before
                thisVerse = AddParatextVerse(nextVerse, currentPassage.Verses, transcription, true);
            }
            if (currentPassage.StartVerse == 1)
            {
                SectionSummary sectionInfo = sectionSummaryList.First(s => s.startChapter == currentPassage.StartChapter && s.startVerse == currentPassage.StartVerse);
                //add/update the section header
                if (thisVerse.PreviousNode.IsSection())
                {
                    ((XText)((XElement)thisVerse.PreviousNode).FirstNode).Value = sectionInfo.SectionHeader(addNumbers);
                }
                else
                {
                    thisVerse.AddBeforeSelf(ParatextSection(sectionInfo.SectionHeader(addNumbers)));
                }
            }
            sDebug = TraverseNodes(chapterContent, 1);
            return chapterContent;
        }
     }
}
