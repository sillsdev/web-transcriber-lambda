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
        private static XElement ParatextVerse(string verses, string text)
        {
            return ParatextPara("p", new XElement("verse", new XAttribute("number", verses), new XAttribute("style", "v"), new XText(text)));
        }

        private static XNode MoveToPara(XElement verse)
        {
            var prev = verse.PreviousNode;
            if (!verse.Parent.IsPara())
            {
                var text = verse.Scripture();
                verse.RemoveVerse();  //remove the verse and it's text
                prev.AddAfterSelf(ParatextVerse(verse.FirstAttribute.Value, text));
                return prev.NextNode; //return the para
            }
            return verse.Parent;
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

        public static XElement AddSectionHeaders(XElement chapterContent, IEnumerable<SectionSummary> sectionSummaryList)
        {
            IEnumerable<XElement> verses = GetElements(chapterContent, "verse");
            IEnumerable<XElement> existingsections = GetElementsWithAttribute(chapterContent, "para", "s");
            IEnumerable<string> sectionList = sectionSummaryList.Select(s => s.SectionHeader());
            //remove any that don't match
            IEnumerable<XElement> changedSections = existingsections.Where(s => sectionList.FirstOrDefault(h => h == s.SectionText()) == null).ToList();
            foreach (XElement s in changedSections)
                    s.Remove();

            foreach (SectionSummary sectionInfo in sectionSummaryList)
            {
                //find it
                XElement section = existingsections.Where(s => s.SectionText() == sectionInfo.SectionHeader()).FirstOrDefault();
                XElement verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(sectionInfo.startVerse)).FirstOrDefault();
                //is it in the right place?
                if (section != null)
                {
                    if (verse == null || MoveToPara(verse).PreviousNode != section)
                    {
                        section.Remove();  // remove it...we'll readd it rather than trying to move it.
                        section = null;
                    }
                }
                if (section == null) 
                {
                    //find the next verse to add it before if (verse == null)
                    int lastinChapter = 0;
                    if (verses.LastOrDefault() != null)
                    {
                        lastinChapter = verses.Last().EndVerse();
                    }
                    for (int ix = sectionInfo.startVerse; verse == null && ix <= lastinChapter; ix++)
                    {
                        verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                    }
                    if (verse == null)
                    {
                        chapterContent.LastNode.AddAfterSelf(ParatextSection(sectionInfo.SectionHeader()));
                        chapterContent.LastNode.AddAfterSelf(ParatextVerse(sectionInfo.startVerse.ToString(), ""));
                    }
                    else
                    {
                        verse = (XElement)MoveToPara(verse);
                        var prev = verse.PreviousNode;
                        prev.AddAfterSelf(ParatextSection(sectionInfo.SectionHeader()));
                    }
                }
 
            }
            return chapterContent;
        }

        public static XElement GenerateParatextData(XElement chapterContent, Passage currentPassage, string transcription, IEnumerable<SectionSummary> sectionSummaryList)
        {
            string sDebug = TraverseNodes(chapterContent, 1);

            IEnumerable<XElement> verses = GetElements(chapterContent, "verse");
            IEnumerable<XElement> sections = GetElementsWithAttribute(chapterContent, "para", "s");

            //find the verses that contain verses in my range
            SortedList<int, XElement> existing = new SortedList<int, XElement>();
            for (int ix = currentPassage.StartVerse; ix <= currentPassage.EndVerse; ix++)
            {
                XElement verse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                if (verse != null && !existing.ContainsKey(verse.StartVerse()))
                    existing.Add(verse.StartVerse(), verse);
            }
            if (existing.Count == 0)
            {
                int lastinChapter = verses.LastOrDefault() != null ? int.Parse(verses.Last().FirstAttribute.Value) : 0;
                //find where to put it
                XElement nextVerse = null;
                int ix = currentPassage.EndVerse + 1;
                while (nextVerse == null && ix <= lastinChapter)
                {
                    nextVerse = (XElement)verses.Where(n => ((XElement)n).IncludesVerse(ix)).FirstOrDefault();
                    ix++;
                }
                if (nextVerse == null)
                {   //add it at the end
                    chapterContent.LastNode.AddAfterSelf(ParatextVerse(currentPassage.Verses, transcription));
                }
                else
                {
                     //see if the verse we're adding before is in a section
                    var checkSection = sectionSummaryList.FirstOrDefault(s => nextVerse.StartVerse() >= s.startVerse && nextVerse.StartVerse() <= s.endVerse);
                    if (checkSection != null)
                    {
                        //do I have a section header?
                        var sectionSummary = sectionSummaryList.FirstOrDefault(s => currentPassage.StartVerse >= s.startVerse && currentPassage.StartVerse <= s.endVerse);
                        if (sectionSummary != checkSection)
                        {
                            //put it before the section header
                            nextVerse = sections.FirstOrDefault(s => s.SectionText() == checkSection.SectionHeader());
                        }
                    }
                    if (nextVerse.Parent.IsPara())
                        nextVerse = nextVerse.Parent;
                    nextVerse.AddBeforeSelf(ParatextVerse(currentPassage.Verses, transcription));
                }
            }
            else
            {
                //we'll take over the first node we found...but see if we need to replace any verses
                XElement thisVerse = existing.Values[0];
                //add a node for each verse before our passage starts
                for (int ix = thisVerse.StartVerse(); ix < currentPassage.StartVerse; ix++)
                {
                    thisVerse.AddBeforeSelf(ParatextVerse(ix.ToString(), ""));
                }
                int last = existing.Values[existing.Count - 1].EndVerse();
                thisVerse.SetReference(currentPassage.Verses);
                thisVerse.SetScripture(transcription);
                //delete any other nodes - remembering last verse deleted
                for (int ix = existing.Count - 1; ix > 0; ix--)
                {
                    existing.Values[ix].RemoveVerse();
                }
                //add a node for each verse after our passage ends
                for (int ix = last;  ix > currentPassage.EndVerse; ix--)
                {
                    thisVerse.NextNode.AddAfterSelf(ParatextVerse(ix.ToString(), ""));
                }
                MoveToPara(thisVerse);
            }
           // sDebug = TraverseNodes(chapterContent, 1);
            return chapterContent;
        }
     }
}
