using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using praca_dyplomowa_zesp.Data; // Twój namespace z DbContext

namespace praca_dyplomowa_zesp.Services
{
    public class TrashCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrashCleanupService> _logger;

        public TrashCleanupService(IServiceProvider serviceProvider, ILogger<TrashCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serwis czyszczenia kosza został uruchomiony.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanTrashAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas automatycznego czyszczenia kosza.");
                }

                // Sprawdzaj co 24 godziny (lub częściej, np. TimeSpan.FromHours(1))
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }

        private async Task CleanTrashAsync()
        {
            // Musimy utworzyć zakres (scope), bo DbContext jest "Scoped", a BackgroundService jest "Singleton"
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // Data graniczna: 30 dni temu
                var thresholdDate = DateTime.Now.AddDays(-30);

                // Pobierz elementy w koszu starsze niż 30 dni
                var oldGuides = context.Guides
                    .Where(g => g.IsDeleted && g.DeletedAt < thresholdDate)
                    .ToList();

                if (oldGuides.Any())
                {
                    _logger.LogInformation($"Znaleziono {oldGuides.Count} starych poradników do usunięcia.");

                    context.Guides.RemoveRange(oldGuides);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Kosz został automatycznie oczyszczony.");
                }
            }
        }
    }
}