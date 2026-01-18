namespace praca_dyplomowa_zesp.Models.Modules.Users
{
    public class AdminUserDto //obiekt transferowy zawierający kluczowe dane użytkownika dla administracji
    {
        public Guid Id { get; set; }

        public string UserName { get; set; }

        public string Login { get; set; }

        public string AvatarUrl { get; set; }

        public List<string> Roles { get; set; } = new List<string>();

        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public bool IsMuted { get; set; }
        public DateTimeOffset? MuteEnd { get; set; }
    }
}