#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace MomomaAssets.Utility
{

    public static class StringExtension
    {
        public static string ToSentence(this string input)
        {
            var outChars = new Queue<char>();
            var isInit = true;
            var word = new Stack<char>();
            foreach (var c in input)
            {
                if (char.IsUpper(c))
                {
                    if (!isInit || (isInit = false))
                    {
                        if (!char.IsUpper(word.Peek()))
                            AddWordToChars(outChars, word);
                    }
                }
                else
                {
                    if (word.Count > 1 && char.IsUpper(word.Peek()))
                    {
                        var tempChar = word.Pop();
                        AddWordToChars(outChars, word);
                        word.Push(tempChar);
                    }
                }
                word.Push(c);
            }
            AddWordToChars(outChars, word);
            return new string(outChars.ToArray()).TrimEnd(' ');
        }

        static void AddWordToChars(Queue<char> str, Stack<char> word)
        {
            var lastChar = word.Peek();
            foreach (var wc in word.Reverse())
            {
                str.Enqueue(wc);
            }
            word.Clear();
            if (lastChar != ' ')
                str.Enqueue(' ');
        }
    }

}
#endif
