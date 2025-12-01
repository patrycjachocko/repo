using System;
using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.Modules.Admin
{
    public class AdminPanelViewModel
    {
        public List<AdminUserDto> Users { get; set; } = new List<AdminUserDto>();
        public string SearchString { get; set; }
    }

    public class AdminUserDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } // Nazwa wyświetlana
        public string Login { get; set; }    // Login do logowania (ZMIANA)
        public string AvatarUrl { get; set; }
        public List<string> Roles { get; set; } = new List<string>();

        // Statusy
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public bool IsMuted { get; set; }
        public DateTimeOffset? MuteEnd { get; set; }
    }
}