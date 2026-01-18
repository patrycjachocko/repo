using praca_dyplomowa_zesp.Data;

namespace praca_dyplomowa_zesp.Services
{
    public class TrashCleanupService : BackgroundService //usługa działająca w tle, odpowiedzialna za cykliczne opróżnianie kosza z usuniętymi poradnikami
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
            _logger.LogInformation("Serwis czyszczenia kosza został zainicjalizowany.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanTrashAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wystąpił nieoczekiwany błąd podczas procedury czyszczenia kosza.");
                }

                //czyszczenie co 24 godziny
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }

        private async Task CleanTrashAsync()
        {
            //tworzenie tymczasowego zakresu (scope) w celu uzyskania dostępu do usług o cyklu życia Scoped (DbContext)
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                //definicja progu retencji danych: starsze niż 30 dni
                var thresholdDate = DateTime.Now.AddDays(-30);

                //identyfikacja poradników oznaczonych jako usunięte, których termin przechowywania w koszu upłynął
                var oldGuides = context.Guides
                    .Where(g => g.IsDeleted && g.DeletedAt < thresholdDate)
                    .ToList();

                if (oldGuides.Any())
                {
                    _logger.LogInformation($"Procedura czyszczenia: wykryto {oldGuides.Count} rekordów do trwałego usunięcia.");

                    context.Guides.RemoveRange(oldGuides);
                    await context.SaveChangesAsync();

                    _logger.LogInformation("Zasoby kosza zostały pomyślnie zutylizowane.");
                }
            }
        }
    }
}