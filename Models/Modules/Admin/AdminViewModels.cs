using praca_dyplomowa_zesp.Models.Modules.Guides;
using System;
using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.Modules.Admin
{
    public class AdminPanelViewModel
    {
        public List<AdminUserDto> Users { get; set; } = new List<AdminUserDto>();
        public string SearchString { get; set; }
        public List<Guide> PendingGuides { get; set; }
        public List<Guide> DeletedGuides { get; set; } = new List<Guide>();
        public List<praca_dyplomowa_zesp.Models.Ticket> Tickets { get; set; } = new List<praca_dyplomowa_zesp.Models.Ticket>();
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