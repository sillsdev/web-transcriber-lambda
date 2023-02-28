using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TranscriberAPI.Utility.Extensions;

namespace SIL.Transcriber.Utility
{
    public class ParatextHelpers
    {
        public static string ParatextProject(int? projectId, string artifactType, ProjectIntegrationRepository piRepo)
        {
            if (projectId == null)
                return "";
            string? paratextSettings = piRepo.IntegrationSettings(projectId??0, "paratext"+ artifactType);
            if (paratextSettings is null or "")
            {
                throw new Exception("No Paratext Integration Settings for this project " + projectId.ToString());
            }
            dynamic? settings = JsonConvert.DeserializeObject(paratextSettings);
            return settings?.ParatextId ?? "";
        }

        public static string TraverseNodes(XNode? xml, int level)
        {
            string myLevel = "";
            for (XNode? n = xml; n != null; n = n.NextNode)
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
        public static void ReplaceText(XElement value, string transcription)
        {
            value.RemoveText();
            string[] lines = transcription.Split('\n');
            if (lines.Length == 1 && !lines [0].EndsWith('\n'))
                lines [0] += '\n';
            XText newverse = new (lines[0]);
            value.AddAfterSelf(newverse);
            XNode ?last = value.Parent.IsPara() ? value.Parent : value.NextNode;

            for (int ix = 1; ix < lines.Length; ix++)
            {
                last?.AddAfterSelf(ParatextPara("p", new XText(lines [ix])));
                last = last?.NextNode;
            }
            return;
        }
        private static XElement ParatextPara(string style, XNode? child = null)
        {
            return new XElement("para", new XAttribute("style", style), child);
        }
        private static XElement ParatextSection(string text)
        {
            return ParatextPara("s", new XText(text));
        }
        private static XElement AddParatextVerse(XNode? parent, string verses, string text, bool before = false)
        {
            XElement verse = new ("verse", new XAttribute("number", verses), new XAttribute("style", "v"), null);
            XElement first = ParatextPara("p", verse);
            if (parent == null)
                return first;

            if (before)
                parent.AddBeforeSelf(first);
            else
                parent.AddAfterSelf(first);
            ReplaceText(verse, text);
            /*XNode last = first;
            for (int ix = 1; ix < lines.Length; ix++)
            {
                last.AddAfterSelf(ParatextPara("p", new XText(lines[ix])));
                last = last.NextNode;
            }*/
            return first;
        }

        private static XElement? MoveToPara(XElement verse)
        {
            if (verse.IsPara())
                return verse;
            string text = verse.VerseText();
            if (verse.Parent?.IsPara()??false)
            {
                if (verse.PreviousNode != null)
                {
                    XElement newVerse = AddParatextVerse(verse.Parent, verse.FirstAttribute?.Value??"", text);
                    XNode? nextVerse = verse.NextNode;
                    XNode? endverse = newVerse;

                    while (nextVerse != null)
                    {
                        endverse?.AddAfterSelf(nextVerse); //unlike javascript, this doesn't MOVE it, it copies it
                        XNode rem = nextVerse;
                        nextVerse = nextVerse.NextNode;
                        if (rem.NodeType == System.Xml.XmlNodeType.Element)
                        {
                            XNode? remchild = ((XElement)rem).FirstNode;
                            while (remchild != null)
                            {
                                XNode x = remchild;
                                remchild = remchild.NextNode;
                                x.Remove();
                            }
                        }
                        rem.Remove();
                        endverse = endverse?.NextNode;
                    }
                    _ = verse.RemoveVerse();  //remove the verse and its text
                    return newVerse;
                }
                else if (verse.NextNode != null)
                {
                    XNode? next = verse.NextNode;
                    while (next?.IsText() ?? false)
                        next = next?.NextNode;
                    if (next?.IsVerse()??false)
                        MoveToPara((XElement)next);
                }
                return verse.Parent;
            }
            XNode? prev = verse.PreviousNode;
            _ = AddParatextVerse(prev, verse.FirstAttribute?.Value ?? "", text);
            _ = verse.RemoveVerse();  //remove the verse and its text
            return (XElement?)prev?.NextNode; //return the para
        }

        public static XElement? GetParatextBook(XElement? chapterContent, string code, bool addIt = false)
        {
            XElement? book = chapterContent?.GetElement("book");
            if (book == null && addIt)
            {
                book = new XElement("book", new XAttribute("code", code));
                /* find the book */
                if (chapterContent == null)
                    _ = book;
                else
                    chapterContent.AddFirst(book);
            }
            return book;
        }

        public static XElement? AddParatextChapter(XElement? chapterContent, string book, int number)
        {
            XElement? chapter = GetParatextChapter(chapterContent);
            if (chapter == null)
            {
                chapter = new XElement("chapter", new XAttribute("number", number), new XAttribute("style", "c"));
                /* find the book */
                if (number == 1 || GetParatextBook(chapterContent, book) != null) //if first chapter, or it's already there...
                {
                    XElement? bookElement = GetParatextBook(chapterContent, book, true);
                    bookElement?.AddAfterSelf(chapter);
                }
                else
                {
                    if (chapterContent == null)
                        chapterContent = chapter;
                    else
                        chapterContent.AddFirst(chapter);
                }
            }
            return chapterContent;
        }

        public static XElement? GetParatextChapter(XElement? chapterContent)
        {
            return chapterContent?.GetElement("chapter");
        }
        private static XNode? FindNodeAfterVerse(int startverse, int endverse, IEnumerable<XElement>? verses)
        {
            //find where to put it
            XElement? nextVerse = null;
            if (verses != null)
                foreach (XElement v in verses)
                {
                    if (nextVerse == null)
                    {
                        if (v.StartVerse() == startverse && v.EndVerse() > endverse)
                            nextVerse = v;
                        else if (v.StartVerse() > startverse)
                            nextVerse = v;
                    }
                };
            if (nextVerse != null)
            {
                nextVerse = MoveToPara(nextVerse);
                //skip section if there
                return nextVerse?.PreviousNode != null && nextVerse.PreviousNode.IsSection() 
                    ? nextVerse.PreviousNode 
                    : nextVerse;
            }
            return nextVerse;
        }
        private static bool FindNodes(XNode? thisNode, XNode nextVerse, List<XNode> list)
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
        public static SortedList<string, XElement> GetExistingVerses(XElement? chapterContent, Passage currentPassage, out XElement? thisVerse)
        {
            SortedList<string, XElement> existing = new ();
            XElement? exactVerse = null;
            if (chapterContent != null)
            {
                IEnumerable<XElement>? verses = chapterContent.GetElements("verse");
                for (int ix = currentPassage.StartVerse; ix <= currentPassage.EndVerse; ix++)
                {
                    IEnumerable<XElement>? myverses = verses?.Where(n => n.IncludesVerse(ix));
                    if (myverses != null)
                        foreach (XElement verse in myverses)
                        {
                            if (verse.Verses() == currentPassage.Verses)
                            {
                                if (!existing.ContainsKey(verse.SortableVerses()))
                                {
                                    exactVerse = verse;
                                    existing.Add(verse.SortableVerses(), verse);
                                    //if our section is there...add it to the remove list
                                    if (!existing.ContainsKey("S" + verse.Verses()) && currentPassage.Sequencenum == 1 && verse.Parent.IsPara() && verse.Parent?.PreviousNode != null && verse.Parent.PreviousNode.IsSection())
                                        existing.Add("S" + verse.SortableVerses(), (XElement)verse.Parent.PreviousNode);
                                }
                            }
                            else
                            {
                                if (!existing.ContainsKey(verse.SortableVerses()) && (verse.VerseText() == "" || (verse.StartVerse() >= currentPassage.StartVerse && verse.EndVerse() <= currentPassage.EndVerse)))
                                {
                                    existing.Add(verse.SortableVerses(), verse);
                                }
                            }
                        };
                }
            }
            thisVerse = exactVerse;
            return existing;
        }

        private static IEnumerable<Passage> ParseTranscription(Passage currentPassage, string transcription)
        {
            string pattern = @"(\\v\s*[1-9+]-*[1-9+]*)";
            List<Passage> ret = new ();
            // Create a Regex  
            Regex rg = new (pattern);

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
                string t =  ix < internalverses.Count-1 ? transcription[start..internalverses[ix+1].Index ] : transcription[start..];
                if (t.EndsWith('\n'))
                    t = t.Remove(t.Length - 1);
                Passage p = new ()
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
        public static string GetParatextData(XElement? chapterContent, Passage currentPassage)
        {
            string transcription = "";

            //find the verses that contain verses in my range
            SortedList<string, XElement> existing = GetExistingVerses(chapterContent, currentPassage, out _);
            if (existing.Values.Count == 0)
                throw new Exception("no range");
            foreach (XElement v in existing.Values)
            {
                if (v.IsVerse())
                    transcription += "\\v" + v.Verses() + " " + v.VerseText().Replace("\\p", "\r");
            };
            return transcription;
        }
        public static XElement? GenerateParatextData(XElement? chapterContent, Passage currentPassage, string transcription, bool addNumbers)
        {
            Debug.WriteLine(transcription);
            IEnumerable<Passage> parsedPassages = ParseTranscription(currentPassage, transcription);
            bool first = true;
            if (parsedPassages.Count() > 1)
            {
                //remove the original range if it exists 
                _ = GetExistingVerses(chapterContent, currentPassage, out XElement? thisVerse);
                if (thisVerse != null)
                    _ = thisVerse.RemoveVerse();
            }
            foreach (Passage? p in parsedPassages)
            {
                //find the verses that contain verses in my range
                SortedList<string, XElement> existing = GetExistingVerses(chapterContent, p, out XElement? thisVerse);
                foreach (XElement? v in existing.Values)
                {
                    if (v != thisVerse)
                    {
                        if (v.IsVerse())
                            _ = v.RemoveVerse();
                        else
                            v.RemoveSection();
                    }
                };

                if (thisVerse != null)
                {
                    thisVerse = MoveToPara(thisVerse);
#pragma warning disable CS8604 // Possible null reference argument.
                    ReplaceText(thisVerse, (p.LastComment ?? ""));
#pragma warning restore CS8604 // Possible null reference argument.
                }
                else
                {
                    IEnumerable<XElement>? verses = chapterContent?.GetElements("verse");
                    XNode? nextVerse = FindNodeAfterVerse(p.StartVerse, p.EndVerse, verses);
                    thisVerse = 
                        nextVerse == null
                        //add it at the end
                        ? AddParatextVerse(chapterContent?.LastNode, p.Verses, p.LastComment ?? "")
                        //add before
                        : AddParatextVerse(nextVerse, p.Verses, p.LastComment ?? "", true);
                }
                if (currentPassage.Sequencenum == 1 && first)
                {
                    XElement? vp = MoveToPara(thisVerse);
                    if (vp != null)
                        //add/update the section header
                        if (vp.PreviousNode?.IsSection() ?? false)
                        {
                            XText? firstNode = (XText?)((XElement)vp.PreviousNode).FirstNode;
                            if (firstNode != null)
                                firstNode.Value = currentPassage.Section?.SectionHeader(addNumbers) ?? "";
                        }
                        else
                        {
                            vp.AddBeforeSelf(ParatextSection(currentPassage.Section?.SectionHeader(addNumbers) ?? ""));
                        }
                    first = false;
                }
            }
            return chapterContent;
        }
    }
}