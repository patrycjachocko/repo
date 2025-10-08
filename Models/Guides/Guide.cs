namespace praca_dyplomowa_zesp.Models.Guides
{
    public class Guide
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Author { get; set; } //ściągać z usera nazwę czy odnośnik do usera? żeby wejść na profil
        public string Category { get; set; } //tytuł gry?
        public string ImageUrl { get; set; } //czy wiele? raczej tak. jak to zrobić? nw
        public bool IsPublished { get; set; } //nw czy bedzie potrzebne
        public int Views { get; set; } //nw jak to niby bedziemy liczyc ale slay
        public int Likes { get; set; }
        public int CommentsCount { get; set; }
        //public string Slug { get; set; } //nw co to
        public string Summary { get; set; } //raczej nie bedzie
        public string Tags { get; set; }
        public string Language { get; set; } //?
        //public string Difficulty { get; set; } //raczej nbd
        public int EstimatedReadTime { get; set; } //to by bylo cool
        //public string SourceUrl { get; set; } //?
    }
}
