using System;

namespace praca_dyplomowa_zesp.Models.Users
{
    // Ten plik powinien być oddzielny od User.cs
    public class ProfileViewModel
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }
        public string? Description { get; set; }

        // Pole do przechowywania ID Steam w widoku
        public string? SteamId { get; set; }
    }
}