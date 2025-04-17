using System.Text.RegularExpressions;

namespace SIL.Transcriber.Utility;

public static class FileName
{
    /// <summary>
    /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
    /// </summary>
    /// <remarks>
    /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
    /// </remarks>
    public static string CleanFileName(string filename)
    {
        string invalidChars = Regex.Escape(
               new string(Path.GetInvalidFileNameChars()));

        string[] reservedWords =
            [
                "CON",
                "PRN",
                "AUX",
                "CLOCK$",
                "NUL",
                "COM0",
                "COM1",
                "COM2",
                "COM3",
                "COM4",
                "COM5",
                "COM6",
                "COM7",
                "COM8",
                "COM9",
                "LPT0",
                "LPT1",
                "LPT2",
                "LPT3",
                "LPT4",
                "LPT5",
                "LPT6",
                "LPT7",
                "LPT8",
                "LPT9"
            ];

        string sanitizedName = Regex.Replace(
                filename,
                @"['()*?&/<\[\]\\,""| \r\n:#]+",
                "_"
            );
        string invalidReStr = string.Format(@"[{0}, ]+", invalidChars);
        sanitizedName = Regex.Replace(
                sanitizedName,
                invalidReStr,
                "_"
            );
        while (sanitizedName.IndexOf("__") > -1)
            sanitizedName = sanitizedName.Replace("__", "_");

        foreach (string reservedWord in reservedWords)
        {
            string reservedWordPattern = string.Format("^{0}(\\.|$)", reservedWord);
            sanitizedName = Regex.Replace(
                sanitizedName,
                reservedWordPattern,
                "_reservedWord_$1",
                RegexOptions.IgnoreCase
            );
        }

        return sanitizedName;
    }

}
