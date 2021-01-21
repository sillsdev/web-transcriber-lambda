using Newtonsoft.Json;
using SIL.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static void RemoveText(XNode next)
        {
            XNode rem;
            while (next != null)
            {
                rem = next;
                if (next.IsText())
                {
                    if (next.NextNode != null)
                        RemoveText(next.NextNode);
                    next = next.Parent.NextNode;
                    rem.Remove();
                }
                else if (next.IsPara())
                {
                    if (((XElement)next).FirstNode == null || ((XElement)next).FirstNode.IsText())
                    {
                        if (((XElement)next).FirstNode != null) ((XElement)next).FirstNode.Remove();
                        next = next.NextNode;
                        if (((XElement)rem).FirstNode == null) rem.Remove();
                    }
                    else next = null;
                }
                else if (next.IsVerse())
                    next = null;
                else //skip notes etc
                    next = next.NextNode;
            }
        }
        public static void ReplaceText(XElement para, string transcription)
        {
            XNode value = para.FirstNode;
            RemoveText(value.NextNode);
            string[] lines = transcription.Split('\n');
            XText newverse = new XText(lines[0]);
            value.AddAfterSelf(newverse);
            XNode last = value.Parent;
            for (int ix = 1; ix < lines.Length; ix++)
            {
                last.AddAfterSelf(ParatextPara("p", new XText(lines[ix])));
                last = last.NextNode;
            }
            return;
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

        private static XElement MoveToPara(XElement verse)
        {
            string text = verse.VerseText();
            if (verse.Parent.IsPara()) 
            {
                if (verse.PreviousNode != null)
                {
                    XElement newVerse = AddParatextVerse(verse.Parent, verse.FirstAttribute.Value, text);
                    XNode nextVerse = verse.NextNode;
                    XNode endverse = newVerse;
                    
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
                    verse.RemoveVerse();  //remove the verse and its text
                    return newVerse;
                }
                return verse.Parent;
            }
            XNode prev = verse.PreviousNode;
            AddParatextVerse(prev, verse.FirstAttribute.Value, text);
            verse.RemoveVerse();  //remove the verse and its text
            return (XElement)prev.NextNode; //return the para
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
                nextVerse = MoveToPara(nextVerse);
                //skip section if there
                if (nextVerse.PreviousNode != null && nextVerse.PreviousNode.IsSection())
                    return nextVerse.PreviousNode;
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
        public static SortedList<string, XElement> GetExistingVerses(XElement chapterContent, Passage currentPassage, out XElement thisVerse)
        {
            XElement exactVerse = null;
            IEnumerable<XElement> verses = chapterContent.GetElements("verse");
            SortedList<string, XElement> existing = new SortedList<string, XElement>();
            for (int ix = currentPassage.StartVerse; ix <= currentPassage.EndVerse; ix++)
            {
                IEnumerable<XElement> myverses = verses.Where(n => ((XElement)n).IncludesVerse(ix));
                myverses.ForEach(verse =>
                {
                    if (verse.Verses() == currentPassage.Verses)
                    {
                        if (!existing.ContainsKey(verse.Verses()))
                        {
                            exactVerse = verse;
                            existing.Add(verse.Verses(), verse);
                            //if our section is there...add it to the remove list
                            if (!existing.ContainsKey("S" + verse.Verses()) && currentPassage.Sequencenum == 1 && verse.Parent.IsPara() && verse.Parent.PreviousNode != null && verse.Parent.PreviousNode.IsSection())
                                existing.Add("S" + verse.Verses(), (XElement)verse.Parent.PreviousNode);
                        }
                    }
                    else
                    {
                        if (!existing.ContainsKey(verse.Verses()) && (verse.VerseText() == "" || (verse.StartVerse() >= currentPassage.StartVerse && verse.EndVerse() <= currentPassage.EndVerse)))
                        {
                            existing.Add(verse.Verses(), verse);
                        }
                    }
                });
            }
            thisVerse = exactVerse;
            return existing;
        }

        private static IEnumerable<Passage> ParseTranscription(Passage currentPassage, string transcription)
        {
            string pattern = @"(\\v\s*[1-9+]-*[1-9+]*)";
            List<Passage> ret = new List<Passage>();
            // Create a Regex  
            Regex rg = new Regex(pattern);

            // Get all matches  
            MatchCollection internalverses = rg.Matches(transcription);
            if (internalverses.Count == 0)
            {
                currentPassage.LastComment = transcription;
                ret.Add(currentPassage);
            }
            // Report on each match.
            for (int ix = 0; ix < internalverses.Count; ix++)
            {
                Match match = internalverses[ix];
                int start = match.Index + match.Value.Length;
                string t =  ix < internalverses.Count-1 ? transcription.Substring(start, internalverses[ix+1].Index - start) : transcription.Substring(start);
                if (t.EndsWith('\n'))
                    t = t.Remove(t.Length - 1);
                Passage p = new Passage
                {
                    Book = currentPassage.Book,
                    Reference = currentPassage.StartChapter.ToString() + ":" + match.Value.Replace("\\v", ""),
                    LastComment = t.TrimStart(),
                    SectionId = currentPassage.SectionId,
                };

                ret.Add(p);
            }
            return ret;
        }
        public static string GetParatextData(XElement chapterContent, Passage currentPassage)
        {
            string transcription = "";
            //find the verses that contain verses in my range
            SortedList<string, XElement> existing = GetExistingVerses(chapterContent, currentPassage, out XElement thisVerse);
            existing.Values.ForEach(v =>
            {
                if (v.IsVerse())
                    transcription += "\\v" + v.Verses() + " " + v.VerseText().Replace("\\p", "\r");
            });
            return transcription;
        }
        public static XElement GenerateParatextData(XElement chapterContent, Passage currentPassage, string transcription, bool addNumbers)
        {
            IEnumerable<Passage> parsedPassages = ParseTranscription(currentPassage, transcription);
            bool first = true;
            if (parsedPassages.Count() > 1)
            {
                //remove the original range if it exists 
                GetExistingVerses(chapterContent, currentPassage, out XElement thisVerse);
                if (thisVerse != null)
                    thisVerse.RemoveVerse();
            }
            parsedPassages.ForEach(p =>
            {
                //find the verses that contain verses in my range
                SortedList<string, XElement> existing = GetExistingVerses(chapterContent, p, out XElement thisVerse);
                existing.Values.ForEach(v =>
                {
                    if (v != thisVerse)
                    {
                        if (v.IsVerse())
                            v.RemoveVerse();
                        else
                            v.RemoveSection();
                    }
                });

                if (thisVerse != null)
                {
                    thisVerse = MoveToPara(thisVerse);
                    ReplaceText(thisVerse, p.LastComment);
                }
                else
                {
                    IEnumerable<XElement> verses = chapterContent.GetElements("verse");
                    XNode nextVerse = FindNodeAfterVerse(p.StartVerse, p.EndVerse, verses);
                    if (nextVerse == null)
                    {   //add it at the end
                        thisVerse = AddParatextVerse(chapterContent.LastNode, p.Verses, p.LastComment);
                    }
                    else
                    {   //add before
                        thisVerse = AddParatextVerse(nextVerse, p.Verses, p.LastComment, true);
                    }
                }
                if (currentPassage.Sequencenum == 1 && first)
                {
                    //add/update the section header
                    if (thisVerse.PreviousNode.IsSection())
                    {
                        ((XText)((XElement)thisVerse.PreviousNode).FirstNode).Value = currentPassage.Section.SectionHeader(addNumbers);
                    }
                    else
                    {
                        thisVerse.AddBeforeSelf(ParatextSection(currentPassage.Section.SectionHeader(addNumbers)));
                    }
                    first = false;
                }
            });
            return chapterContent;
        }
    }
}