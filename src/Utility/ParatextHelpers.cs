using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIL.Transcriber.Utility
{
    public class ParatextHelpers
    {
        public static string ParatextProject(int projectId, ProjectService projectService)
        {
            var paratextSettings = projectService.IntegrationSettings(projectId, "paratext");
            if (paratextSettings != null)
            {
                dynamic settings = JsonConvert.DeserializeObject(paratextSettings);
                return settings.ParatextId;
            }
            return null;
        }

        public static StringBuilder GenerateParatextData(Passage currentPassage, string chapterContent, string transcription, string heading = "")
        {
            var sb = new StringBuilder();
            var firstIndex = 0;
            var list = chapterContent.Split(new[] { "\\v" }, StringSplitOptions.None).ToList();

            if (!chapterContent.Contains(@"\c " + currentPassage.StartChapter))
            {
                list.Insert(1, @"\c " + currentPassage.StartChapter);
            }

            var startContains = list.FirstOrDefault(f => f.TrimStart().StartsWith(currentPassage.StartVerse.ToString() + " ")
                                                         || f.TrimStart().StartsWith(currentPassage.StartVerse.ToString() + "-")
                                                         || f.TrimStart().StartsWith(currentPassage.StartVerse.ToString() + "\r\n"));
            if (startContains == null)
            {
                firstIndex = InsertVerseAsNew(currentPassage, null, ref list);
            }
            else if (startContains.Trim().IndexOf('-') > 0 && startContains.Trim().IndexOf('-') <= 4)
            {
                firstIndex = InsertVerseWithRange(startContains, currentPassage, firstIndex, ref list);
            }
            else
            {
                firstIndex = InsertVerseWithoutRange(currentPassage, ref list);
            }

            var verseFormat = " " + currentPassage.StartVerse + "-" + currentPassage.EndVerse;
            var pContentFormat = " " + transcription + "\r\n";
            list.Insert(firstIndex, verseFormat + pContentFormat);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Trim().Length == 0) continue;
                if (i == 0 || list[i].Contains(@"\c " + Convert.ToInt32(currentPassage.StartChapter)))
                {
                    if (i == 0 && currentPassage.StartVerse == 1)
                    {
                        list[i] = RemovePreviousSection(list[i], heading != string.Empty);
                    }
                    if (i == 0 && !list[i].EndsWith("\r\n") || i > 0)
                    {
                        sb.Append(list[i].Trim() + Environment.NewLine);
                    }
                    else
                    {
                        sb.Append(list[i]);
                    }
                }
                else
                {
                    if (i == firstIndex)
                    {
                        sb.Append(heading != string.Empty
                            ? $@"\s1 {heading}{Environment.NewLine}\p \v{list[i]}"
                            : $@"\v{list[i]}");
                    }
                    else
                    {
                        if (i == firstIndex - 1)
                        {
                            list[i] = RemovePreviousSection(list[i], heading != string.Empty);
                        }
                        sb.Append($@"\v{list[i]}");
                    }
                }
            }

            return sb;
        }

        private static string RemovePreviousSection(string verseContent, bool isHeadingIncluded)
        {
            if (isHeadingIncluded)
            {
                var sectionPosition = verseContent.IndexOf("\\s", StringComparison.InvariantCulture);
                if (sectionPosition > 0)
                {
                    return verseContent.Replace(verseContent.Substring(sectionPosition, verseContent.Length - sectionPosition), "");
                }
            }
            return verseContent;
        }

        /// <summary>
        /// It handles the verse numbers without hyphen(-)
        /// </summary>
        /// <param name="currentTask">Current Task Id</param>
        /// <param name="list">List of Verses from SFM File of the Current Chapter</param>
        /// <returns>Index of the new Verse Number</returns>
        private static int InsertVerseWithoutRange(Passage currentPassage, ref List<string> list)
        {
            var firstIndex = GetIndexFromList(ref list, currentPassage.StartVerse.ToString());
            var secondIndex = GetIndexFromList(ref list, currentPassage.EndVerse.ToString());
            list.RemoveRange(firstIndex, (secondIndex - firstIndex) + 1);
            return firstIndex;
        }

        /// <summary>
        /// It handles the verse numbers with hyphen(-)
        /// </summary>
        /// <param name="startContains">Starting Verse Content</param>
        /// <param name="currentTask">Current Task Id</param>
        /// <param name="firstIndex">Index of the Starting Verse</param>
        /// <param name="list">List of Verses from SFM File of the Current Chapter</param>
        /// <returns>Index of the new Verse Number</returns>
        private static int InsertVerseWithRange(string startContains, Passage currentPassage, int firstIndex,
            ref List<string> list)
        {
            string[] verseParts = startContains.Trim().Split(' ');

            if (verseParts[0] == (currentPassage.StartVerse + "-" + currentPassage.EndVerse))
            {
                firstIndex = GetIndexFromList(ref list, verseParts[0]);
                list.RemoveAt(firstIndex);
            }
            else
            {
                string[] rangeItems = verseParts[0].Split('-');
                string firstPart = rangeItems[0];
                if (Convert.ToInt32(firstPart) >= currentPassage.StartVerse)
                {
                    if (int.Parse(rangeItems[1]) > currentPassage.EndVerse)
                    {
                        firstIndex = GetIndexFromList(ref list, verseParts[0]);
                    }
                    else
                    {
                        firstIndex = GetIndexFromList(ref list, verseParts[0]) + 1;
                    }
                }
                else if (Convert.ToInt32(firstPart) < Convert.ToInt32(currentPassage.StartVerse))
                {
                    firstIndex = GetIndexFromList(ref list, verseParts[0]) - 1;
                }
            }

            return firstIndex;
        }

        /// <summary>
        /// It handles verse numbers that does not exist already in the SFM File
        /// </summary>
        /// <param name="currentTask">Current Task Id</param>
        /// <param name="startContains">Starting Verse Content</param>
        /// <param name="list">List of Verses from SFM File of the Current Chapter</param>
        /// <returns>Index of the new Verse Number</returns>
        private static int InsertVerseAsNew(Passage currentPassage, string startContains, ref List<string> list)
        {
            int firstIndex;
            for (int i = currentPassage.StartVerse - 1; i > 0; i--)
            {
                startContains = list.FirstOrDefault(f => f.Trim().StartsWith(i.ToString()));
                if (startContains != null)
                    break;
            }

            if (startContains != null)
            {
                string[] verseParts = startContains.Trim().Split(' ');
                firstIndex = GetIndexFromList(ref list, verseParts[0]) + 1;
            }
            else
            {
                firstIndex = list.Count;
            }

            return firstIndex;
        }

        /// <summary>
        /// Gets Index of the Verse Range of the Transcribed Data from the Verses List
        /// </summary>
        /// <param name="verseList">List of Verses from SFM File of the Current Chapter</param>
        /// <param name="searchString">Verse Range of the Transcribed Data</param>
        /// <returns>Index of the Verse Range</returns>
        private static int GetIndexFromList(ref List<string> verseList, string searchString)
        {
            int index = verseList.Select((c, i) => new { c, i })
                .Where(x => x.c.Trim().StartsWith(searchString))
                .Select(x => x.i).FirstOrDefault();
            return index;
        }
    }
}
