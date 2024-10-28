using static Cliptok.Constants.RegexConstants;

namespace Cliptok.Checks
{
    public class ListChecks
    {
        // Map of Cyrillic to Latin characters, to catch attempted bypasses using Cyrillic lookalikes
        // <string, string> is <Cyrillic, Latin>
        public static Dictionary<string, string> alphabetMap = new()
                            {
                                { "А", "A" },
                                { "В", "B" },
                                { "С", "C" },
                                { "Е", "E" },
                                { "Ԍ", "G" },
                                { "Н", "H" },
                                { "І", "I" },
                                { "Ӏ", "I" },
                                { "ӏ", "I" },
                                { "Ј", "J" },
                                { "К", "K" },
                                { "М", "M" },
                                { "О", "O" },
                                { "Р", "P" },
                                { "Ѕ", "S" },
                                { "Т", "T" },
                                { "Ѵ", "V" },
                                { "Ԝ", "W" },
                                { "Х", "X" },
                                { "Ү", "Y" },
                                { "ү", "Y" },
                                { "а", "a" },
                                { "Ь", "b" },
                                { "с", "c" },
                                { "ԁ", "d" },
                                { "е", "e" },
                                { "ҽ", "e" },
                                { "һ", "h" },
                                { "і", "i" },
                                { "ј", "j" },
                                { "о", "o" },
                                { "р", "p" },
                                { "ԛ", "q" },
                                { "г", "r" },
                                { "ѕ", "s" },
                                { "ѵ", "v" },
                                { "ѡ", "w" },
                                { "х", "x" },
                                { "у", "y" },
                                { "У", "y" }
                            };

        public static (bool success, string? flaggedWord) CheckForNaughtyWords(string input, WordListJson naughtyWordList)
        {
            // Replace any Cyrillic letters found in message with Latin characters, if in the dictionary
            foreach (var letter in alphabetMap)
                input = input.Replace(letter.Key, letter.Value);

            string[] naughtyWords = naughtyWordList.Words;
            input = input.Replace("\0", "");
            if (naughtyWordList.WholeWord)
            {
                input = input.Replace("\'", " ")
                    .Replace("-", " ")
                    .Replace("_", " ")
                    .Replace(".", " ")
                    .Replace(":", " ")
                    .Replace("/", " ")
                    .Replace(",", " ");

                char[] tempArray = input.ToCharArray();

                tempArray = Array.FindAll(tempArray, c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c));
                input = new string(tempArray);

                string[] arrayOfWords = input.Split(' ');

                for (int i = 0; i < arrayOfWords.Length; i++)
                {
                    bool isNaughty = false;
                    foreach (string naughty in naughtyWords)
                    {
                        string naughtyWord = naughty.ToLower();
                        string distinctString = new(arrayOfWords[i].Replace(naughtyWord, "#").Distinct().ToArray());
                        if (distinctString.Length <= 3 && arrayOfWords[i].Contains(naughtyWord))
                        {
                            if (distinctString.Length == 1)
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 2 && (naughtyWord.EndsWith(distinctString[1].ToString()) || naughtyWord.StartsWith(distinctString[0].ToString())))
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 3 && naughtyWord.EndsWith(distinctString[1].ToString()) && naughtyWord.StartsWith(distinctString[0].ToString()))
                            {
                                isNaughty = true;
                            }
                        }
                        if (arrayOfWords[i] == "")
                        {
                            isNaughty = false;
                        }
                        if (isNaughty)
                        {
                            return (true, naughtyWord);
                        }
                    }
                }
                return (false, null);
            }
            else if (naughtyWordList.Url)
            {
                var urlMatches = url_rx.Matches(input);
                foreach (Match match in urlMatches)
                {
                    if (naughtyWords.Contains(match.Value))
                        return (true, match.Value);
                }
                return (false, null);
            }
            {
                foreach (string word in naughtyWords)
                {
                    if (!string.IsNullOrWhiteSpace(word) && input.Contains(word.ToLower()))
                    {
                        return (true, word);
                    }
                }
                return (false, null);
            }

        }


    }
}
