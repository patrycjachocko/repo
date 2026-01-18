using System;

namespace praca_dyplomowa_zesp.Models.ViewModels.Users
{
    // Ten plik powinien być oddzielny od User.cs
    public class ProfileViewModel
    {
        public Guid UserId { get; set; }
        public string Username { get; set; }

        // Pole do przechowywania ID Steam w widoku
        public string? SteamId { get; set; }
        public bool IsCreatedBySteam { get; set; }
    }
}