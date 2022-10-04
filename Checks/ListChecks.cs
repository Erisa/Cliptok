using static Cliptok.Constants.RegexConstants;

namespace Cliptok.Checks
{
    public class ListChecks
    {
        public static (bool success, string? flaggedWord) CheckForNaughtyWords(string input, WordListJson naughtyWordList)
        {
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
                    string naughtyWord = "";
                    bool isNaughty = false;
                    foreach (string naughty in naughtyWords)
                    {
                        string distinctString = new(arrayOfWords[i].Replace(naughty, "#").Distinct().ToArray());
                        if (distinctString.Length <= 3 && arrayOfWords[i].Contains(naughty))
                        {
                            if (distinctString.Length == 1)
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 2 && (naughty.EndsWith(distinctString[1].ToString()) || naughty.StartsWith(distinctString[0].ToString())))
                            {
                                isNaughty = true;
                            }
                            else if (distinctString.Length == 3 && naughty.EndsWith(distinctString[1].ToString()) && naughty.StartsWith(distinctString[0].ToString()))
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
                            return (true, naughty);
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
                    if (!string.IsNullOrWhiteSpace(word) && input.Contains(word))
                    {
                        return (true, word);
                    }
                }
                return (false, null);
            }

        }


    }
}
