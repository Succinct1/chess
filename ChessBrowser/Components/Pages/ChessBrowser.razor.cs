using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using ChessBrowser.Components;

namespace ChessBrowser.Components.Pages
{
    public partial class ChessBrowser
    {
        /// <summary>
        /// Bound to the Unsername form input
        /// </summary>
        private string Username = "";

        /// <summary>
        /// Bound to the Password form input
        /// </summary>
        private string Password = "";

        /// <summary>
        /// Bound to the Database form input
        /// </summary>
        private string Database = "";

        /// <summary>
        /// Represents the progress percentage of the current
        /// upload operation. Update this value to update 
        /// the progress bar.
        /// </summary>
        private int Progress = 0;


        /// <summary>
        /// This method runs when a PGN file is selected for upload.
        /// Given a list of lines from the selected file, parses the 
        /// PGN data, and uploads each chess game to the user's database.
        /// </summary>
        /// <param name="PGNFileLines">The lines from the selected file</param>
        private async Task InsertGameData(string[] PGNFileLines)
        {
            string tempFilePath = Path.GetTempFileName();
            await File.WriteAllLinesAsync(tempFilePath, PGNFileLines);

            List<ChessGame> games = PgnParser.ParsePgnFile(tempFilePath);
            string connection = GetConnectionString();

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                try
                {
                    conn.Open();
                    int totalGames = games.Count;
                    int processedGames = 0;

                    foreach (var game in games)
                    {
                        int whitePlayerId = GetOrInsertPlayer(conn, game.White, game.WhiteElo);
                        int blackPlayerId = GetOrInsertPlayer(conn, game.Black, game.BlackElo);
                        int eventId = GetOrInsertEvent(conn, game.EventName, game.Site, game.EventDate);

                        string checkGameQuery = @"
                    SELECT Result, Moves FROM Games 
                    WHERE Round = @Round AND BlackPlayer = @BlackPlayer 
                    AND WhitePlayer = @WhitePlayer AND eID = @EventID";

                        using (MySqlCommand checkCmd = new MySqlCommand(checkGameQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@Round", game.Round);
                            checkCmd.Parameters.AddWithValue("@BlackPlayer", blackPlayerId);
                            checkCmd.Parameters.AddWithValue("@WhitePlayer", whitePlayerId);
                            checkCmd.Parameters.AddWithValue("@EventID", eventId);

                            using (MySqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    string existingResult = reader["Result"].ToString();
                                    string existingMoves = reader["Moves"].ToString();
                                    reader.Close(); // ✅ Close before running the UPDATE

                                    if (existingResult != game.Result.ToString() || existingMoves != game.Moves)
                                    {
                                        string updateQuery = @"
                                    UPDATE Games 
                                    SET Result = @Result, Moves = @Moves 
                                    WHERE Round = @Round AND BlackPlayer = @BlackPlayer 
                                    AND WhitePlayer = @WhitePlayer AND eID = @EventID";

                                        using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn))
                                        {
                                            updateCmd.Parameters.AddWithValue("@Round", game.Round);
                                            updateCmd.Parameters.AddWithValue("@Result", game.Result);
                                            updateCmd.Parameters.AddWithValue("@Moves", game.Moves);
                                            updateCmd.Parameters.AddWithValue("@BlackPlayer", blackPlayerId);
                                            updateCmd.Parameters.AddWithValue("@WhitePlayer", whitePlayerId);
                                            updateCmd.Parameters.AddWithValue("@EventID", eventId);
                                            updateCmd.ExecuteNonQuery();
                                        }
                                    }

                                    continue; // ✅ Skip inserting if game already exists
                                }
                            }
                        }

                        // Insert new game
                        string insertGameQuery = @"
                    INSERT INTO Games (Round, Result, Moves, BlackPlayer, WhitePlayer, eID) 
                    VALUES (@Round, @Result, @Moves, @BlackPlayer, @WhitePlayer, @EventID)";

                        using (MySqlCommand cmd = new MySqlCommand(insertGameQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Round", game.Round);
                            cmd.Parameters.AddWithValue("@Result", game.Result);
                            cmd.Parameters.AddWithValue("@Moves", game.Moves);
                            cmd.Parameters.AddWithValue("@BlackPlayer", blackPlayerId);
                            cmd.Parameters.AddWithValue("@WhitePlayer", whitePlayerId);
                            cmd.Parameters.AddWithValue("@EventID", eventId);
                            cmd.ExecuteNonQuery();
                        }

                        processedGames++;
                        Progress = (processedGames * 100) / totalGames;
                        await InvokeAsync(StateHasChanged);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Database Error: " + e.Message);
                }
            }
        }


        /// <summary>
        /// Ensures a player exists in the database, inserts if not found.
        /// </summary>
        private int GetOrInsertPlayer(MySqlConnection conn, string playerName, int elo)
        {
            string selectQuery = "SELECT pID, Elo FROM Players WHERE Name = @Name";
            using (MySqlCommand cmd = new MySqlCommand(selectQuery, conn))
            {
                cmd.Parameters.AddWithValue("@Name", playerName);
                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read()) // Player exists
                    {
                        int playerId = Convert.ToInt32(reader["pID"]);
                        int currentElo = Convert.ToInt32(reader["Elo"]);
                        reader.Close(); // Close before running the UPDATE

                        // Update Elo only if higher
                        if (elo > currentElo)
                        {
                            string updateQuery = "UPDATE Players SET Elo = @Elo WHERE pID = @PlayerID";
                            using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, conn))
                            {
                                updateCmd.Parameters.AddWithValue("@Elo", elo);
                                updateCmd.Parameters.AddWithValue("@PlayerID", playerId);
                                updateCmd.ExecuteNonQuery();
                            }
                        }

                        return playerId;
                    }
                }
            }

            // Insert new player if not found
            string insertQuery = "INSERT INTO Players (Name, Elo) VALUES (@Name, @Elo)";
            using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
            {
                insertCmd.Parameters.AddWithValue("@Name", playerName);
                insertCmd.Parameters.AddWithValue("@Elo", elo);
                insertCmd.ExecuteNonQuery();
            }

            using (MySqlCommand getIdCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", conn))
            {
                return Convert.ToInt32(getIdCmd.ExecuteScalar());
            }
        }


        /// <summary>
        /// Ensures an event exists in the database, inserts if not found.
        /// </summary>
        private int GetOrInsertEvent(MySqlConnection conn, string eventName, string site, string eventDate)
        {
            string query = "SELECT eID FROM Events WHERE Name = @Name AND Site = @Site AND Date = @Date";
            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Name", eventName);
                cmd.Parameters.AddWithValue("@Site", site);
                cmd.Parameters.AddWithValue("@Date", eventDate);
                object result = cmd.ExecuteScalar();

                if (result != null) return Convert.ToInt32(result);

                string insertQuery = "INSERT INTO Events (Name, Site, Date) VALUES (@Name, @Site, @Date)";
                using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Name", eventName);
                    insertCmd.Parameters.AddWithValue("@Site", site);
                    insertCmd.Parameters.AddWithValue("@Date", eventDate);
                    insertCmd.ExecuteNonQuery();
                }

                return Convert.ToInt32(cmd.LastInsertedId);
            }
        }

        /// <summary>
        /// Queries the database for games that match all the given filters.
        /// The filters are taken from the various controls in the GUI.
        /// </summary>
        /// <param name="white">The white player, or "" if none</param>
        /// <param name="black">The black player, or "" if none</param>
        /// <param name="opening">The first move, e.g. "1.e4", or "" if none</param>
        /// <param name="winner">The winner as "W", "B", "D", or "" if none</param>
        /// <param name="useDate">true if the filter includes a date range, false otherwise</param>
        /// <param name="start">The start of the date range</param>
        /// <param name="end">The end of the date range</param>
        /// <param name="showMoves">true if the returned data should include the PGN moves</param>
        /// <returns>A string separated by newlines containing the filtered games</returns>
        private string PerformQuery(string white, string black, string opening,
            string winner, bool useDate, DateTime start, DateTime end, bool showMoves)
        {
            string connection = GetConnectionString();
            var results = new List<string>();

            using (MySqlConnection conn = new MySqlConnection(connection))
            {
                conn.Open();

                string query = @"
            SELECT g.*, 
                   wp.Name AS WhitePlayerName, wp.Elo AS WhiteElo,
                   bp.Name AS BlackPlayerName, bp.Elo AS BlackElo,
                   e.Name AS EventName, e.Site, e.Date
            FROM Games g
            JOIN Players wp ON g.WhitePlayer = wp.pID
            JOIN Players bp ON g.BlackPlayer = bp.pID
            JOIN Events e ON g.eID = e.eID
            WHERE 1=1";

                var parameters = new List<MySqlParameter>();

                if (!string.IsNullOrEmpty(white))
                {
                    query += " AND wp.Name = @White";
                    parameters.Add(new MySqlParameter("@White", white));
                }

                if (!string.IsNullOrEmpty(black))
                {
                    query += " AND bp.Name = @Black";
                    parameters.Add(new MySqlParameter("@Black", black));
                }

                if (!string.IsNullOrEmpty(winner))
                {
                    query += " AND g.Result = @Result";
                    parameters.Add(new MySqlParameter("@Result", winner));
                }

                if (!string.IsNullOrEmpty(opening))
                {
                    query += " AND g.Moves LIKE @Opening";
                    parameters.Add(new MySqlParameter("@Opening", opening + "%"));
                }

                if (useDate)
                {
                    query += " AND e.Date BETWEEN @Start AND @End";
                    parameters.Add(new MySqlParameter("@Start", start.ToString("yyyy-MM-dd")));
                    parameters.Add(new MySqlParameter("@End", end.ToString("yyyy-MM-dd")));
                }


                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddRange(parameters.ToArray());

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string resultText = $@"Event: {reader["EventName"]}
Site: {reader["Site"]}
Date: {((DateTime)reader["Date"]).ToString("MM/dd/yyyy")}
White: {reader["WhitePlayerName"]} ({reader["WhiteElo"]})
Black: {reader["BlackPlayerName"]} ({reader["BlackElo"]})
Result: {reader["Result"]}";

                            if (showMoves)
                            {
                                resultText += $"\n{reader["Moves"]}";
                            }

                            results.Add(resultText);
                        }
                    }
                }
            }

            return $"{results.Count} results\n\n" + string.Join("\n\n", results);
        }


        private string GetConnectionString()
        {
            return "server=atr.eng.utah.edu;database=" + Database + ";uid=" + Username + ";password=" + Password;
        }


        /// <summary>
        /// This method will run when the file chooser is used.
        /// It loads the file's contents as an array of strings,
        /// then invokes the InsertGameData method.
        /// </summary>
        /// <param name="args">The event arguments, which contains the selected file name</param>
        private async void HandleFileChooser(EventArgs args)
        {
            try
            {
                InputFileChangeEventArgs eventArgs =
                    args as InputFileChangeEventArgs ?? throw new Exception("Unable to get file name");

                if (eventArgs.FileCount == 1)
                {
                    var file = eventArgs.File;
                    if (file is null) return;

                    // Load the chosen file and split it into an array of strings, one per line
                    using var stream = file.OpenReadStream(1000000); // max 1MB
                    using var reader = new StreamReader(stream);
                    string fileContent = await reader.ReadToEndAsync();
                    string[] fileLines =
                        fileContent.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    // Insert the games, and don't wait for it to finish
                    _ = InsertGameData(fileLines);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading the file: {e.Message}");
            }
        }
    }
}