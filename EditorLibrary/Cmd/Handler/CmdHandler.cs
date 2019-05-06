﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using EditorLibrary.Dict;
using EditorLibrary.Editor;
using System.Collections.Generic;
using System.Reflection;

namespace EditorLibrary.Cmd
{
    public static class CmdHandler
    {
        public static bool Execute(string cmd)
        {
            cmd = Regex.Replace(cmd, @"\s+", " ");

            if (IsExecutionEnd(cmd))
                return false;

            Validate(cmd);
            return true;
        }

        private static bool IsExecutionEnd(string cmd)
        {
            return
                Regex.Match(cmd, GetRegexCmd(CmdDict.End), RegexOptions.IgnoreCase).Success;
        }

        private static string GetRegexCmd(string cmd)
        {
            return $"^{cmd}$";
        }

        private static void CheckCmdMisstake(string cmd)
        {
            List<IndexedWord> words = EditorHandler.GetNumeredWordsText(cmd)
            .Where(w => !string.IsNullOrWhiteSpace(w.Value))
            .ToList();
            List<IndexedWord> cmdWords = new List<IndexedWord>();
            Type cmds = typeof(CmdDict);
            string pName = string.Empty;
            foreach(PropertyInfo p in cmds.GetProperties())
            {
                cmdWords = EditorHandler.GetNumeredWordsText(p.GetValue(cmds, null).ToString())
                .Where(w => !string.IsNullOrWhiteSpace(w.Value))
                .ToList();
                
                if(words[0].Value == cmdWords
                .OrderBy(o => o.WhiteSpacePosition)
                .Select(s => s.Value).FirstOrDefault())
                {
                     pName = p.Name;
                    break;
                }
            }
            if(string.IsNullOrEmpty(pName))
            {
                throw new WrongCmdException(cmd);
            }

            string expected = string.Empty, finded = string.Empty;
            for(int i = 0; i < words.Count; i++)
            {
                cmdWords[i].Value = Regex.Replace(cmdWords[i].Value, @"(\?|\[|\]|\[ \])", "");
                if (i > cmdWords.Count)
                {
                    expected = ExceptionMessageDict.EndLine;
                    finded = words[i].Value;
                    break;
                }
                else if(!Regex.IsMatch(words[i].Value,cmdWords[i].Value))
                {
                    switch(cmdWords[i].Value)
                    {
                        case "(\\S+)": expected = ExceptionMessageDict.word;
                        break;
                        case "(\\d+)" : expected = ExceptionMessageDict.number;
                        break;
                        case "(\\d+|\\S+)": expected = $"{ExceptionMessageDict.word} {ExceptionMessageDict.or} {ExceptionMessageDict.number}";
                        break;
                        case "(<-|->)": expected = ExceptionMessageDict.direction;
                        break;
                        case "(.+)": expected = ExceptionMessageDict.text;
                        break;
                        default: expected = cmdWords[i].Value;
                        break;
                    }
                    finded = words[i].Value;
                    break;
                    //throw new MisstakeCmdException($"\"{cmd}\" {ExceptionMessageDict.Expected} {expected} {ExceptionMessageDict.Finded} {words[i].Value}");
                }
            }
            throw new MisstakeCmdException($"\"{cmd}\" {ExceptionMessageDict.Expected} {expected} {ExceptionMessageDict.Finded} {finded}");


        }

        public static void Validate(string cmd)
        {
            Match matchInput  = Regex.Match(cmd, GetRegexCmd(CmdDict.Input ), RegexOptions.IgnoreCase);
            Match matchFormat = Regex.Match(cmd, GetRegexCmd(CmdDict.Format), RegexOptions.IgnoreCase);
            Match matchCursor = Regex.Match(cmd, GetRegexCmd(CmdDict.Cursor), RegexOptions.IgnoreCase);
            Match matchSend = Regex.Match(cmd,   GetRegexCmd(CmdDict.Send  ), RegexOptions.IgnoreCase);
            Match matchPrint = Regex.Match(cmd,  GetRegexCmd(CmdDict.Print ), RegexOptions.IgnoreCase);
            Match matchEnd = Regex.Match(cmd,    GetRegexCmd(CmdDict.End   ), RegexOptions.IgnoreCase);


            if (matchInput.Success)

                Input(new InputCmd
                {
                    cmd = new Text
                    {
                        Name = matchInput.Groups[1].Value,
                        Data = matchInput.Groups[2]?.Value
                    }
                });

            else if (matchFormat.Success)

                Format(new FormatCmd
                {
                    name = matchFormat.Groups[1].Value,
                    separators = matchFormat.Groups.Count == 3 ?
                    Regex
                    .Split(matchFormat.Groups[2].Value, "")
                    .Where(w => !String.IsNullOrEmpty(w))
                    .ToArray() :
                    null

                });

            else if (matchCursor.Success)

                Cursor(new CursorCmd
                {
                    cmd = new Cursor
                    {
                        Name = matchCursor.Groups[1].Value,
                        Target = EditorHandler.GetTexts(matchCursor.Groups[2].Value).FirstOrDefault(),
                        From = new CursorDestination
                        {
                            Position = Regex.Match(matchCursor.Groups[3].Value, @"\d+").Success ?
                            Convert.ToInt32(matchCursor.Groups[3].Value) :
                            (int?)null,

                            Word = Regex.Match(matchCursor.Groups[3].Value, @"\S+").Success ?
                            matchCursor.Groups[3].Value :
                            null,
                        },
                        Ahead = matchCursor.Groups[4].Value == "->",
                        To = new CursorDestination
                        {
                            Position = Regex.Match(matchCursor.Groups[5].Value, @"\d+").Success ?
                            Convert.ToInt32(matchCursor.Groups[5].Value) :
                            (int?)null,

                            Word = Regex.Match(matchCursor.Groups[5].Value, @"\S+").Success ?
                            matchCursor.Groups[5].Value :
                            null,
                        }
                    }
                });

            else if (matchSend.Success)

                Send(new SendCmd
                {
                    SourceName = matchSend.Groups[1].Value,
                    TargetName = matchSend.Groups[2].Value
                });

            else if (matchPrint.Success)
                Print(new PrintCmd
                {
                    Name = !string.IsNullOrEmpty(matchPrint.Groups[1]?.Value) ?
                    matchPrint.Groups[1].Value :
                    null
                });

            else
                CheckCmdMisstake(cmd);
        }

        public static void Print(PrintCmd p)
        {
            Console.WriteLine(JsonConvert.SerializeObject(EditorHandler.Print(p.Name), Formatting.Indented));
        }

        public static void Input(InputCmd cmd)
        {
            if(string.IsNullOrEmpty(cmd.cmd.Data))
            {
                string area = EditorHandler.GetTexts(cmd.cmd.Name).Select(s => s.Data).FirstOrDefault();
                TextView.Execute.ShowAreaWindow(cmd.cmd.Name, area);
                cmd.cmd.Data = TextView.Execute.area;
            }

            EditorHandler.AddText(cmd.cmd);
        }

        public static void Format(FormatCmd cmd)
        {
            EditorHandler.Format(cmd.name, cmd.separators);
        }

        public static void Cursor(CursorCmd cmd)
        {
            EditorHandler.AddCursor(cmd.cmd);
        }

        public static void Send(SendCmd cmd)
        {
            EditorHandler.SendText(cmd.SourceName, cmd.TargetName);
        }

    }
}
