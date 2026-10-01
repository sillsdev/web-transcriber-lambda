using Newtonsoft.Json.Linq;
using SIL.Transcriber.Services;
using System.Reflection;
using Xunit;

namespace TranscriberAPI.Tests.Services;

public class AeroServiceTests
{
    [Fact]
    public void BuildClipArray_IncludesClipPathAndTimestamps()
    {
        JArray clips = InvokeBuildClipArray(new[] { "s3://bucket/input/a.wav" }, new[] { 0f, 12.5f });

        Assert.Single(clips);
        JObject clip = Assert.IsType<JObject>(clips[0]);
        Assert.Equal("s3://bucket/input/a.wav", clip["s3_path"]?.ToString());
        Assert.Equal(new JArray(0f, 12.5f), clip["timestamps"]);
        Assert.Null(clip["s3_paths"]);
    }

    [Fact]
    public void ParseTranscriptionStatusResult_MapsNestedItemsAndSegments()
    {
        // Aero v2 shape: each segment carries a "transcriptions" array whose entries hold the
        // model-specific fields (method, lang_code, log_id).
        JObject status = new()
        {
            ["total"] = 1,
            ["items"] = new JArray
            {
                new JObject
                {
                    ["clip"] = "1639253353982890452.ogg",
                    ["state"] = "SUCCESS",
                    ["segments"] = new JArray
                    {
                        new JObject
                        {
                            ["start"] = 0.0,
                            ["end"] = 25.2,
                            ["transcriptions"] = new JArray
                            {
                                new JObject
                                {
                                    ["transcription"] = "hello world",
                                    ["method"] = "omnilingual",
                                    ["lang_code"] = "seh_Latn",
                                    ["log_id"] = 7482
                                }
                            }
                        },
                        new JObject
                        {
                            ["start"] = 25.2,
                            ["end"] = 32.7,
                            ["transcriptions"] = new JArray
                            {
                                new JObject
                                {
                                    ["transcription"] = "second segment",
                                    ["method"] = "omnilingual",
                                    ["lang_code"] = "seh_Latn",
                                    ["log_id"] = 7480
                                }
                            }
                        }
                    },
                    ["progress"] = new JObject
                    {
                        ["completed"] = 2,
                        ["total"] = 2
                    }
                }
            },
            ["progress"] = new JObject
            {
                ["completed"] = 1,
                ["total"] = 1
            },
            ["error"] = JValue.CreateNull()
        };

        TranscriptionStatusResult result = InvokeParseTranscriptionStatusResult(status);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        TranscriptionStatusItem item = result.Items[0];
        Assert.Equal("1639253353982890452.ogg", item.Clip);
        Assert.Equal("SUCCESS", item.State);
        Assert.Equal(2, item.Segments.Count);
        Assert.Equal(2, item.Progress.Completed);
        Assert.Equal(2, item.Progress.Total);
        Assert.Equal(7482, item.Segments[0].LogId);
        Assert.Equal("hello world", item.Segments[0].Transcription);
        Assert.Equal("omnilingual", item.Segments[0].Method);
        Assert.Equal("seh_Latn", item.Segments[0].LangCode);
    }

    [Fact]
    public void ParseTranscriptionStatusResult_TranscriptionsArray_SelectsEntryByMethod()
    {
        // A single segment can carry both a base and a phonetic transcription in v2.
        JObject status = new()
        {
            ["total"] = 1,
            ["items"] = new JArray
            {
                new JObject
                {
                    ["clip"] = "a.wav",
                    ["state"] = "SUCCESS",
                    ["segments"] = new JArray
                    {
                        new JObject
                        {
                            ["start"] = 0.0,
                            ["end"] = 12.5,
                            ["transcriptions"] = new JArray
                            {
                                new JObject
                                {
                                    ["transcription"] = "hello world",
                                    ["method"] = "omnilingual",
                                    ["lang_code"] = "nld_Latn",
                                    ["log_id"] = 51
                                },
                                new JObject
                                {
                                    ["transcription"] = "hɛˈloʊ",
                                    ["method"] = "phonetic"
                                }
                            }
                        }
                    }
                }
            },
            ["error"] = JValue.CreateNull()
        };

        // Non-phonetic request selects the non-phonetic entry.
        TranscriptionStatusResult regular = InvokeParseTranscriptionStatusResult(status, phonetic: false);
        TranscriptionStatusSegment regularSegment = regular.Items[0].Segments[0];
        Assert.Equal("hello world", regularSegment.Transcription);
        Assert.Equal("omnilingual", regularSegment.Method);
        Assert.Equal("nld_Latn", regularSegment.LangCode);
        Assert.Equal(51, regularSegment.LogId);

        // Phonetic request selects the method="phonetic" entry.
        TranscriptionStatusResult phonetic = InvokeParseTranscriptionStatusResult(status, phonetic: true);
        TranscriptionStatusSegment phoneticSegment = phonetic.Items[0].Segments[0];
        Assert.Equal("hɛˈloʊ", phoneticSegment.Transcription);
        Assert.Equal("phonetic", phoneticSegment.Method);
    }

    [Fact]
    public void ParseTranscriptionStatus_UsesClipAndSegmentProgressCounts()
    {
        JObject status = new()
        {
            ["task_id"] = "task-1",
            ["state"] = "SUCCESS",
            ["result"] = new JObject
            {
                ["total"] = 2,
                ["items"] = new JArray
                {
                    new JObject
                    {
                        ["clip"] = "a.wav",
                        ["state"] = "SUCCESS",
                        ["segments"] = new JArray
                        {
                            new JObject
                            {
                                ["start"] = 0.0,
                                ["end"] = 10.0,
                                ["transcriptions"] = new JArray
                                {
                                    new JObject
                                    {
                                        ["transcription"] = "alpha",
                                        ["method"] = "omnilingual",
                                        ["lang_code"] = "seh_Latn",
                                        ["log_id"] = 1
                                    }
                                }
                            }
                        }
                    },
                    new JObject
                    {
                        ["clip"] = "b.wav",
                        ["state"] = "PENDING",
                        ["segments"] = new JArray()
                    }
                }
            },
            ["progress"] = new JArray(),
            ["error"] = JValue.CreateNull()
        };

        TranscriptionStatusResponse response = InvokeParseTranscriptionStatus(status);

        Assert.Equal("task-1", response.TaskId);
        Assert.Equal("SUCCESS", response.State);
        Assert.Equal(1, response.Progress.Completed);
        Assert.Equal(2, response.Progress.Total);
        Assert.Equal(2, response.Result.Total);
        Assert.Equal(2, response.Result.Items.Count);
        Assert.Equal(1, response.Result.Items[0].Progress.Completed);
        Assert.Equal(1, response.Result.Items[0].Progress.Total);
        Assert.Equal(0, response.Result.Items[1].Progress.Completed);
        Assert.Equal(1, response.Result.Items[1].Progress.Total);
    }

    [Fact]
    public void ExtractTranscriptionText_CombinesSegmentsInOrder()
    {
        IReadOnlyList<TranscriptionStatusItem> items =
        [
            new TranscriptionStatusItem(
                "a.wav",
                "SUCCESS",
                [
                    new TranscriptionStatusSegment(5.0f, 12.5f, "later", 2, null, null),
                    new TranscriptionStatusSegment(0.0f, 5.0f, "earlier", 1, null, null)
                ],
                new TranscriptionProgress(2, 2),
                null)
        ];

        string? text = InvokeExtractTranscriptionText(items);

        Assert.Equal("earlier later", text);
    }

    private static JArray InvokeBuildClipArray(string[] s3Paths, float[]? timing)
    {
        MethodInfo method = typeof(AeroService).GetMethod("BuildClipArray", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AeroService).FullName, "BuildClipArray");

        return (JArray)method.Invoke(null, [s3Paths, timing])!;
    }

    private static TranscriptionStatusResult InvokeParseTranscriptionStatusResult(JToken? result, bool phonetic = false)
    {
        MethodInfo method = typeof(AeroService).GetMethod("ParseTranscriptionStatusResult", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AeroService).FullName, "ParseTranscriptionStatusResult");

        return (TranscriptionStatusResult)method.Invoke(null, [result, phonetic])!;
    }

    private static TranscriptionStatusResponse InvokeParseTranscriptionStatus(JToken? status, bool phonetic = false)
    {
        MethodInfo method = typeof(AeroService).GetMethod("ParseTranscriptionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AeroService).FullName, "ParseTranscriptionStatus");

        Type envelopeType = typeof(AeroService).GetNestedType("TaskStatusEnvelope", BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(typeof(AeroService).FullName, "TaskStatusEnvelope");

        object envelope = Activator.CreateInstance(
            envelopeType,
            status?["task_id"]?.ToString() ?? string.Empty,
            status?["state"]?.ToString() ?? string.Empty,
            status?["result"],
            status?["progress"],
            status?["error"])!;

        return (TranscriptionStatusResponse)method.Invoke(null, [envelope, phonetic])!;
    }

    private static string? InvokeExtractTranscriptionText(IEnumerable<TranscriptionStatusItem> items)
    {
        MethodInfo method = typeof(AeroService).GetMethod("ExtractTranscriptionText", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(AeroService).FullName, "ExtractTranscriptionText");

        return (string?)method.Invoke(null, [items]);
    }
}
