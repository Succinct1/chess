using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ChessBrowser.Components
{
    public static class PgnParser
    {
        public static List<ChessGame> ParsePgnFile(string filePath)
        {
            List<ChessGame> games = new List<ChessGame>();
            string[] lines = File.ReadAllLines(filePath);
            ChessGame currentGame = new ChessGame();
            bool readingMoves = false;
            string moves = "";

            Regex tagRegex = new Regex(@"\[(\w+) ""(.+?)""\]");

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (readingMoves && !string.IsNullOrEmpty(moves))
                    {
                        currentGame.Moves = CleanMoves(moves);
                        if (IsValidGame(currentGame)) games.Add(currentGame);
                        currentGame = new ChessGame();
                        readingMoves = false;
                        moves = "";
                    }
                    continue;
                }

                if (trimmedLine.StartsWith("["))
                {
                    Match match = tagRegex.Match(trimmedLine);
                    if (match.Success)
                    {
                        string tag = match.Groups[1].Value;
                        string value = match.Groups[2].Value;

                        switch (tag)
                        {
                            case "Event": currentGame.EventName = value; break;
                            case "Site": currentGame.Site = value == "?" ? "Unknown" : value; break;
                            case "Date": currentGame.Date = value; break;
                            case "Round": currentGame.Round = value.Contains("??") ? "0" : value; break;
                            case "White": currentGame.White = value; break;
                            case "Black": currentGame.Black = value; break;
                            case "WhiteElo": currentGame.WhiteElo = int.TryParse(value, out int we) ? we : 0; break;
                            case "BlackElo": currentGame.BlackElo = int.TryParse(value, out int be) ? be : 0; break;
                            case "Result":
                                currentGame.Result = value switch
                                {
                                    "1-0" => 'W',
                                    "0-1" => 'B',
                                    "1/2-1/2" => 'D',
                                    _ => '?'
                                };
                                break;
                            case "EventDate":
                                currentGame.EventDate = value.Contains("??") ? "0000-00-00" : value;
                                break;
                        }
                    }
                }
                else
                {
                    readingMoves = true;
                    moves += " " + trimmedLine;
                }
            }

            if (!string.IsNullOrEmpty(moves))
            {
                currentGame.Moves = CleanMoves(moves);
                if (IsValidGame(currentGame)) games.Add(currentGame);
            }

            return games;
        }

        private static string CleanMoves(string moves)
        {
            // Use a single regex replacement to remove comments, variations, and move numbers
            return Regex.Replace(moves, @"\{.*?\}|\(.*?\)|\d+\.", "").Trim();
        }

        private static bool IsValidGame(ChessGame game)
        {
            return !string.IsNullOrEmpty(game.White) &&
                   !string.IsNullOrEmpty(game.Black) &&
                   game.Result != '?';
        }
    }
}
