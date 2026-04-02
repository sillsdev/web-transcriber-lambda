using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility.Extensions;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;

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
                throw new Exception("No Paratext Integration Settings for this project " + projectId.ToString() + " type: paratext" + artifactType);
            }
            dynamic? settings = JsonConvert.DeserializeObject(paratextSettings);
            return settings?.ParatextId ?? "";
        }

        public static void ReplaceText(XElement value, string transcription)
        {
            XElement verse = value;
            if (value.IsPara())
            {
                XElement? paraVerse = value.Elements().FirstOrDefault(e => e.IsVerse());
                if (paraVerse == null)
                    return;
                verse = paraVerse;
            }

            verse.RemoveText();
            string[] lines = transcription.Replace("\r", "").Split('\n');
            if (lines.Length == 1 && !lines[0].EndsWith('\n'))
                lines[0] += '\n';
            XText newverse = new (lines[0]);
            verse.AddAfterSelf(newverse);
            XNode? last = verse.Parent.IsPara() ? verse.Parent : verse.NextNode;

            for (int ix = 1; ix < lines.Length; ix++)
            {
                last?.AddAfterSelf(ParatextPara("p", new XText(lines[ix])));
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
        private static XElement AddParatextVerse(XNode? parent, string verses, string text, bool para, bool before = false)
        {
            XElement verse = new ("verse", new XAttribute("number", verses), new XAttribute("style", "v"), null);
            if (para)
                verse = ParatextPara("p", verse);
            if (parent == null)
                return verse;

            if (before)
                parent.AddBeforeSelf(verse);
            else
                parent.AddAfterSelf(verse);
            ReplaceText(verse, text);
            return verse;
        }
        private static XElement? RemovePara(XElement verse)
        {
            XElement para = verse;
            if (!verse.IsPara() && verse.Parent.IsPara() && verse.Parent?.FirstNode == verse)
                para = verse.Parent;
            else if (!verse.IsPara())
                return verse;

            //move all the text and other nodes in this para to the previous
            XNode? dest = para.PreviousNode ?? para.Parent;
            if (dest is XElement)
            {
                dest = ((XElement)dest).LastNode;
            }
            XNode? origdest = dest;
            XNode? copyNode = para?.FirstNode;
            while (copyNode != null)
            {
                dest?.AddAfterSelf(copyNode);
                dest = dest?.NextNode;
                XNode? rem = copyNode;
                copyNode = copyNode.NextNode;
                rem.Remove();
            }
            Debug.Assert(!para.HasChildren());
            para?.Remove();
            return origdest?.NextNode is XElement ? (XElement?)origdest?.NextNode : null;
        }
        private static XElement? MoveToPara(XElement verse)
        {
            if (verse.IsPara())
                return verse;
            string text = verse.VerseText();
            if (verse.Parent?.IsPara() ?? false)
            {
                if (verse.PreviousNode != null)
                {
                    XElement newVerse = AddParatextVerse(verse.Parent, verse.FirstAttribute?.Value??"", text, true);
                    XElement oldPara = verse.Parent;
                    List<XNode> moveNodes = [.. oldPara.Nodes().SkipWhile(n => n != verse).Skip(1)];
                    XNode? endverse = newVerse.LastNode;
                    foreach (XNode node in moveNodes)
                    {
                        endverse?.AddAfterSelf(node);
                        endverse = node;
                    }
                    _ = verse.RemoveVerse();  //remove the verse and its text
                    return newVerse;
                }
                return verse.Parent;
            }
            XNode? prev = verse.PreviousNode;
            _ = AddParatextVerse(prev, verse.FirstAttribute?.Value ?? "", text, true);
            _ = verse.RemoveVerse();  //remove the verse and its text
            return (XElement?)prev?.NextNode; //return the para
        }

        private static void LogCurrentStructure(XElement? chapterContent, string label)
        {
            Debug.WriteLine($"Paratext structure [{label}]");
            if (chapterContent == null)
            {
                Debug.WriteLine("<null chapterContent>");
                return;
            }

            Debug.WriteLine(chapterContent.FirstNode.TraverseNodes(1));
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
        private static XNode? FindNodeAfterVerse(int? startverse, int? endverse, IEnumerable<XElement>? verses)
        {
            //find where to put it
            XElement? nextVerse = null;
            if (verses != null && startverse != null && endverse != null)
                foreach (XElement v in verses)
                {
                    if (nextVerse == null)
                    {
                        if (v.StartVerse() == startverse && v.EndVerse() > endverse)
                            nextVerse = v;
                        else if (v.StartVerse() > startverse)
                            nextVerse = v;
                    }
                }
            ;
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
        public static SortedList<string, XElement> GetExistingVerses(int chapter, XElement? chapterContent, Passage currentPassage, out XElement? thisVerse)
        {
            SortedList<string, XElement> existing = [];
            XElement? exactVerse = null;
            if (chapterContent != null)
            {
                IEnumerable<XElement>? verses = chapterContent.GetElements("verse");
                int start = currentPassage.ChapterStartVerse(chapter);
                int end = currentPassage.ChapterEndVerse(chapter);
                for (int ix = start; ix <= end; ix++)
                {
                    IEnumerable<XElement>? myverses = verses?.Where(n => n.IncludesVerse(ix));
                    if (myverses != null)
                        foreach (XElement verse in myverses)
                        {
                            if (verse.Verses() == currentPassage.Verses(chapter))
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
                                if (!existing.ContainsKey(verse.SortableVerses()) &&
                                    (verse.VerseText() == "" ||
                                    (verse.StartVerse() >= currentPassage.ChapterStartVerse(chapter) && verse.EndVerse() <= currentPassage.ChapterEndVerse(chapter))))
                                {
                                    existing.Add(verse.SortableVerses(), verse);
                                }
                            }
                        }
                }
            }
            thisVerse = exactVerse;
            return existing;
        }

        private static List<Passage> ParseTranscription(Passage currentPassage, int chapter, string transcription)
        {
            string pattern = @"(\\v\s*([0-9]*)-?([0-9]*))";
            List<Passage> ret = [];
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
                    t = t[..^1];
                _ = int.TryParse(match.Groups[2].Value, out int startv);
                _ = int.TryParse(match.Groups[3].Value, out int endv);

                Passage p = new ()
                {
                    Book = currentPassage.Book,
                    Reference = chapter.ToString() + ":" + match.Value.Replace("\\v", ""),
                    LastComment = t.TrimStart(),
                    SectionId = currentPassage.SectionId,
                    StartChapter = chapter,
                    EndChapter = chapter,
                    StartVerse = startv,
                    EndVerse = endv > 0 ? endv : startv,
                };

                ret.Add(p);
            }
            return ret;
        }
        public static string GetParatextData(int chapter, XElement? chapterContent, Passage currentPassage)
        {
            string transcription = "";

            //find the verses that contain verses in my range
            SortedList<string, XElement> existing = GetExistingVerses(chapter, chapterContent, currentPassage, out _);
            if (existing.Values.Count == 0)
                throw new ArgumentOutOfRangeException(nameof(currentPassage), "no range");
            bool any = false;
            foreach (XElement v in existing.Values)
            {
                if (v.IsVerse())
                {
                    string txt = v.VerseText().Replace("\\p", "\r");
                    if (txt.Length > 0)
                    {
                        any = true;
                    }
                    transcription += "\\v" + v.Verses() + " " + txt;
                }
            }
            ;
            return any ? transcription : "";
        }
        public static XElement? GenerateParatextData(int chapter, XElement? chapterContent, Passage currentPassage, string transcription, bool addNumbers, SectionMap[] sectionMap)
        {
            Debug.WriteLine(transcription);
            IEnumerable<Passage> parsedPassages = ParseTranscription(currentPassage,chapter, transcription);
            bool first = true;
            if (parsedPassages.Count() > 1)
            {
                //remove the original range if it exists 
                _ = GetExistingVerses(chapter, chapterContent, currentPassage, out XElement? thisVerse);
                if (thisVerse != null)
                    _ = thisVerse.RemoveVerse();
            }
            bool firstVerse = true;
            foreach (Passage? p in parsedPassages)
            {
                //find the verses that contain verses in my range
                SortedList<string, XElement> existing = GetExistingVerses(chapter, chapterContent, p, out XElement? thisVerse);
                foreach (XElement? v in existing.Values)
                {
                    if (v != thisVerse)
                    {
                        if (v.IsVerse())
                            _ = v.RemoveVerse();
                        else
                            v.RemoveSection();
                    }
                }
                if (thisVerse != null)
                {
                    thisVerse = firstVerse ? MoveToPara(thisVerse) : RemovePara(thisVerse);
#pragma warning disable CS8604 // Possible null reference argument.
                    ReplaceText(thisVerse, (p.LastComment ?? ""));
#pragma warning restore CS8604 // Possible null reference argument.
                }
                else
                {
                    IEnumerable<XElement>? verses = chapterContent?.GetElements("verse");
                    XNode? nextVerse = FindNodeAfterVerse(p.ChapterStartVerse(chapter), p.ChapterEndVerse(chapter), verses);
                    thisVerse =
                        nextVerse == null
                        //add it at the end
                        ? AddParatextVerse(chapterContent?.LastNode, p.Verses(chapter), p.LastComment ?? "", firstVerse)
                        //add before
                        : AddParatextVerse(nextVerse, p.Verses(chapter), p.LastComment ?? "", firstVerse, true);
                }
                if (currentPassage.Sequencenum == 1 && first)
                {
                    XElement? vp = MoveToPara(thisVerse);
                    if (vp != null)
                    {
                        string header = currentPassage.Section?.SectionHeader(addNumbers, sectionMap) ?? "";
                        //add/update the section header
                        if (vp.PreviousNode?.IsSection() ?? false)
                        {
                            XText? firstNode = (XText?)((XElement)vp.PreviousNode).FirstNode;
                            if (firstNode != null)
                                firstNode.Value = header;
                            else
                                vp.PreviousNode.AddAfterSelf(new XText(header));
                        }
                        else
                        {
                            vp.AddBeforeSelf(ParatextSection(header));
                        }
                    }
                    first = false;
                }

                //LogCurrentStructure(chapterContent, $"after verse {p.Verses(chapter)}");
                firstVerse = false;
            }

            return chapterContent;
        }
    }
}