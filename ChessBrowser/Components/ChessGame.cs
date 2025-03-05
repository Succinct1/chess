namespace ChessBrowser.Components
{
    public class ChessGame
    {
        public string EventName{ get; set; }
        public string Site { get; set; }
        public string Date { get; set; }
        public string Round { get; set; }
        public string White { get; set; }
        public string Black { get; set; }
        public int WhiteElo { get; set; }
        public int BlackElo { get; set; }
        public char Result { get; set; } // 'W', 'B', 'D'
        public string EventDate { get; set; }
        public string Moves { get; set; }
    }
}